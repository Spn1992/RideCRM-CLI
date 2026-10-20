using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public class PurchasingQueueAggregationService : IPurchasingQueueAggregationService
    {
        private readonly ApplicationDbContext _context;

        public PurchasingQueueAggregationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AggregationValidationResult> ValidateForConsolidationAsync(IEnumerable<int> queueItemIds)
        {
            var result = new AggregationValidationResult();

            var query = _context.PurchasingQueueItem.AsQueryable();

            List<int> idList = queueItemIds?.Distinct().ToList() ?? new List<int>();
            if (idList.Any())
            {
                query = query.Where(q => idList.Contains(q.QueueItemId));
            }

            var candidateItems = await query.ToListAsync();

            // Filter out items already Consolidated or Ordered
            var eligibleItems = candidateItems
                .Where(q => string.IsNullOrWhiteSpace(q.Status) ||
                            (!q.Status.Equals("Consolidated", StringComparison.OrdinalIgnoreCase) &&
                             !q.Status.Equals("Ordered", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            result.EligibleItemCount = eligibleItems.Count;

            if (!eligibleItems.Any())
            {
                result.IsValid = false;
                result.Errors.Add("No eligible pending queue items found for consolidation.");
                return result;
            }

            // Check for missing vendor assignments
            foreach (var item in eligibleItems)
            {
                if (!item.SuggestedVendorId.HasValue || item.SuggestedVendorId.Value <= 0)
                {
                    result.IsValid = false;
                    result.Warnings.Add(new QueueValidationWarning
                    {
                        QueueItemId = item.QueueItemId,
                        Code = "MISSING_VENDOR",
                        Message = $"Queue Item #{item.QueueItemId} (Part #{item.PartId}) has no vendor assigned."
                    });
                    result.Errors.Add($"Queue Item #{item.QueueItemId} is missing a suggested vendor assignment.");
                }
            }

            // Check vendor currency mismatches
            var vendorIds = eligibleItems
                .Where(i => i.SuggestedVendorId.HasValue && i.SuggestedVendorId.Value > 0)
                .Select(i => i.SuggestedVendorId!.Value)
                .Distinct()
                .ToList();

            if (vendorIds.Any())
            {
                var vendors = await _context.VendorMaster
                    .Where(v => vendorIds.Contains(v.VendorId))
                    .ToDictionaryAsync(v => v.VendorId);

                foreach (var vendorId in vendorIds)
                {
                    if (vendors.TryGetValue(vendorId, out var vendor))
                    {
                        if (!string.IsNullOrWhiteSpace(vendor.Currency) &&
                            !vendor.Currency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Warnings.Add(new QueueValidationWarning
                            {
                                VendorId = vendorId,
                                Code = "CURRENCY_MISMATCH",
                                Message = $"Vendor '{vendor.VendorName}' (ID: {vendorId}) default currency is '{vendor.Currency}' instead of EUR. Purchase Order totals will be generated in EUR."
                            });
                        }
                    }
                }
            }

            return result;
        }

        public async Task<AggregationResult> ConsolidateQueueItemsAsync(IEnumerable<int> queueItemIds, string? user = null)
        {
            var result = new AggregationResult();

            // Perform pre-validation
            var validation = await ValidateForConsolidationAsync(queueItemIds);
            result.Warnings.AddRange(validation.Warnings);

            if (!validation.IsValid)
            {
                result.Success = false;
                result.Errors.AddRange(validation.Errors);
                result.Message = "Consolidation failed validation constraints.";
                return result;
            }

            List<int> idList = queueItemIds?.Distinct().ToList() ?? new List<int>();
            var query = _context.PurchasingQueueItem.AsQueryable();

            if (idList.Any())
            {
                query = query.Where(q => idList.Contains(q.QueueItemId));
            }

            var rawItems = await query.ToListAsync();

            // Exclude Consolidated or Ordered items
            var eligibleItems = rawItems
                .Where(q => string.IsNullOrWhiteSpace(q.Status) ||
                            (!q.Status.Equals("Consolidated", StringComparison.OrdinalIgnoreCase) &&
                             !q.Status.Equals("Ordered", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!eligibleItems.Any())
            {
                result.Success = false;
                result.Message = "No pending purchasing queue items were found to consolidate.";
                return result;
            }

            IDbContextTransaction? transaction = null;
            if (!_context.Database.IsInMemory())
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }

            try
            {
                var vendorGroups = eligibleItems
                    .GroupBy(q => q.SuggestedVendorId!.Value)
                    .ToList();

                int poSeqCounter = await _context.PurchaseOrder.CountAsync() + 1;

                foreach (var vendorGroup in vendorGroups)
                {
                    int vendorId = vendorGroup.Key;
                    var vendorItems = vendorGroup.ToList();

                    // Generate unique PO Number
                    string poNumber = $"PO-{DateTime.UtcNow:yyyy}-{poSeqCounter:D3}";
                    while (await _context.PurchaseOrder.AnyAsync(p => p.PONumber == poNumber))
                    {
                        poSeqCounter++;
                        poNumber = $"PO-{DateTime.UtcNow:yyyy}-{poSeqCounter:D3}";
                    }
                    poSeqCounter++;

                    var draftPo = new PurchaseOrder
                    {
                        PONumber = poNumber,
                        VendorId = vendorId,
                        Status = "Draft",
                        OrderDate = DateTime.UtcNow,
                        Currency = "EUR",
                        SubTotalEur = 0m,
                        FreightEur = 0m,
                        TotalEur = "0.00",
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = user ?? "System"
                    };

                    _context.PurchaseOrder.Add(draftPo);
                    await _context.SaveChangesAsync();

                    // Group items by PartId and RideModel
                    var partRideGroups = vendorItems
                        .GroupBy(q => new { q.PartId, RideModel = q.RideModel ?? string.Empty })
                        .ToList();

                    int lineNumber = 1;
                    decimal poSubTotal = 0m;

                    foreach (var prGroup in partRideGroups)
                    {
                        var groupItems = prGroup.ToList();
                        decimal totalQty = groupItems.Sum(x => x.QuantityRequired);

                        var part = await _context.Part.FirstOrDefaultAsync(p => p.PartId == prGroup.Key.PartId);

                        decimal unitCost = groupItems.FirstOrDefault(x => x.SuggestedCostEur.HasValue && x.SuggestedCostEur.Value > 0)?.SuggestedCostEur
                                           ?? part?.LastVendorCostEur
                                           ?? 0m;

                        decimal lineTotal = totalQty * unitCost;

                        string? rideManufacturer = groupItems.Select(x => x.RideManufacturer)
                            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

                        string? rideModel = string.IsNullOrWhiteSpace(prGroup.Key.RideModel) ? null : prGroup.Key.RideModel;

                        var serialNumbers = groupItems
                            .Select(x => x.SerialNumber)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Distinct()
                            .ToList();
                        string? combinedSerial = serialNumbers.Any() ? string.Join(", ", serialNumbers) : null;

                        var sourceOrderLineIds = groupItems
                            .Where(x => x.SourceOrderLineId.HasValue && x.SourceOrderLineId.Value > 0)
                            .Select(x => x.SourceOrderLineId!.Value)
                            .Distinct()
                            .ToList();

                        int? primarySourceLineId = sourceOrderLineIds.FirstOrDefault() > 0 ? sourceOrderLineIds.FirstOrDefault() : (int?)null;

                        var notesList = new List<string>();
                        if (sourceOrderLineIds.Count > 1)
                        {
                            notesList.Add($"Source Line IDs: {string.Join(", ", sourceOrderLineIds)}");
                        }
                        var itemNotes = groupItems
                            .Select(x => x.Notes)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Distinct();
                        foreach (var n in itemNotes)
                        {
                            notesList.Add(n!);
                        }

                        string? combinedNotes = notesList.Any() ? string.Join(" | ", notesList) : null;

                        var poLine = new PurchaseOrderLine
                        {
                            PurchaseOrderId = draftPo.PurchaseOrderId,
                            LineNumber = lineNumber++,
                            PartId = prGroup.Key.PartId,
                            VendorPartNumber = null,
                            VendorDescription = part?.Description,
                            Quantity = totalQty,
                            QuantityReceived = 0m,
                            QuantityOutstanding = totalQty.ToString("F0"),
                            UnitCostEur = unitCost,
                            LineTotalEur = lineTotal.ToString("F2"),
                            RideManufacturer = rideManufacturer,
                            RideModel = rideModel,
                            SerialNumber = combinedSerial,
                            SourceOrderLineId = primarySourceLineId,
                            Notes = combinedNotes,
                            CreatedDate = DateTime.UtcNow
                        };

                        _context.PurchaseOrderLine.Add(poLine);
                        await _context.SaveChangesAsync();

                        // Update queue items
                        foreach (var qItem in groupItems)
                        {
                            qItem.Status = "Consolidated";
                            qItem.PurchaseOrderLineId = poLine.POLineId;
                            qItem.ModifiedDate = DateTime.UtcNow;
                            qItem.ModifiedBy = user ?? "System";
                        }

                        poSubTotal += lineTotal;
                    }

                    draftPo.SubTotalEur = poSubTotal;
                    draftPo.TotalEur = poSubTotal.ToString("F2");
                    await _context.SaveChangesAsync();

                    result.CreatedPurchaseOrders.Add(draftPo);
                    result.CreatedPurchaseOrderIds.Add(draftPo.PurchaseOrderId);
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                result.Success = true;
                result.ProcessedItemCount = eligibleItems.Count;
                result.Message = $"Successfully consolidated {eligibleItems.Count} queue item(s) into {result.CreatedPurchaseOrderIds.Count} draft purchase order(s).";
                return result;
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                result.Success = false;
                result.Errors.Add($"Consolidation failed due to error: {ex.Message}");
                result.Message = "An error occurred during queue item consolidation.";
                return result;
            }
        }
    }
}
