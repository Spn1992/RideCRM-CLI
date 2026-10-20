using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class MockFailingShippingService : IShippingService
    {
        public Task<ShippingRateQuoteResult> GetRateQuoteAsync(ShippingRateRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShippingRateQuoteResult { Success = false, ErrorMessage = "UPS API standard rate quote failure" });
        }

        public Task<ShipmentDispatchResult> CreateShipmentLabelAsync(ShipmentDispatchRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShipmentDispatchResult { Success = false, ErrorMessage = "UPS Shipping API timeout or 503 Service Unavailable" });
        }
    }

    public class ShipmentDispatchWorkflowTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private async Task SeedDatabaseAsync(ApplicationDbContext context)
        {
            context.SystemConfiguration.AddRange(
                new SystemConfiguration { ConfigKey = "UPS_ClientId", ConfigValue = "MOCK_CLIENT_ID", DataType = "String" },
                new SystemConfiguration { ConfigKey = "UPS_ClientSecret", ConfigValue = "MOCK_CLIENT_SECRET", DataType = "String" }
            );

            var part = new Part
            {
                PartId = 10,
                PartNumber = "RIDE-PUMP-01",
                Description = "Hydraulic Brake Pump",
                UnitOfMeasure = "Ea",
                QuantityOnHand = 20m,
                ReservedQuantity = 5m,
                AvailableQuantity = "15"
            };

            var holdingLoc = new HoldingLocation
            {
                HoldingLocationId = 100,
                LocationCode = "HL-STAGE-01",
                LocationName = "Staging Holding Bin 01",
                IsAvailable = false // Currently occupied by reserved order stock
            };

            var order = new CustomerOrder
            {
                OrderId = 50,
                OrderNumber = "ORD-2026-001",
                OrderType = "Order",
                Status = "Confirmed",
                HoldingLocationId = 100,
                OrderDate = DateTime.UtcNow
            };

            var orderLine = new CustomerOrderLine
            {
                OrderLineId = 500,
                OrderId = 50,
                LineNumber = 1,
                PartId = 10,
                Quantity = 5m,
                QuantityReserved = 5m,
                QuantityShipped = 0m,
                LineStatus = "Confirmed"
            };

            var reservation = new InventoryReservation
            {
                ReservationId = 1000,
                OrderLineId = 500,
                PartId = 10,
                QuantityReserved = 5m,
                HoldingLocationId = 100,
                ReservationDate = DateTime.UtcNow,
                Status = "Reserved"
            };

            var shipment = new Shipment
            {
                ShipmentId = 1,
                ShipmentNumber = "SHP-2026-001",
                OrderId = 50,
                ShipmentDate = DateTime.UtcNow,
                Carrier = "UPS",
                ServiceType = "03",
                Status = "Draft",
                ShipToName = "Cedar Point Amusement Park",
                ShipToAddress1 = "1 Cedar Point Dr",
                ShipToCity = "Sandusky",
                ShipToState = "OH",
                ShipToPostalCode = "44870"
            };

            var shipmentLine = new ShipmentLine
            {
                ShipmentLineId = 10,
                ShipmentId = 1,
                OrderLineId = 500,
                PartId = 10,
                QuantityShipped = 5m,
                CreatedDate = DateTime.UtcNow
            };

            var shipmentPackage = new ShipmentPackage
            {
                PackageId = 100,
                ShipmentId = 1,
                PackageNumber = 1,
                WeightLbs = 25m,
                LengthIn = 18m,
                WidthIn = 14m,
                HeightIn = 12m,
                CreatedDate = DateTime.UtcNow
            };

            context.Part.Add(part);
            context.HoldingLocation.Add(holdingLoc);
            context.CustomerOrder.Add(order);
            context.CustomerOrderLine.Add(orderLine);
            context.InventoryReservation.Add(reservation);
            context.Shipment.Add(shipment);
            context.ShipmentLine.Add(shipmentLine);
            context.ShipmentPackage.Add(shipmentPackage);

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task DispatchShipmentAsync_OnSuccess_StoresTrackingReleasesHoldingLocationAndUpdatesPartStock()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedDatabaseAsync(context);
            }

            using (var context = new ApplicationDbContext(options))
            {
                var shippingService = new UpsShippingService(new HttpClient(), context);

                // Act
                var result = await ShipmentDispatchWorkflow.DispatchShipmentAsync(
                    context,
                    shippingService,
                    shipmentId: 1,
                    user: "Dispatcher John");

                // Assert Dispatch Result
                Assert.True(result.Success);
                Assert.NotNull(result.TrackingNumber);
                Assert.StartsWith("1Z99999903", result.TrackingNumber);
                Assert.NotNull(result.ShippingLabelUrl);
            }

            // Verify State persisted in DB
            using (var context = new ApplicationDbContext(options))
            {
                var shipment = await context.Shipment.FirstAsync(s => s.ShipmentId == 1);
                var pkg = await context.ShipmentPackage.FirstAsync(p => p.PackageId == 100);
                var reservation = await context.InventoryReservation.FirstAsync(r => r.ReservationId == 1000);
                var holdingLoc = await context.HoldingLocation.FirstAsync(h => h.HoldingLocationId == 100);
                var part = await context.Part.FirstAsync(p => p.PartId == 10);
                var order = await context.CustomerOrder.FirstAsync(o => o.OrderId == 50);
                var orderLine = await context.CustomerOrderLine.FirstAsync(l => l.OrderLineId == 500);

                // 1. Shipment updated
                Assert.Equal("Dispatched", shipment.Status);
                Assert.NotNull(shipment.TrackingNumber);
                Assert.NotNull(shipment.ShippingLabelUrl);
                Assert.Equal("Dispatcher John", shipment.ModifiedBy);

                // 2. Package tracking updated
                Assert.NotNull(pkg.TrackingNumber);
                Assert.NotNull(pkg.LabelImageUrl);

                // 3. Inventory Reservation Released
                Assert.Equal("Released", reservation.Status);
                Assert.NotNull(reservation.ReleasedDate);

                // 4. Holding Location Availability Restored
                Assert.True(holdingLoc.IsAvailable);

                // 5. Part Stock Levels Updated
                Assert.Equal(0m, part.ReservedQuantity); // Reduced from 5m to 0m
                Assert.Equal("20", part.AvailableQuantity); // 20 - 0 = 20

                // 6. Order and Line Status Updated
                Assert.Equal("Shipped", orderLine.LineStatus);
                Assert.Equal(5m, orderLine.QuantityShipped);
                Assert.Equal(0m, orderLine.QuantityReserved);
                Assert.Equal("Shipped", order.Status);
            }
        }

        [Fact]
        public async Task DispatchShipmentAsync_WhenCarrierFails_PreservesReservedStockAndHoldingLocation()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedDatabaseAsync(context);
            }

            using (var context = new ApplicationDbContext(options))
            {
                var failingService = new MockFailingShippingService();

                // Act
                var result = await ShipmentDispatchWorkflow.DispatchShipmentAsync(
                    context,
                    failingService,
                    shipmentId: 1,
                    user: "Dispatcher John");

                // Assert Dispatch Result
                Assert.False(result.Success);
                Assert.Contains("UPS Shipping API timeout", result.ErrorMessage);
            }

            // Assert DB State was untouched / preserved
            using (var context = new ApplicationDbContext(options))
            {
                var shipment = await context.Shipment.FirstAsync(s => s.ShipmentId == 1);
                var reservation = await context.InventoryReservation.FirstAsync(r => r.ReservationId == 1000);
                var holdingLoc = await context.HoldingLocation.FirstAsync(h => h.HoldingLocationId == 100);
                var part = await context.Part.FirstAsync(p => p.PartId == 10);

                Assert.Equal("Draft", shipment.Status);
                Assert.Null(shipment.TrackingNumber);
                Assert.Equal("Reserved", reservation.Status);
                Assert.False(holdingLoc.IsAvailable);
                Assert.Equal(5m, part.ReservedQuantity);
            }
        }

        [Fact]
        public async Task DispatchShipmentAsync_WhenNoPackagesConfigured_ReturnsValidationError()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                await SeedDatabaseAsync(context);
                var pkg = await context.ShipmentPackage.FirstAsync(p => p.PackageId == 100);
                context.ShipmentPackage.Remove(pkg);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new UpsShippingService(new HttpClient(), context);

                // Act
                var result = await ShipmentDispatchWorkflow.DispatchShipmentAsync(context, service, shipmentId: 1);

                // Assert
                Assert.False(result.Success);
                Assert.Contains("At least one package with weight and dimensions is required", result.ErrorMessage);
            }
        }
    }
}
