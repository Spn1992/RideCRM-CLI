using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class PartMergeService : IPartMergeService
    {
        private readonly ApplicationDbContext _context;

        public PartMergeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PartMergeResult> ExecuteMergeAsync(
            int survivorPartId, 
            int mergedPartId, 
            string? mergedByUserName = null, 
            string? notes = null, 
            int? mergedByUserId = null, 
            string? mergeDetails = null)
        {
            if (survivorPartId == mergedPartId)
            {
                return new PartMergeResult
                {
                    Success = false,
                    ErrorMessage = "Survivor part ID and Merged part ID cannot be the same."
                };
            }

            var survivorPart = await _context.Part.FirstOrDefaultAsync(p => p.PartId == survivorPartId);
            if (survivorPart == null)
            {
                return new PartMergeResult
                {
                    Success = false,
                    ErrorMessage = $"Survivor part with ID {survivorPartId} not found."
                };
            }

            if (survivorPart.IsMerged)
            {
                return new PartMergeResult
                {
                    Success = false,
                    ErrorMessage = $"Survivor part with ID {survivorPartId} has already been merged into another part."
                };
            }

            var mergedPart = await _context.Part.FirstOrDefaultAsync(p => p.PartId == mergedPartId);
            if (mergedPart == null)
            {
                return new PartMergeResult
                {
                    Success = false,
                    ErrorMessage = $"Merged part with ID {mergedPartId} not found."
                };
            }

            if (mergedPart.IsMerged)
            {
                return new PartMergeResult
                {
                    Success = false,
                    ErrorMessage = $"Merged part with ID {mergedPartId} has already been merged."
                };
            }

            using var transaction = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;

            try
            {
                // 1. Transfer stock balances
                survivorPart.QuantityOnHand += mergedPart.QuantityOnHand;
                survivorPart.ReservedQuantity += mergedPart.ReservedQuantity;
                survivorPart.AvailableQuantity = (survivorPart.QuantityOnHand - survivorPart.ReservedQuantity).ToString("F0");
                survivorPart.ModifiedDate = DateTime.UtcNow;

                // Mark duplicate as merged
                mergedPart.IsMerged = true;
                mergedPart.IsActive = false;
                mergedPart.MergedIntoPartId = survivorPartId;
                mergedPart.MergedDate = DateTime.UtcNow;
                mergedPart.MergedBy = mergedByUserName;
                mergedPart.QuantityOnHand = 0m;
                mergedPart.ReservedQuantity = 0m;
                mergedPart.AvailableQuantity = "0";
                mergedPart.ModifiedDate = DateTime.UtcNow;

                // 2. Transfer foreign keys across 11 child tables
                var customerOrderLines = await _context.CustomerOrderLine.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var line in customerOrderLines) line.PartId = survivorPartId;

                var purchaseOrderLines = await _context.PurchaseOrderLine.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var line in purchaseOrderLines) line.PartId = survivorPartId;

                var inventoryReservations = await _context.InventoryReservation.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var item in inventoryReservations) item.PartId = survivorPartId;

                var partRideApps = await _context.PartRideApplication.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var item in partRideApps) item.PartId = survivorPartId;

                var receivingLines = await _context.ReceivingLine.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var line in receivingLines) line.PartId = survivorPartId;

                var shipmentLines = await _context.ShipmentLine.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var line in shipmentLines) line.PartId = survivorPartId;

                var vendorSources = await _context.VendorSource.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var source in vendorSources) source.PartId = survivorPartId;

                var vendorPartCrossRefs = await _context.VendorPartCrossReference.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var crossRef in vendorPartCrossRefs) crossRef.PartId = survivorPartId;

                var vendorInvoiceLines = await _context.VendorInvoiceLine.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var line in vendorInvoiceLines) line.PartId = survivorPartId;

                var customerInvoiceLines = await _context.CustomerInvoiceLine.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var line in customerInvoiceLines) line.PartId = survivorPartId;

                var purchasingQueueItems = await _context.PurchasingQueueItem.Where(x => x.PartId == mergedPartId).ToListAsync();
                foreach (var item in purchasingQueueItems) item.PartId = survivorPartId;

                // 3. Record PartMergeHistory entry
                var history = new PartMergeHistory
                {
                    SurvivorPartId = survivorPartId,
                    MergedPartId = mergedPartId,
                    MergedDate = DateTime.UtcNow,
                    MergedByUserId = mergedByUserId,
                    MergedByUserName = mergedByUserName,
                    MergeDetails = mergeDetails ?? $"Merged part {mergedPartId} into part {survivorPartId}",
                    Notes = notes
                };
                _context.PartMergeHistory.Add(history);

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return new PartMergeResult
                {
                    Success = true,
                    MergeHistory = history
                };
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return new PartMergeResult
                {
                    Success = false,
                    ErrorMessage = $"An error occurred while merging parts: {ex.Message}"
                };
            }
        }
    }
}
