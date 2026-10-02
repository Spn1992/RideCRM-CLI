using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class StockAndReceivingTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public void StockAvailability_CalculatedCorrectly()
        {
            // Arrange
            var part = new Part
            {
                PartId = 1,
                PartNumber = "GEAR-101",
                Description = "Steel Gear",
                UnitOfMeasure = "Ea",
                QuantityOnHand = 50m,
                ReservedQuantity = 15m
            };

            // Act
            decimal available = part.QuantityOnHand - part.ReservedQuantity;

            // Assert
            Assert.Equal(50m, part.QuantityOnHand);
            Assert.Equal(15m, part.ReservedQuantity);
            Assert.Equal(35m, available);
        }

        [Fact]
        public async Task ReceivingWorkflow_UpdatesStockAndPOLineProgress()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 10,
                    PartNumber = "BEARING-200",
                    Description = "Roller Bearing",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 10m,
                    ReservedQuantity = 2m
                };

                var po = new PurchaseOrder
                {
                    PurchaseOrderId = 100,
                    PONumber = "PO-2026-001",
                    VendorId = 5,
                    Status = "Sent",
                    Currency = "EUR"
                };

                var poLine = new PurchaseOrderLine
                {
                    POLineId = 500,
                    PurchaseOrderId = 100,
                    LineNumber = 1,
                    PartId = 10,
                    Quantity = 20m,
                    QuantityReceived = 0m,
                    UnitCostEur = 45.00m
                };

                context.Part.Add(part);
                context.PurchaseOrder.Add(po);
                context.PurchaseOrderLine.Add(poLine);
                await context.SaveChangesAsync();
            }

            // Act 1: Partial Receiving
            using (var context = new ApplicationDbContext(options))
            {
                var recLine1 = new ReceivingLine
                {
                    ReceivingLineId = 1,
                    ReceivingId = 1000,
                    POLineId = 500,
                    PartId = 10,
                    QuantityExpected = 20m,
                    QuantityReceived = 10m
                };

                context.ReceivingLine.Add(recLine1);
                await context.SaveChangesAsync();

                await ReceivingWorkflow.ProcessReceivingLineAsync(context, recLine1);
            }

            // Assert 1: Partial receiving results
            using (var context = new ApplicationDbContext(options))
            {
                var part = await context.Part.FirstAsync(p => p.PartId == 10);
                var poLine = await context.PurchaseOrderLine.FirstAsync(l => l.POLineId == 500);
                var po = await context.PurchaseOrder.FirstAsync(p => p.PurchaseOrderId == 100);

                // Stock increased from 10 to 20
                Assert.Equal(20m, part.QuantityOnHand);
                Assert.Equal("18", part.AvailableQuantity); // 20 on hand - 2 reserved = 18

                // PO Line updated
                Assert.Equal(10m, poLine.QuantityReceived);
                Assert.Equal("10", poLine.QuantityOutstanding);

                // PO Status
                Assert.Equal("Partially Received", po.Status);
                Assert.Equal(45.00m, part.LastVendorCostEur);
            }

            // Act 2: Final Receiving to complete order
            using (var context = new ApplicationDbContext(options))
            {
                var recLine2 = new ReceivingLine
                {
                    ReceivingLineId = 2,
                    ReceivingId = 1001,
                    POLineId = 500,
                    PartId = 10,
                    QuantityExpected = 10m,
                    QuantityReceived = 10m
                };

                context.ReceivingLine.Add(recLine2);
                await context.SaveChangesAsync();

                await ReceivingWorkflow.ProcessReceivingLineAsync(context, recLine2);
            }

            // Assert 2: Final receiving results
            using (var context = new ApplicationDbContext(options))
            {
                var part = await context.Part.FirstAsync(p => p.PartId == 10);
                var poLine = await context.PurchaseOrderLine.FirstAsync(l => l.POLineId == 500);
                var po = await context.PurchaseOrder.FirstAsync(p => p.PurchaseOrderId == 100);

                // Stock increased from 20 to 30
                Assert.Equal(30m, part.QuantityOnHand);

                // PO Line fully received
                Assert.Equal(20m, poLine.QuantityReceived);
                Assert.Equal("0", poLine.QuantityOutstanding);

                // PO Status updated to Received
                Assert.Equal("Received", po.Status);
            }
        }
    }
}
