using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class PartMergeServiceTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task ExecuteMergeAsync_TransfersStockAndUpdatesPartStatuses()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var survivorPart = new Part
                {
                    PartId = 1,
                    PartNumber = "PART-MASTER",
                    Description = "Master Part",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 10m,
                    ReservedQuantity = 2m,
                    AvailableQuantity = "8",
                    IsActive = true,
                    IsMerged = false
                };

                var duplicatePart = new Part
                {
                    PartId = 2,
                    PartNumber = "PART-DUP",
                    Description = "Duplicate Part",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 15m,
                    ReservedQuantity = 3m,
                    AvailableQuantity = "12",
                    IsActive = true,
                    IsMerged = false
                };

                context.Part.AddRange(survivorPart, duplicatePart);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);
                var result = await service.ExecuteMergeAsync(
                    survivorPartId: 1, 
                    mergedPartId: 2, 
                    mergedByUserName: "testuser", 
                    notes: "Merging duplicate PART-DUP into PART-MASTER",
                    mergedByUserId: 42,
                    mergeDetails: "Custom merge details"
                );

                Assert.True(result.Success);
                Assert.NotNull(result.MergeHistory);
                Assert.Equal(1, result.MergeHistory.SurvivorPartId);
                Assert.Equal(2, result.MergeHistory.MergedPartId);
                Assert.Equal("testuser", result.MergeHistory.MergedByUserName);
                Assert.Equal(42, result.MergeHistory.MergedByUserId);
                Assert.Equal("Merging duplicate PART-DUP into PART-MASTER", result.MergeHistory.Notes);
            }

            // Assert
            using (var context = new ApplicationDbContext(options))
            {
                var survivor = await context.Part.FirstAsync(p => p.PartId == 1);
                var duplicate = await context.Part.FirstAsync(p => p.PartId == 2);
                var history = await context.PartMergeHistory.FirstOrDefaultAsync(h => h.SurvivorPartId == 1 && h.MergedPartId == 2);

                // Stock transfer check
                Assert.Equal(25m, survivor.QuantityOnHand); // 10 + 15
                Assert.Equal(5m, survivor.ReservedQuantity); // 2 + 3
                Assert.Equal("20", survivor.AvailableQuantity); // 25 - 5

                // Duplicate status check
                Assert.True(duplicate.IsMerged);
                Assert.False(duplicate.IsActive);
                Assert.Equal(1, duplicate.MergedIntoPartId);
                Assert.NotNull(duplicate.MergedDate);
                Assert.Equal("testuser", duplicate.MergedBy);
                Assert.Equal(0m, duplicate.QuantityOnHand);
                Assert.Equal(0m, duplicate.ReservedQuantity);
                Assert.Equal("0", duplicate.AvailableQuantity);

                // History record check
                Assert.NotNull(history);
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_UpdatesForeignKeysAcrossAll11ChildTables()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var survivor = new Part { PartId = 10, PartNumber = "SURVIVOR", Description = "Survivor", UnitOfMeasure = "Ea" };
                var duplicate = new Part { PartId = 20, PartNumber = "DUP", Description = "Duplicate", UnitOfMeasure = "Ea" };

                context.Part.AddRange(survivor, duplicate);

                // 1. CustomerOrderLine
                context.CustomerOrderLine.Add(new CustomerOrderLine { OrderLineId = 1, OrderId = 100, LineNumber = 1, PartId = 20, Quantity = 5, UnitPriceUsd = 10 });
                // 2. PurchaseOrderLine
                context.PurchaseOrderLine.Add(new PurchaseOrderLine { POLineId = 1, PurchaseOrderId = 200, LineNumber = 1, PartId = 20, Quantity = 10 });
                // 3. InventoryReservation
                context.InventoryReservation.Add(new InventoryReservation { ReservationId = 1, PartId = 20, QuantityReserved = 2 });
                // 4. PartRideApplication
                context.PartRideApplication.Add(new PartRideApplication { PartRideAppId = 1, PartId = 20, RideManufacturer = "FunRides", RideModel = "Coaster1" });
                // 5. ReceivingLine
                context.ReceivingLine.Add(new ReceivingLine { ReceivingLineId = 1, ReceivingId = 300, PartId = 20, QuantityReceived = 5 });
                // 6. ShipmentLine
                context.ShipmentLine.Add(new ShipmentLine { ShipmentLineId = 1, ShipmentId = 400, PartId = 20, QuantityShipped = 3 });
                // 7. VendorSource
                context.VendorSource.Add(new VendorSource { VendorSourceId = 1, VendorId = 1, PartId = 20 });
                // 8. VendorPartCrossReference
                context.VendorPartCrossReference.Add(new VendorPartCrossReference { CrossRefId = 1, VendorId = 1, PartId = 20, VendorPartNumber = "VPN-20" });
                // 9. VendorInvoiceLine
                context.VendorInvoiceLine.Add(new VendorInvoiceLine { VILineId = 1, VendorInvoiceId = 500, PartId = 20, Quantity = 1 });
                // 10. CustomerInvoiceLine
                context.CustomerInvoiceLine.Add(new CustomerInvoiceLine { InvoiceLineId = 1, InvoiceId = 600, PartId = 20, Quantity = 1 });
                // 11. PurchasingQueueItem
                context.PurchasingQueueItem.Add(new PurchasingQueueItem { QueueItemId = 1, PartId = 20, QuantityRequired = 10, SourceType = "Manual", Status = "Pending" });

                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);
                var result = await service.ExecuteMergeAsync(10, 20, "admin");
                Assert.True(result.Success);
            }

            // Assert: All 11 child tables updated PartId from 20 to 10
            using (var context = new ApplicationDbContext(options))
            {
                Assert.All(await context.CustomerOrderLine.ToListAsync(), col => Assert.Equal(10, col.PartId));
                Assert.All(await context.PurchaseOrderLine.ToListAsync(), pol => Assert.Equal(10, pol.PartId));
                Assert.All(await context.InventoryReservation.ToListAsync(), ir => Assert.Equal(10, ir.PartId));
                Assert.All(await context.PartRideApplication.ToListAsync(), pra => Assert.Equal(10, pra.PartId));
                Assert.All(await context.ReceivingLine.ToListAsync(), rl => Assert.Equal(10, rl.PartId));
                Assert.All(await context.ShipmentLine.ToListAsync(), sl => Assert.Equal(10, sl.PartId));
                Assert.All(await context.VendorSource.ToListAsync(), vs => Assert.Equal(10, vs.PartId));
                Assert.All(await context.VendorPartCrossReference.ToListAsync(), vxr => Assert.Equal(10, vxr.PartId));
                Assert.All(await context.VendorInvoiceLine.ToListAsync(), vil => Assert.Equal(10, vil.PartId));
                Assert.All(await context.CustomerInvoiceLine.ToListAsync(), cil => Assert.Equal(10, cil.PartId));
                Assert.All(await context.PurchasingQueueItem.ToListAsync(), pqi => Assert.Equal(10, pqi.PartId));
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_ReturnsError_WhenSameSurvivorAndMergedId()
        {
            var options = GetDbContextOptions();
            using var context = new ApplicationDbContext(options);
            var service = new PartMergeService(context);

            var result = await service.ExecuteMergeAsync(1, 1, "admin");

            Assert.False(result.Success);
            Assert.Contains("cannot be the same", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteMergeAsync_ReturnsError_WhenDuplicatePartAlreadyMerged()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.Add(new Part { PartId = 1, PartNumber = "SURVIVOR", Description = "Survivor", UnitOfMeasure = "Ea" });
                context.Part.Add(new Part { PartId = 2, PartNumber = "ALREADY-MERGED", Description = "Merged", UnitOfMeasure = "Ea", IsMerged = true, MergedIntoPartId = 99 });
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);
                var result = await service.ExecuteMergeAsync(1, 2, "admin");

                Assert.False(result.Success);
                Assert.Contains("already been merged", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_ReturnsError_WhenPartDoesNotExist()
        {
            var options = GetDbContextOptions();
            using var context = new ApplicationDbContext(options);
            var service = new PartMergeService(context);

            var result = await service.ExecuteMergeAsync(1, 999, "admin");

            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task IndexModel_ExcludesMergedPartsFromList()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.Add(new Part { PartId = 1, PartNumber = "ACTIVE-1", Description = "Active Part 1", UnitOfMeasure = "Ea", IsMerged = false });
                context.Part.Add(new Part { PartId = 2, PartNumber = "MERGED-1", Description = "Merged Part 1", UnitOfMeasure = "Ea", IsMerged = true, MergedIntoPartId = 1 });
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var indexModel = new Pages.Part.IndexModel(context);
                await indexModel.OnGetAsync();

                Assert.Single(indexModel.Items);
                Assert.Equal(1, indexModel.Items.First().PartId);
                Assert.Equal("ACTIVE-1", indexModel.Items.First().PartNumber);
            }
        }

        private class ThrowingDbContext : ApplicationDbContext
        {
            public ThrowingDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Simulated database exception");
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_HandlesExceptionAndRollsBack()
        {
            var options = GetDbContextOptions();
            using (var setupContext = new ApplicationDbContext(options))
            {
                setupContext.Part.Add(new Part { PartId = 1, PartNumber = "P1", Description = "P1", UnitOfMeasure = "Ea" });
                setupContext.Part.Add(new Part { PartId = 2, PartNumber = "P2", Description = "P2", UnitOfMeasure = "Ea" });
                await setupContext.SaveChangesAsync();
            }

            using (var throwingContext = new ThrowingDbContext(options))
            {
                var service = new PartMergeService(throwingContext);
                var result = await service.ExecuteMergeAsync(1, 2, "admin");

                Assert.False(result.Success);
                Assert.Contains("Simulated database exception", result.ErrorMessage);
            }
        }
    }
}
