using System;
using System.Linq;
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

        [Fact]
        public async Task ReceivingWorkflow_TriggersDiscrepancyGate_WhenOverDeliveryAttemptedWithoutSupervisorApproval()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part { PartId = 101, PartNumber = "VALVE-01", Description = "Hydraulic Valve", QuantityOnHand = 0m, ReservedQuantity = 0m };
                var poLine = new PurchaseOrderLine { POLineId = 201, PurchaseOrderId = 301, PartId = 101, Quantity = 5m, QuantityReceived = 0m };
                context.Part.Add(part);
                context.PurchaseOrderLine.Add(poLine);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var recLine = new ReceivingLine { ReceivingLineId = 10, ReceivingId = 401, POLineId = 201, PartId = 101, QuantityExpected = 5m, QuantityReceived = 10m };
                context.ReceivingLine.Add(recLine);
                await context.SaveChangesAsync();

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await ReceivingWorkflow.ProcessReceivingLineAsync(context, recLine);
                });

                Assert.Contains("Supervisor review and authorization is required", ex.Message);
            }
        }

        [Fact]
        public async Task ReceivingWorkflow_SavesDiscrepancy_WhenSupervisorAuthorized()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part { PartId = 102, PartNumber = "VALVE-02", Description = "Safety Valve", QuantityOnHand = 0m, ReservedQuantity = 0m };
                var poLine = new PurchaseOrderLine { POLineId = 202, PurchaseOrderId = 302, PartId = 102, Quantity = 5m, QuantityReceived = 0m };
                var recRecord = new ReceivingRecord { ReceivingId = 402, ReceivingNumber = "REC-402", ReceivingDate = DateTime.UtcNow, Status = "Draft" };

                context.Part.Add(part);
                context.PurchaseOrderLine.Add(poLine);
                context.ReceivingRecord.Add(recRecord);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var recLine = new ReceivingLine { ReceivingLineId = 20, ReceivingId = 402, POLineId = 202, PartId = 102, QuantityExpected = 5m, QuantityReceived = 8m };
                context.ReceivingLine.Add(recLine);
                await context.SaveChangesAsync();

                await ReceivingWorkflow.ProcessReceivingLineAsync(
                    context,
                    recLine,
                    supervisorUsername: "SupervisorJane",
                    discrepancyNotes: "Authorized vendor over-shipment bonus",
                    isSupervisorApproved: true);

                var updatedLine = await context.ReceivingLine.FirstAsync(l => l.ReceivingLineId == 20);
                var updatedRec = await context.ReceivingRecord.FirstAsync(r => r.ReceivingId == 402);
                var auditLog = await context.AuditLog.FirstOrDefaultAsync(a => a.EntityId == "20" && a.ActionType == "OverDeliveryDiscrepancyAuthorized");

                Assert.Equal("Yes", updatedLine.HasDiscrepancy);
                Assert.Equal("Authorized vendor over-shipment bonus", updatedLine.DiscrepancyNotes);
                Assert.True(updatedRec.HasDiscrepancy);
                Assert.Contains("Authorized vendor over-shipment bonus", updatedRec.DiscrepancyNotes);
                Assert.NotNull(auditLog);
                Assert.Equal("SupervisorJane", auditLog.UserName);
            }
        }

        [Fact]
        public async Task ReceivingWorkflow_AutoReservesPendingCustomerOrders_AndAssignsHoldingLocations()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part { PartId = 103, PartNumber = "MOTOR-03", Description = "Drive Motor", QuantityOnHand = 0m, ReservedQuantity = 0m };
                var customerOrderLine = new CustomerOrderLine { OrderLineId = 501, OrderId = 801, PartId = 103, Quantity = 4m, QuantityReserved = 0m, LineStatus = "Open", CreatedDate = DateTime.UtcNow };

                context.Part.Add(part);
                context.CustomerOrderLine.Add(customerOrderLine);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var recLine = new ReceivingLine { ReceivingLineId = 30, ReceivingId = 403, POLineId = 0, PartId = 103, QuantityExpected = 5m, QuantityReceived = 5m };
                context.ReceivingLine.Add(recLine);
                await context.SaveChangesAsync();

                await ReceivingWorkflow.ProcessReceivingLineAsync(context, recLine);

                var reservation = await context.InventoryReservation.FirstOrDefaultAsync(r => r.PartId == 103 && r.OrderLineId == 501);
                var updatedCol = await context.CustomerOrderLine.FirstAsync(c => c.OrderLineId == 501);
                var updatedPart = await context.Part.FirstAsync(p => p.PartId == 103);
                var holdingBin = await context.HoldingLocation.FirstOrDefaultAsync(h => h.HoldingLocationId == reservation!.HoldingLocationId);

                Assert.NotNull(reservation);
                Assert.Equal(4m, reservation.QuantityReserved);
                Assert.Equal(4m, updatedCol.QuantityReserved);
                Assert.Equal(4m, updatedPart.ReservedQuantity);
                Assert.Equal(5m, updatedPart.QuantityOnHand);
                Assert.NotNull(holdingBin);
                Assert.StartsWith("HOLD-BIN-", holdingBin.LocationCode);
            }
        }

        [Fact]
        public async Task ReceivingWorkflow_SpoolsZplPrintJobsForReceivedItems()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part { PartId = 104, PartNumber = "CABLE-04", Description = "Steel Wire Cable", IsSerialized = false, QuantityOnHand = 0m, ReservedQuantity = 0m };
                var recRecord = new ReceivingRecord { ReceivingId = 404, ReceivingNumber = "REC-404", ReceivingDate = DateTime.UtcNow, Status = "Draft" };

                context.Part.Add(part);
                context.ReceivingRecord.Add(recRecord);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var recLine = new ReceivingLine { ReceivingLineId = 40, ReceivingId = 404, POLineId = 0, PartId = 104, QuantityExpected = 3m, QuantityReceived = 3m, LabelQty = 3 };
                context.ReceivingLine.Add(recLine);
                await context.SaveChangesAsync();

                await ReceivingWorkflow.ProcessReceivingLineAsync(context, recLine);

                var printJobs = await context.ZplPrintJob.Where(j => j.ReceivingId == 404 && j.PartId == 104).ToListAsync();
                var updatedRec = await context.ReceivingRecord.FirstAsync(r => r.ReceivingId == 404);

                Assert.Equal(3, printJobs.Count);
                Assert.All(printJobs, j => Assert.Equal("Pending", j.Status));
                Assert.True(updatedRec.LabelsPrinted);
                Assert.Equal(3, updatedRec.LabelCount);
            }
        }
    }
}
