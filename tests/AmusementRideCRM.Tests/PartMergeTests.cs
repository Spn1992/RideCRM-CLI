using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class PartMergeTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task ExecuteMergeAsync_TransfersStockAndUpdatesDuplicatePart()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var masterPart = new Part
                {
                    PartId = 1,
                    PartNumber = "MASTER-001",
                    Description = "Master Gear",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 10m,
                    ReservedQuantity = 2m,
                    AvailableQuantity = 8m,
                    IsActive = true,
                    IsMerged = false
                };

                var duplicatePart = new Part
                {
                    PartId = 2,
                    PartNumber = "DUP-001",
                    Description = "Duplicate Gear",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 15m,
                    ReservedQuantity = 5m,
                    AvailableQuantity = 10m,
                    IsActive = true,
                    IsMerged = false
                };

                context.Part.AddRange(masterPart, duplicatePart);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);
                await service.ExecuteMergeAsync(1, 2, userName: "AdminUser", notes: "Merging duplicate into master");
            }

            // Assert
            using (var context = new ApplicationDbContext(options))
            {
                var master = await context.Part.FirstAsync(p => p.PartId == 1);
                var duplicate = await context.Part.FirstAsync(p => p.PartId == 2);

                // Master part stock updated
                Assert.Equal(25m, master.QuantityOnHand);
                Assert.Equal(7m, master.ReservedQuantity);
                Assert.Equal(18m, master.AvailableQuantity);
                Assert.Equal("AdminUser", master.ModifiedBy);

                // Duplicate part updated
                Assert.Equal(0m, duplicate.QuantityOnHand);
                Assert.Equal(0m, duplicate.ReservedQuantity);
                Assert.Equal(0m, duplicate.AvailableQuantity);
                Assert.True(duplicate.IsMerged);
                Assert.False(duplicate.IsActive);
                Assert.Equal(1, duplicate.MergedIntoPartId);
                Assert.NotNull(duplicate.MergedDate);
                Assert.Equal("AdminUser", duplicate.MergedBy);
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_UpdatesForeignKeysAcrossAll11ChildTables()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var masterPart = new Part { PartId = 1, PartNumber = "MASTER-100", Description = "Master Part", UnitOfMeasure = "Ea" };
                var duplicatePart = new Part { PartId = 2, PartNumber = "DUP-100", Description = "Duplicate Part", UnitOfMeasure = "Ea" };

                context.Part.AddRange(masterPart, duplicatePart);

                // Seed 11 child table entities pointing to duplicatePart (PartId = 2)
                context.CustomerOrderLine.Add(new CustomerOrderLine { OrderLineId = 1, OrderId = 1, LineNumber = 1, PartId = 2, Quantity = 1 });
                context.PurchaseOrderLine.Add(new PurchaseOrderLine { POLineId = 1, PurchaseOrderId = 1, LineNumber = 1, PartId = 2, Quantity = 1 });
                context.InventoryReservation.Add(new InventoryReservation { ReservationId = 1, PartId = 2, QuantityReserved = 1 });
                context.PartRideApplication.Add(new PartRideApplication { PartRideAppId = 1, PartId = 2, RideManufacturer = "Vekoma", RideModel = "Coaster" });
                context.ReceivingLine.Add(new ReceivingLine { ReceivingLineId = 1, ReceivingId = 1, POLineId = 1, PartId = 2, QuantityExpected = 1, QuantityReceived = 1 });
                context.ShipmentLine.Add(new ShipmentLine { ShipmentLineId = 1, ShipmentId = 1, OrderLineId = 1, PartId = 2, QuantityShipped = 1 });
                context.VendorSource.Add(new VendorSource { VendorSourceId = 1, VendorId = 1, PartId = 2, IsPreferred = true });
                context.VendorPartCrossReference.Add(new VendorPartCrossReference { CrossRefId = 1, VendorId = 1, PartId = 2, VendorPartNumber = "V-001" });
                context.VendorInvoiceLine.Add(new VendorInvoiceLine { VILineId = 1, VendorInvoiceId = 1, LineNumber = 1, PartId = 2, Quantity = 1, UnitCostEur = 10 });
                context.CustomerInvoiceLine.Add(new CustomerInvoiceLine { InvoiceLineId = 1, InvoiceId = 1, LineNumber = 1, PartId = 2, Quantity = 1, UnitPrice = 15 });
                context.PurchasingQueueItem.Add(new PurchasingQueueItem { QueueItemId = 1, SuggestedVendorId = 1, PartId = 2, QuantityRequired = 1 });

                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);
                await service.ExecuteMergeAsync(1, 2, "TestUser", "FK Migration Test");
            }

            // Assert
            using (var context = new ApplicationDbContext(options))
            {
                Assert.Equal(1, (await context.CustomerOrderLine.FirstAsync()).PartId);
                Assert.Equal(1, (await context.PurchaseOrderLine.FirstAsync()).PartId);
                Assert.Equal(1, (await context.InventoryReservation.FirstAsync()).PartId);
                Assert.Equal(1, (await context.PartRideApplication.FirstAsync()).PartId);
                Assert.Equal(1, (await context.ReceivingLine.FirstAsync()).PartId);
                Assert.Equal(1, (await context.ShipmentLine.FirstAsync()).PartId);
                Assert.Equal(1, (await context.VendorSource.FirstAsync()).PartId);
                Assert.Equal(1, (await context.VendorPartCrossReference.FirstAsync()).PartId);
                Assert.Equal(1, (await context.VendorInvoiceLine.FirstAsync()).PartId);
                Assert.Equal(1, (await context.CustomerInvoiceLine.FirstAsync()).PartId);
                Assert.Equal(1, (await context.PurchasingQueueItem.FirstAsync()).PartId);
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_RecordsPartMergeHistory()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.AddRange(
                    new Part { PartId = 10, PartNumber = "P-10", Description = "Part 10", UnitOfMeasure = "Ea" },
                    new Part { PartId = 20, PartNumber = "P-20", Description = "Part 20", UnitOfMeasure = "Ea" }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);
                await service.ExecuteMergeAsync(10, 20, userName: "JohnDoe", notes: "Merging duplicate record", userId: 101);
            }

            // Assert
            using (var context = new ApplicationDbContext(options))
            {
                var history = await context.PartMergeHistory.FirstOrDefaultAsync();
                Assert.NotNull(history);
                Assert.Equal(10, history.SurvivorPartId);
                Assert.Equal(20, history.MergedPartId);
                Assert.Equal("JohnDoe", history.MergedByUserName);
                Assert.Equal(101, history.MergedByUserId);
                Assert.Equal("Merging duplicate record", history.Notes);
                Assert.Contains("P-20", history.MergeDetails);
                Assert.Contains("P-10", history.MergeDetails);
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_ValidationErrors_ThrowsExpectedExceptions()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.AddRange(
                    new Part { PartId = 1, PartNumber = "P-1", Description = "Part 1", UnitOfMeasure = "Ea", IsMerged = false },
                    new Part { PartId = 2, PartNumber = "P-2", Description = "Part 2", UnitOfMeasure = "Ea", IsMerged = true }
                );
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);

                // 1. Same survivor and merged part ID
                await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteMergeAsync(1, 1));

                // 2. Duplicate part already merged
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteMergeAsync(1, 2));

                // 3. Survivor or merged part not found
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ExecuteMergeAsync(999, 1));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ExecuteMergeAsync(1, 888));
            }
        }

        [Fact]
        public async Task ExecuteMergeAsync_RollsBackOnFailure()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.AddRange(
                    new Part { PartId = 1, PartNumber = "P-1", Description = "Part 1", UnitOfMeasure = "Ea", QuantityOnHand = 10m },
                    new Part { PartId = 2, PartNumber = "P-2", Description = "Part 2", UnitOfMeasure = "Ea", QuantityOnHand = 5m, IsMerged = true }
                );
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var service = new PartMergeService(context);

                // Attempting to merge an already merged part throws exception and leaves stock unchanged
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteMergeAsync(1, 2));
            }

            using (var context = new ApplicationDbContext(options))
            {
                var survivor = await context.Part.FirstAsync(p => p.PartId == 1);
                Assert.Equal(10m, survivor.QuantityOnHand);
            }
        }

        [Fact]
        public async Task PartIndex_ExcludesMergedParts()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.Part.AddRange(
                    new Part { PartId = 1, PartNumber = "ACTIVE-1", Description = "Active Part", UnitOfMeasure = "Ea", IsMerged = false },
                    new Part { PartId = 2, PartNumber = "MERGED-2", Description = "Merged Part", UnitOfMeasure = "Ea", IsMerged = true }
                );
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var indexModel = new Pages.Part.IndexModel(context);
                await indexModel.OnGetAsync();

                Assert.Single(indexModel.Items);
                Assert.Equal(1, indexModel.Items[0].PartId);
                Assert.Equal("ACTIVE-1", indexModel.Items[0].PartNumber);
            }
        }
    }
}
