using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class PurchasingQueueAggregationTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task ConsolidateQueueItems_GroupsByVendorAndRideModel_CreatesDraftPOsInEUR()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.VendorMaster.AddRange(
                    new VendorMaster { VendorId = 101, VendorName = "Vekoma Manufacturing", Currency = "EUR", IsActive = true },
                    new VendorMaster { VendorId = 102, VendorName = "B&M Components", Currency = "EUR", IsActive = true }
                );

                context.Part.AddRange(
                    new Part { PartId = 1, PartNumber = "BEARING-100", Description = "Main Axle Bearing", LastVendorCostEur = 10.00m },
                    new Part { PartId = 2, PartNumber = "BOLT-200", Description = "High Tensile Bolt", LastVendorCostEur = 25.00m }
                );

                context.PurchasingQueueItem.AddRange(
                    new PurchasingQueueItem
                    {
                        QueueItemId = 1,
                        PartId = 1,
                        SuggestedVendorId = 101,
                        QuantityRequired = 5m,
                        SuggestedCostEur = 10.00m,
                        RideManufacturer = "Vekoma",
                        RideModel = "CoasterX",
                        SerialNumber = "SN-001",
                        SourceOrderLineId = 1001,
                        Status = "Pending"
                    },
                    new PurchasingQueueItem
                    {
                        QueueItemId = 2,
                        PartId = 1,
                        SuggestedVendorId = 101,
                        QuantityRequired = 3m,
                        SuggestedCostEur = 10.00m,
                        RideManufacturer = "Vekoma",
                        RideModel = "CoasterX",
                        SerialNumber = "SN-002",
                        SourceOrderLineId = 1002,
                        Status = "Pending"
                    },
                    new PurchasingQueueItem
                    {
                        QueueItemId = 3,
                        PartId = 2,
                        SuggestedVendorId = 101,
                        QuantityRequired = 2m,
                        SuggestedCostEur = 25.00m,
                        RideManufacturer = "Vekoma",
                        RideModel = "CoasterY",
                        SerialNumber = "SN-003",
                        SourceOrderLineId = 1003,
                        Status = "Pending"
                    },
                    new PurchasingQueueItem
                    {
                        QueueItemId = 4,
                        PartId = 1,
                        SuggestedVendorId = 102,
                        QuantityRequired = 4m,
                        SuggestedCostEur = 12.00m,
                        RideManufacturer = "B&M",
                        RideModel = "HyperCoaster",
                        SerialNumber = "SN-101",
                        SourceOrderLineId = 2001,
                        Status = "Pending"
                    }
                );

                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PurchasingQueueAggregationService(context);
                var result = await service.ConsolidateQueueItemsAsync(new[] { 1, 2, 3, 4 }, "BuyerUser");

                // Assert
                Assert.True(result.Success);
                Assert.Equal(4, result.ProcessedItemCount);
                Assert.Equal(2, result.CreatedPurchaseOrderIds.Count);

                // Verify Vendor 101 Draft PO
                var po101 = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.VendorId == 101);
                Assert.NotNull(po101);
                Assert.Equal("Draft", po101.Status);
                Assert.Equal("EUR", po101.Currency);
                Assert.Equal(130.00m, po101.SubTotalEur); // (8*10) + (2*25) = 80 + 50 = 130
                Assert.Equal("130.00", po101.TotalEur);

                var po101Lines = await context.PurchaseOrderLine
                    .Where(l => l.PurchaseOrderId == po101.PurchaseOrderId)
                    .OrderBy(l => l.LineNumber)
                    .ToListAsync();

                Assert.Equal(2, po101Lines.Count);

                // Line 1: Part 1, CoasterX (aggregated Qty 8)
                var line1 = po101Lines.First(l => l.PartId == 1);
                Assert.Equal(8m, line1.Quantity);
                Assert.Equal(10.00m, line1.UnitCostEur);
                Assert.Equal("80.00", line1.LineTotalEur);
                Assert.Equal("Vekoma", line1.RideManufacturer);
                Assert.Equal("CoasterX", line1.RideModel);
                Assert.Contains("SN-001", line1.SerialNumber);
                Assert.Contains("SN-002", line1.SerialNumber);
                Assert.Equal(1001, line1.SourceOrderLineId);

                // Line 2: Part 2, CoasterY
                var line2 = po101Lines.First(l => l.PartId == 2);
                Assert.Equal(2m, line2.Quantity);
                Assert.Equal(25.00m, line2.UnitCostEur);
                Assert.Equal("50.00", line2.LineTotalEur);

                // Verify Vendor 102 Draft PO
                var po102 = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.VendorId == 102);
                Assert.NotNull(po102);
                Assert.Equal(48.00m, po102.SubTotalEur);

                // Verify status of queue items updated
                var queueItems = await context.PurchasingQueueItem.ToListAsync();
                Assert.All(queueItems, q => Assert.Equal("Consolidated", q.Status));
                Assert.All(queueItems, q => Assert.NotNull(q.PurchaseOrderLineId));
            }
        }

        [Fact]
        public async Task ConsolidateQueueItems_ExcludesConsolidatedAndOrderedItems()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.VendorMaster.Add(new VendorMaster { VendorId = 10, VendorName = "Vendor Ten", Currency = "EUR", IsActive = true });
                context.Part.Add(new Part { PartId = 5, PartNumber = "P-5", LastVendorCostEur = 15m });

                context.PurchasingQueueItem.AddRange(
                    new PurchasingQueueItem { QueueItemId = 10, PartId = 5, SuggestedVendorId = 10, QuantityRequired = 2m, Status = "Consolidated" },
                    new PurchasingQueueItem { QueueItemId = 11, PartId = 5, SuggestedVendorId = 10, QuantityRequired = 3m, Status = "Ordered" },
                    new PurchasingQueueItem { QueueItemId = 12, PartId = 5, SuggestedVendorId = 10, QuantityRequired = 4m, Status = "Pending" }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PurchasingQueueAggregationService(context);
                var result = await service.ConsolidateQueueItemsAsync(new[] { 10, 11, 12 });

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, result.ProcessedItemCount); // Only QueueItemId 12 is eligible

                var po = await context.PurchaseOrder.FirstOrDefaultAsync();
                Assert.NotNull(po);
                Assert.Equal(60.00m, po.SubTotalEur); // 4 * 15.00
            }
        }

        [Fact]
        public async Task ConsolidateQueueItems_MissingVendorAssignment_FailsValidation()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.Add(new Part { PartId = 1, PartNumber = "P-1" });
                context.PurchasingQueueItem.Add(new PurchasingQueueItem
                {
                    QueueItemId = 20,
                    PartId = 1,
                    SuggestedVendorId = null, // Missing vendor
                    QuantityRequired = 10m,
                    Status = "Pending"
                });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PurchasingQueueAggregationService(context);
                var result = await service.ConsolidateQueueItemsAsync(new[] { 20 });

                // Assert
                Assert.False(result.Success);
                Assert.NotEmpty(result.Errors);
                Assert.Contains(result.Warnings, w => w.Code == "MISSING_VENDOR");
                Assert.Empty(await context.PurchaseOrder.ToListAsync());
            }
        }

        [Fact]
        public async Task ConsolidateQueueItems_NonEURVendorCurrency_GeneratesWarningAndCreatesPOInEUR()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.VendorMaster.Add(new VendorMaster { VendorId = 30, VendorName = "US Supplier Inc", Currency = "USD", IsActive = true });
                context.Part.Add(new Part { PartId = 3, PartNumber = "P-3", LastVendorCostEur = 50.00m });
                context.PurchasingQueueItem.Add(new PurchasingQueueItem
                {
                    QueueItemId = 30,
                    PartId = 3,
                    SuggestedVendorId = 30,
                    QuantityRequired = 2m,
                    SuggestedCostEur = 50.00m,
                    Status = "Pending"
                });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PurchasingQueueAggregationService(context);
                var result = await service.ConsolidateQueueItemsAsync(new[] { 30 });

                // Assert
                Assert.True(result.Success);
                Assert.Contains(result.Warnings, w => w.Code == "CURRENCY_MISMATCH");

                var po = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.VendorId == 30);
                Assert.NotNull(po);
                Assert.Equal("EUR", po.Currency);
                Assert.Equal(100.00m, po.SubTotalEur);
            }
        }

        [Fact]
        public async Task ConsolidateQueueItems_FallbackToPartCostIfSuggestedCostMissing()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.VendorMaster.Add(new VendorMaster { VendorId = 40, VendorName = "Parts Co", Currency = "EUR", IsActive = true });
                context.Part.Add(new Part { PartId = 4, PartNumber = "P-4", LastVendorCostEur = 75.00m });
                context.PurchasingQueueItem.Add(new PurchasingQueueItem
                {
                    QueueItemId = 40,
                    PartId = 4,
                    SuggestedVendorId = 40,
                    QuantityRequired = 3m,
                    SuggestedCostEur = null, // Missing suggested cost
                    Status = "Pending"
                });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PurchasingQueueAggregationService(context);
                var result = await service.ConsolidateQueueItemsAsync(new[] { 40 });

                // Assert
                Assert.True(result.Success);
                var poLine = await context.PurchaseOrderLine.FirstOrDefaultAsync();
                Assert.NotNull(poLine);
                Assert.Equal(75.00m, poLine.UnitCostEur);
                Assert.Equal("225.00", poLine.LineTotalEur);
            }
        }

        [Fact]
        public async Task IndexModel_OnPostConsolidateAsync_ProcessesSelectedItemsAndSetsPageFeedback()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.VendorMaster.Add(new VendorMaster { VendorId = 50, VendorName = "Coaster Supplies", Currency = "EUR", IsActive = true });
                context.Part.Add(new Part { PartId = 50, PartNumber = "P-50", Description = "Chain Link", LastVendorCostEur = 5.00m });
                context.PurchasingQueueItem.Add(new PurchasingQueueItem
                {
                    QueueItemId = 500,
                    PartId = 50,
                    SuggestedVendorId = 50,
                    QuantityRequired = 10m,
                    SuggestedCostEur = 5.00m,
                    Status = "Pending"
                });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PurchasingQueueAggregationService(context);
                var pageModel = new Pages.PurchasingQueueItem.IndexModel(context, service);

                var result = await pageModel.OnPostConsolidateAsync(new List<int> { 500 });

                // Assert
                Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
                Assert.NotNull(pageModel.SuccessMessage);
                Assert.Contains("Successfully consolidated 1 queue item(s)", pageModel.SuccessMessage);
                Assert.Single(pageModel.CreatedPOIds);

                var itemInDb = await context.PurchasingQueueItem.FirstAsync(q => q.QueueItemId == 500);
                Assert.Equal("Consolidated", itemInDb.Status);
            }
        }
    }
}
