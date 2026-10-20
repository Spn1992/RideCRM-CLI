using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class InventoryReservationServiceTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task ReserveForCustomerOrderLine_SufficientStock_ReservesAllAndUpdatesPart()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 1,
                    PartNumber = "PART-001",
                    Description = "Test Roller Bearing",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 10m,
                    ReservedQuantity = 2m
                };
                var order = new CustomerOrder { OrderId = 1, OrderNumber = "ORD-100", Status = "Draft" };
                var line = new CustomerOrderLine { OrderLineId = 1, OrderId = 1, PartId = 1, LineNumber = 1, Quantity = 5m, UnitPriceUsd = 20m };

                context.Part.Add(part);
                context.CustomerOrder.Add(order);
                context.CustomerOrderLine.Add(line);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new InventoryReservationService(context);
                var result = await service.ReserveForCustomerOrderLineAsync(1, "TestUser");

                Assert.True(result.Success);
                Assert.Equal(5m, result.QuantityRequested);
                Assert.Equal(5m, result.QuantityReserved);
                Assert.Equal(0m, result.QuantityDeficit);
                Assert.NotNull(result.Reservation);
                Assert.Null(result.PurchasingQueueItem);

                var updatedPart = await context.Part.FirstAsync(p => p.PartId == 1);
                Assert.Equal(7m, updatedPart.ReservedQuantity);
                Assert.Equal(3m, updatedPart.AvailableQuantity);

                var updatedLine = await context.CustomerOrderLine.FirstAsync(l => l.OrderLineId == 1);
                Assert.Equal(5m, updatedLine.QuantityReserved);
                Assert.Equal("0", updatedLine.QuantityBackordered);
                Assert.Equal("Reserved", updatedLine.LineStatus);
            }
        }

        [Fact]
        public async Task ReserveForCustomerOrderLine_PartialStock_ReservesAvailableAndCreatesPurchasingQueueItem()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 2,
                    PartNumber = "PART-002",
                    Description = "Test Hydraulic Pump",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 10m,
                    ReservedQuantity = 6m
                };
                var order = new CustomerOrder { OrderId = 2, OrderNumber = "ORD-200", Status = "Draft" };
                var line = new CustomerOrderLine { OrderLineId = 2, OrderId = 2, PartId = 2, LineNumber = 1, Quantity = 10m, UnitPriceUsd = 100m };

                context.Part.Add(part);
                context.CustomerOrder.Add(order);
                context.CustomerOrderLine.Add(line);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new InventoryReservationService(context);
                var result = await service.ReserveForCustomerOrderLineAsync(2, "TestUser");

                Assert.True(result.Success);
                Assert.Equal(10m, result.QuantityRequested);
                Assert.Equal(4m, result.QuantityReserved);
                Assert.Equal(6m, result.QuantityDeficit);
                Assert.NotNull(result.Reservation);
                Assert.NotNull(result.PurchasingQueueItem);

                Assert.Equal(6m, result.PurchasingQueueItem.QuantityRequired);
                Assert.Equal("Pending", result.PurchasingQueueItem.Status);
                Assert.Equal("CustomerOrder", result.PurchasingQueueItem.SourceType);
                Assert.Equal(2, result.PurchasingQueueItem.SourceOrderLineId);

                var updatedPart = await context.Part.FirstAsync(p => p.PartId == 2);
                Assert.Equal(10m, updatedPart.ReservedQuantity);
                Assert.Equal(0m, updatedPart.AvailableQuantity);

                var updatedLine = await context.CustomerOrderLine.FirstAsync(l => l.OrderLineId == 2);
                Assert.Equal(4m, updatedLine.QuantityReserved);
                Assert.Equal("6", updatedLine.QuantityBackordered);
                Assert.Equal("Partially Reserved", updatedLine.LineStatus);
            }
        }

        [Fact]
        public async Task ReserveForCustomerOrderLine_ZeroStock_RoutesAllToPurchasingQueueItem()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 3,
                    PartNumber = "PART-003",
                    Description = "Test Motor",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 5m,
                    ReservedQuantity = 5m
                };
                var order = new CustomerOrder { OrderId = 3, OrderNumber = "ORD-300", Status = "Draft" };
                var line = new CustomerOrderLine { OrderLineId = 3, OrderId = 3, PartId = 3, LineNumber = 1, Quantity = 8m, UnitPriceUsd = 150m };

                context.Part.Add(part);
                context.CustomerOrder.Add(order);
                context.CustomerOrderLine.Add(line);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new InventoryReservationService(context);
                var result = await service.ReserveForCustomerOrderLineAsync(3, "TestUser");

                Assert.True(result.Success);
                Assert.Equal(8m, result.QuantityRequested);
                Assert.Equal(0m, result.QuantityReserved);
                Assert.Equal(8m, result.QuantityDeficit);
                Assert.Null(result.Reservation);
                Assert.NotNull(result.PurchasingQueueItem);

                Assert.Equal(8m, result.PurchasingQueueItem.QuantityRequired);
                Assert.Equal("Pending", result.PurchasingQueueItem.Status);

                var updatedPart = await context.Part.FirstAsync(p => p.PartId == 3);
                Assert.Equal(5m, updatedPart.ReservedQuantity);

                var updatedLine = await context.CustomerOrderLine.FirstAsync(l => l.OrderLineId == 3);
                Assert.Equal(0m, updatedLine.QuantityReserved);
                Assert.Equal("8", updatedLine.QuantityBackordered);
                Assert.Equal("Backordered", updatedLine.LineStatus);
            }
        }

        [Fact]
        public async Task CreateManualReservation_SufficientStock_SucceedsAndIncrementsReservedQuantity()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 4,
                    PartNumber = "PART-004",
                    Description = "Test Gearbox",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 20m,
                    ReservedQuantity = 5m
                };
                context.Part.Add(part);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new InventoryReservationService(context);
                var reservation = new InventoryReservation
                {
                    PartId = 4,
                    QuantityReserved = 10m,
                    Notes = "Manual reservation test"
                };

                var result = await service.CreateManualReservationAsync(reservation, "InventoryManager");

                Assert.True(result.Success);
                Assert.Equal(10m, result.QuantityReserved);

                var updatedPart = await context.Part.FirstAsync(p => p.PartId == 4);
                Assert.Equal(15m, updatedPart.ReservedQuantity);
                Assert.Equal(5m, updatedPart.AvailableQuantity);
            }
        }

        [Fact]
        public async Task CreateManualReservation_ExceedingAvailableStock_FailsGracefully()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 5,
                    PartNumber = "PART-005",
                    Description = "Test Chain",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 20m,
                    ReservedQuantity = 15m
                };
                context.Part.Add(part);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new InventoryReservationService(context);
                var reservation = new InventoryReservation
                {
                    PartId = 5,
                    QuantityReserved = 10m,
                    Notes = "Excessive reservation attempt"
                };

                var result = await service.CreateManualReservationAsync(reservation, "InventoryManager");

                Assert.False(result.Success);
                Assert.Contains("exceeds available stock", result.ErrorMessage);

                var updatedPart = await context.Part.FirstAsync(p => p.PartId == 5);
                Assert.Equal(15m, updatedPart.ReservedQuantity);
                Assert.Equal(5m, updatedPart.AvailableQuantity);

                var reservationInDb = await context.InventoryReservation.FirstOrDefaultAsync(r => r.PartId == 5);
                Assert.Null(reservationInDb);
            }
        }

        [Fact]
        public async Task ReserveForCustomerOrderLine_NonStockedPart_RoutesAllToPurchasingQueue()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 6,
                    PartNumber = "NON-STOCK-01",
                    Description = "Custom Made Harness",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 100m,
                    ReservedQuantity = 0m,
                    IsNonStocked = true
                };
                var order = new CustomerOrder { OrderId = 6, OrderNumber = "ORD-600", Status = "Draft" };
                var line = new CustomerOrderLine { OrderLineId = 6, OrderId = 6, PartId = 6, LineNumber = 1, Quantity = 5m, UnitPriceUsd = 500m };

                context.Part.Add(part);
                context.CustomerOrder.Add(order);
                context.CustomerOrderLine.Add(line);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new InventoryReservationService(context);
                var result = await service.ReserveForCustomerOrderLineAsync(6, "TestUser");

                Assert.True(result.Success);
                Assert.Equal(5m, result.QuantityRequested);
                Assert.Equal(0m, result.QuantityReserved);
                Assert.Equal(5m, result.QuantityDeficit);
                Assert.Null(result.Reservation);
                Assert.NotNull(result.PurchasingQueueItem);
                Assert.Equal(5m, result.PurchasingQueueItem.QuantityRequired);
                Assert.Equal("Pending", result.PurchasingQueueItem.Status);
            }
        }
    }
}
