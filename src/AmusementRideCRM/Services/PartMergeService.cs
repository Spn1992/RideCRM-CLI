using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task ExecuteMergeAsync(
            int survivorPartId,
            int mergedPartId,
            string? userName = null,
            string? notes = null,
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            if (survivorPartId == mergedPartId)
            {
                throw new ArgumentException("Survivor part ID cannot be the same as merged part ID.");
            }

            var survivorPart = await _context.Part
                .FirstOrDefaultAsync(p => p.PartId == survivorPartId, cancellationToken);
            if (survivorPart == null)
            {
                throw new KeyNotFoundException($"Survivor part with ID {survivorPartId} was not found.");
            }

            var duplicatePart = await _context.Part
                .FirstOrDefaultAsync(p => p.PartId == mergedPartId, cancellationToken);
            if (duplicatePart == null)
            {
                throw new KeyNotFoundException($"Merged part with ID {mergedPartId} was not found.");
            }

            if (duplicatePart.IsMerged)
            {
                throw new InvalidOperationException($"Part {mergedPartId} ({duplicatePart.PartNumber}) has already been merged.");
            }

            if (survivorPart.IsMerged)
            {
                throw new InvalidOperationException($"Survivor part {survivorPartId} ({survivorPart.PartNumber}) is already merged and cannot be a survivor part.");
            }

            using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                // 1. Transfer Stock Quantities and Recalculate Available
                survivorPart.QuantityOnHand += duplicatePart.QuantityOnHand;
                survivorPart.ReservedQuantity += duplicatePart.ReservedQuantity;
                survivorPart.ModifiedDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    survivorPart.ModifiedBy = userName;
                }

                duplicatePart.QuantityOnHand = 0m;
                duplicatePart.ReservedQuantity = 0m;
                duplicatePart.IsMerged = true;
                duplicatePart.IsActive = false;
                duplicatePart.MergedIntoPartId = survivorPartId;
                duplicatePart.MergedDate = DateTime.UtcNow;
                duplicatePart.MergedBy = userName;
                duplicatePart.ModifiedDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    duplicatePart.ModifiedBy = userName;
                }

                // 2. Update Foreign Key PartId across all child tables

                // 2.1 CustomerOrderLine
                var customerOrderLines = await _context.CustomerOrderLine
                    .Where(l => l.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var line in customerOrderLines)
                {
                    line.PartId = survivorPartId;
                }

                // 2.2 PurchaseOrderLine
                var purchaseOrderLines = await _context.PurchaseOrderLine
                    .Where(l => l.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var line in purchaseOrderLines)
                {
                    line.PartId = survivorPartId;
                }

                // 2.3 InventoryReservation
                var reservations = await _context.InventoryReservation
                    .Where(r => r.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var res in reservations)
                {
                    res.PartId = survivorPartId;
                }

                // 2.4 PartRideApplication
                var rideApps = await _context.PartRideApplication
                    .Where(a => a.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var app in rideApps)
                {
                    app.PartId = survivorPartId;
                }

                // 2.5 ReceivingLine
                var receivingLines = await _context.ReceivingLine
                    .Where(l => l.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var line in receivingLines)
                {
                    line.PartId = survivorPartId;
                }

                // 2.6 ShipmentLine
                var shipmentLines = await _context.ShipmentLine
                    .Where(l => l.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var line in shipmentLines)
                {
                    line.PartId = survivorPartId;
                }

                // 2.7 VendorSource
                var vendorSources = await _context.VendorSource
                    .Where(v => v.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var vs in vendorSources)
                {
                    vs.PartId = survivorPartId;
                }

                // 2.8 VendorPartCrossReference
                var xrefs = await _context.VendorPartCrossReference
                    .Where(x => x.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var xref in xrefs)
                {
                    xref.PartId = survivorPartId;
                }

                // 2.9 VendorInvoiceLine
                var vendorInvoiceLines = await _context.VendorInvoiceLine
                    .Where(l => l.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var line in vendorInvoiceLines)
                {
                    line.PartId = survivorPartId;
                }

                // 2.10 CustomerInvoiceLine
                var customerInvoiceLines = await _context.CustomerInvoiceLine
                    .Where(l => l.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var line in customerInvoiceLines)
                {
                    line.PartId = survivorPartId;
                }

                // 2.11 PurchasingQueueItem
                var purchasingQueueItems = await _context.PurchasingQueueItem
                    .Where(q => q.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var item in purchasingQueueItems)
                {
                    item.PartId = survivorPartId;
                }

                // Additional entities
                var serializedUnits = await _context.SerializedPartUnit
                    .Where(s => s.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var unit in serializedUnits)
                {
                    unit.PartId = survivorPartId;
                }

                var printJobs = await _context.ZplPrintJob
                    .Where(j => j.PartId == mergedPartId)
                    .ToListAsync(cancellationToken);
                foreach (var job in printJobs)
                {
                    job.PartId = survivorPartId;
                }

                // 3. Record entry in PartMergeHistory
                var history = new PartMergeHistory
                {
                    SurvivorPartId = survivorPartId,
                    MergedPartId = mergedPartId,
                    MergedDate = DateTime.UtcNow,
                    MergedByUserId = userId,
                    MergedByUserName = userName,
                    Notes = notes,
                    MergeDetails = $"Merged duplicate part #{mergedPartId} ({duplicatePart.PartNumber}) into master part #{survivorPartId} ({survivorPart.PartNumber})."
                };
                _context.PartMergeHistory.Add(history);

                // Save all changes atomically
                await _context.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                throw;
            }
        }
    }
}
