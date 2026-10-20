using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class VendorInvoiceVarianceTests
    {
        private ApplicationDbContext GetInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ProcessUploadedInvoiceAsync_PopulatesHeaderAndLineFields()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(ProcessUploadedInvoiceAsync_PopulatesHeaderAndLineFields));
            var qbService = new QBSyncQueueService(db);
            var varianceService = new VendorInvoiceVarianceService(db, qbService);

            var extractionData = new ExtractionResult
            {
                InvoiceNumber = "INV-PDF-1001",
                VendorId = 10,
                PurchaseOrderId = 20,
                InvoiceDate = new DateTime(2026, 10, 15),
                DueDate = new DateTime(2026, 11, 15),
                SubTotalEur = 500m,
                FreightEur = 50m,
                TaxEur = 0m,
                TotalEur = 550m,
                ConfidenceScore = 0.96m,
                Lines = new List<ExtractedLineItem>
                {
                    new ExtractedLineItem
                    {
                        LineNumber = 1,
                        PartId = 1,
                        VendorPartNumber = "VP-1001",
                        Description = "Brake Cylinder",
                        Quantity = 5,
                        UnitCostEur = 100m,
                        ConfidenceScore = 0.98m
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(extractionData);
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));
            var file = new FormFile(stream, 0, stream.Length, "file", "vendor_invoice_1001.pdf");

            // Act
            var invoice = await varianceService.ProcessUploadedInvoiceAsync(file, uploadedBy: "AP_Specialist");

            // Assert
            Assert.NotNull(invoice);
            Assert.Equal("INV-PDF-1001", invoice.InvoiceNumber);
            Assert.Equal(10, invoice.VendorId);
            Assert.Equal(20, invoice.PurchaseOrderId);
            Assert.Equal(550m, invoice.TotalEur);
            Assert.False(string.IsNullOrEmpty(invoice.UploadedFilePath));
            Assert.Equal("vendor_invoice_1001.pdf", invoice.UploadedFileName);
            Assert.Equal("AP_Specialist", invoice.UploadedBy);

            var lines = await db.VendorInvoiceLine.Where(l => l.VendorInvoiceId == invoice.VendorInvoiceId).ToListAsync();
            Assert.Single(lines);
            Assert.Equal("VP-1001", lines[0].VendorPartNumber);
            Assert.Equal(100m, lines[0].UnitCostEur);
        }

        [Fact]
        public async Task EvaluateInvoiceVariances_Within5Percent_SetsApprovedStatus()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(EvaluateInvoiceVariances_Within5Percent_SetsApprovedStatus));
            var qbService = new QBSyncQueueService(db);
            var varianceService = new VendorInvoiceVarianceService(db, qbService);

            db.PurchaseOrder.Add(new PurchaseOrder { PurchaseOrderId = 100, VendorId = 5 });
            db.PurchaseOrderLine.Add(new PurchaseOrderLine
            {
                POLineId = 501,
                PurchaseOrderId = 100,
                PartId = 10,
                UnitCostEur = 100.00m // Baseline = €100
            });
            await db.SaveChangesAsync();

            var invoice = new VendorInvoice
            {
                VendorInvoiceId = 1001,
                InvoiceNumber = "INV-MATCH-01",
                PurchaseOrderId = 100,
                VendorId = 5,
                TotalEur = 104.00m
            };
            db.VendorInvoice.Add(invoice);

            var line = new VendorInvoiceLine
            {
                VILineId = 1,
                VendorInvoiceId = 1001,
                LineNumber = 1,
                POLineId = 501,
                PartId = 10,
                UnitCostEur = 104.00m // 4% increase (<= 5% threshold)
            };
            db.VendorInvoiceLine.Add(line);
            await db.SaveChangesAsync();

            // Act
            var result = await varianceService.EvaluateInvoiceVariancesAsync(1001);

            // Assert
            Assert.False(result.HasDiscrepancy);
            Assert.Equal("Approved", result.Status);
            Assert.False(invoice.HasDiscrepancy);
            Assert.Equal("Approved", invoice.Status);
            Assert.Single(result.LineVariances);
            Assert.False(result.LineVariances[0].ExceedsThreshold);
            Assert.Equal(4.0m, result.LineVariances[0].VariancePercentage);
        }

        [Fact]
        public async Task EvaluateInvoiceVariances_Exceeding5Percent_TriggersDiscrepancyAndPendingReviewStatus()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(EvaluateInvoiceVariances_Exceeding5Percent_TriggersDiscrepancyAndPendingReviewStatus));
            var qbService = new QBSyncQueueService(db);
            var varianceService = new VendorInvoiceVarianceService(db, qbService);

            db.PurchaseOrder.Add(new PurchaseOrder { PurchaseOrderId = 200, VendorId = 5 });
            db.PurchaseOrderLine.Add(new PurchaseOrderLine
            {
                POLineId = 601,
                PurchaseOrderId = 200,
                PartId = 20,
                VendorPartNumber = "HYD-PUMP-01",
                UnitCostEur = 500.00m // Baseline = €500
            });
            await db.SaveChangesAsync();

            var invoice = new VendorInvoice
            {
                VendorInvoiceId = 2001,
                InvoiceNumber = "INV-VARIANCE-01",
                PurchaseOrderId = 200,
                VendorId = 5,
                TotalEur = 540.00m
            };
            db.VendorInvoice.Add(invoice);

            var line = new VendorInvoiceLine
            {
                VILineId = 10,
                VendorInvoiceId = 2001,
                LineNumber = 1,
                POLineId = 601,
                PartId = 20,
                VendorPartNumber = "HYD-PUMP-01",
                UnitCostEur = 540.00m // 8% increase (> 5% threshold!)
            };
            db.VendorInvoiceLine.Add(line);
            await db.SaveChangesAsync();

            // Act
            var result = await varianceService.EvaluateInvoiceVariancesAsync(2001);

            // Assert
            Assert.True(result.HasDiscrepancy);
            Assert.Equal("Pending Review", result.Status);
            Assert.True(invoice.HasDiscrepancy);
            Assert.Equal("Pending Review", invoice.Status);
            Assert.Contains("exceeds 5% threshold", invoice.DiscrepancyNotes);
            Assert.Single(result.LineVariances);
            Assert.True(result.LineVariances[0].ExceedsThreshold);
            Assert.Equal(8.0m, result.LineVariances[0].VariancePercentage);
        }

        [Fact]
        public async Task EvaluateInvoiceVariances_EvaluatesAgainstPartMasterCost_WhenNoPOLinePresent()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(EvaluateInvoiceVariances_EvaluatesAgainstPartMasterCost_WhenNoPOLinePresent));
            var qbService = new QBSyncQueueService(db);
            var varianceService = new VendorInvoiceVarianceService(db, qbService);

            db.Part.Add(new Part
            {
                PartId = 30,
                PartNumber = "P-GEAR-30",
                Description = "Steel Gear",
                UnitOfMeasure = "Ea",
                LastVendorCostEur = 200.00m // Baseline = €200
            });
            await db.SaveChangesAsync();

            var invoice = new VendorInvoice
            {
                VendorInvoiceId = 3001,
                InvoiceNumber = "INV-PART-01",
                TotalEur = 230.00m
            };
            db.VendorInvoice.Add(invoice);

            var line = new VendorInvoiceLine
            {
                VILineId = 20,
                VendorInvoiceId = 3001,
                LineNumber = 1,
                PartId = 30,
                UnitCostEur = 230.00m // 15% increase above Part master last cost (€200)
            };
            db.VendorInvoiceLine.Add(line);
            await db.SaveChangesAsync();

            // Act
            var result = await varianceService.EvaluateInvoiceVariancesAsync(3001);

            // Assert
            Assert.True(result.HasDiscrepancy);
            Assert.Equal("Pending Review", result.Status);
            Assert.Equal("PartMaster", result.LineVariances[0].BaselineSource);
            Assert.Equal(200.00m, result.LineVariances[0].BaselineUnitCostEur);
            Assert.Equal(15.0m, result.LineVariances[0].VariancePercentage);
        }

        [Fact]
        public async Task PendingReviewInvoice_BlocksQuickBooksSyncEnqueue()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(PendingReviewInvoice_BlocksQuickBooksSyncEnqueue));
            var qbService = new QBSyncQueueService(db);

            var pendingInvoice = new VendorInvoice
            {
                VendorInvoiceId = 4001,
                InvoiceNumber = "INV-BLOCKED-01",
                Status = "Pending Review",
                HasDiscrepancy = true,
                DiscrepancyNotes = "Unit cost exceeds 5% threshold."
            };
            db.VendorInvoice.Add(pendingInvoice);
            await db.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                qbService.EnqueueSyncItemAsync("VendorInvoice", 4001, "Create", "ToQB"));

            Assert.Contains("Pending Review", ex.Message);
        }

        [Fact]
        public async Task ApproveReviewAsync_ClearsGate_UpdatesPartMasterCost_AndEnqueuesQuickBooksSync()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(ApproveReviewAsync_ClearsGate_UpdatesPartMasterCost_AndEnqueuesQuickBooksSync));
            var qbService = new QBSyncQueueService(db);
            var varianceService = new VendorInvoiceVarianceService(db, qbService);

            var part = new Part
            {
                PartId = 50,
                PartNumber = "P-BEARING-50",
                Description = "Roller Bearing",
                UnitOfMeasure = "Ea",
                LastVendorCostEur = 100.00m
            };
            db.Part.Add(part);

            var invoice = new VendorInvoice
            {
                VendorInvoiceId = 5001,
                InvoiceNumber = "INV-APPROVED-01",
                Status = "Pending Review",
                HasDiscrepancy = true,
                DiscrepancyNotes = "Cost shift variance detected (+10%)"
            };
            db.VendorInvoice.Add(invoice);

            var line = new VendorInvoiceLine
            {
                VILineId = 50,
                VendorInvoiceId = 5001,
                LineNumber = 1,
                PartId = 50,
                UnitCostEur = 110.00m // Approved unit cost
            };
            db.VendorInvoiceLine.Add(line);
            await db.SaveChangesAsync();

            // Act: Authorized manager approves cost shift
            await varianceService.ApproveReviewAsync(
                vendorInvoiceId: 5001,
                reviewedByUserId: 99,
                reviewNotes: "Price increase verified due to raw material surcharge.",
                updatePartMasterCosts: true);

            // Assert
            var updatedInvoice = await db.VendorInvoice.FindAsync(5001);
            Assert.NotNull(updatedInvoice);
            Assert.Equal("Approved", updatedInvoice.Status);
            Assert.Equal(99, updatedInvoice.ReviewedByUserId);
            Assert.Equal("Price increase verified due to raw material surcharge.", updatedInvoice.ReviewNotes);
            Assert.NotNull(updatedInvoice.ReviewedDate);

            // Verify Part Master LastVendorCostEur updated to €110.00
            var updatedPart = await db.Part.FindAsync(50);
            Assert.NotNull(updatedPart);
            Assert.Equal(110.00m, updatedPart.LastVendorCostEur);

            // Verify item enqueued in QBSyncQueue
            var syncItem = await db.QBSyncQueue.FirstOrDefaultAsync(q => q.EntityId == 5001 && q.EntityType == "VendorInvoice");
            Assert.NotNull(syncItem);
            Assert.Equal("Pending", syncItem.Status);
        }

        [Fact]
        public async Task VendorInvoiceCostIncrease_DoesNotAlterCustomerOrderLinePricing()
        {
            // Arrange
            var db = GetInMemoryDbContext(nameof(VendorInvoiceCostIncrease_DoesNotAlterCustomerOrderLinePricing));
            var qbService = new QBSyncQueueService(db);
            var varianceService = new VendorInvoiceVarianceService(db, qbService);

            db.Part.Add(new Part { PartId = 60, PartNumber = "P-MOTOR-60", Description = "Drive Motor", UnitOfMeasure = "Ea", LastVendorCostEur = 1000m });

            db.CustomerOrder.Add(new CustomerOrder { OrderId = 10, OrderNumber = "ORD-TEST-10", Status = "Confirmed", TotalAmount = 2500m });
            var orderLine = new CustomerOrderLine
            {
                OrderLineId = 100,
                OrderId = 10,
                PartId = 60,
                UnitPriceUsd = 2500m,
                Quantity = 1
            };
            db.CustomerOrderLine.Add(orderLine);

            var invoice = new VendorInvoice { VendorInvoiceId = 6001, InvoiceNumber = "INV-COST-SHIFT", Status = "Pending Review", HasDiscrepancy = true };
            db.VendorInvoice.Add(invoice);
            db.VendorInvoiceLine.Add(new VendorInvoiceLine { VILineId = 60, VendorInvoiceId = 6001, LineNumber = 1, PartId = 60, UnitCostEur = 1200m });
            await db.SaveChangesAsync();

            // Act: Approve vendor invoice cost shift
            await varianceService.ApproveReviewAsync(6001, 1, "Cost increase accepted for vendor invoice.");

            // Assert: Customer Order pricing MUST NOT automatically increase
            var refreshedOrderLine = await db.CustomerOrderLine.FindAsync(100);
            Assert.NotNull(refreshedOrderLine);
            Assert.Equal(2500m, refreshedOrderLine.UnitPriceUsd);

            var refreshedOrder = await db.CustomerOrder.FindAsync(10);
            Assert.NotNull(refreshedOrder);
            Assert.Equal(2500m, refreshedOrder.TotalAmount);
        }
    }
}
