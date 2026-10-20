#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services
{
    public static class ReceivingWorkflow
    {
        public static async Task ProcessReceivingLineAsync(
            ApplicationDbContext context,
            ReceivingLine receivingLine,
            List<string>? serialNumbers = null,
            string? user = null,
            string? supervisorUsername = null,
            string? discrepancyNotes = null,
            bool isSupervisorApproved = false)
        {
            if (receivingLine == null || receivingLine.QuantityReceived <= 0) return;

            IDbContextTransaction? transaction = null;
            if (context.Database.IsRelational())
            {
                transaction = await context.Database.BeginTransactionAsync();
            }

            try
            {
                PurchaseOrderLine? poLine = null;
                if (receivingLine.POLineId > 0)
                {
                    poLine = await context.PurchaseOrderLine.FirstOrDefaultAsync(l => l.POLineId == receivingLine.POLineId);
                }

                // Discrepancy / Over-delivery Gate Evaluation
                decimal expectedQty = receivingLine.QuantityExpected > 0
                    ? receivingLine.QuantityExpected
                    : (poLine != null ? Math.Max(0, poLine.Quantity - poLine.QuantityReceived) : receivingLine.QuantityReceived);

                bool isOverDelivery = poLine != null
                    ? receivingLine.QuantityReceived > (poLine.Quantity - poLine.QuantityReceived)
                    : (receivingLine.QuantityExpected > 0 && receivingLine.QuantityReceived > receivingLine.QuantityExpected);

                bool isDamaged = receivingLine.QuantityDamaged > 0;
                bool isDiscrepancy = isOverDelivery || isDamaged;

                string? effectiveNotes = !string.IsNullOrWhiteSpace(discrepancyNotes)
                    ? discrepancyNotes.Trim()
                    : receivingLine.DiscrepancyNotes?.Trim();

                if (isDiscrepancy)
                {
                    bool hasSupervisorAuth = isSupervisorApproved || !string.IsNullOrWhiteSpace(supervisorUsername);

                    if (!hasSupervisorAuth)
                    {
                        throw new InvalidOperationException($"Over-delivery/discrepancy gate triggered for receiving line. Received quantity ({receivingLine.QuantityReceived}) exceeds expected/outstanding quantity ({expectedQty}). Supervisor review and authorization is required before saving.");
                    }

                    if (string.IsNullOrWhiteSpace(effectiveNotes))
                    {
                        throw new InvalidOperationException("Discrepancy notes are required when receiving lines exceed expected limits or report damaged goods.");
                    }

                    // Validate supervisor username against UserMaster if user records exist
                    if (!string.IsNullOrWhiteSpace(supervisorUsername) && await context.UserMaster.AnyAsync())
                    {
                        var normalizedSup = supervisorUsername.Trim().ToUpperInvariant();
                        var supervisorUser = await context.UserMaster.FirstOrDefaultAsync(u => u.UserName == supervisorUsername.Trim() || u.NormalizedUserName == normalizedSup);
                        if (supervisorUser == null)
                        {
                            throw new InvalidOperationException($"Supervisor authorization failed: User '{supervisorUsername}' not found.");
                        }
                    }

                    receivingLine.HasDiscrepancy = "Yes";
                    receivingLine.DiscrepancyNotes = effectiveNotes;

                    // Update parent ReceivingRecord discrepancy status
                    if (receivingLine.ReceivingId > 0)
                    {
                        var recRecord = await context.ReceivingRecord.FirstOrDefaultAsync(r => r.ReceivingId == receivingLine.ReceivingId);
                        if (recRecord != null)
                        {
                            recRecord.HasDiscrepancy = true;
                            recRecord.DiscrepancyNotes = string.IsNullOrWhiteSpace(recRecord.DiscrepancyNotes)
                                ? effectiveNotes
                                : $"{recRecord.DiscrepancyNotes}; {effectiveNotes}";
                            recRecord.ModifiedDate = DateTime.UtcNow;
                        }
                    }

                    // Log discrepancy audit entry
                    var audit = new AuditLog
                    {
                        AuditLogId = Guid.NewGuid().ToString(),
                        AuditDate = DateTime.UtcNow,
                        UserName = supervisorUsername ?? user ?? "Supervisor",
                        ActionType = "OverDeliveryDiscrepancyAuthorized",
                        EntityName = "ReceivingLine",
                        EntityId = receivingLine.ReceivingLineId.ToString(),
                        Description = $"Supervisor {supervisorUsername ?? user ?? "Supervisor"} authorized discrepancy (Received: {receivingLine.QuantityReceived}, Expected: {expectedQty}). Notes: {effectiveNotes}"
                    };
                    context.AuditLog.Add(audit);
                }

                var part = await context.Part.FirstOrDefaultAsync(p => p.PartId == receivingLine.PartId);

                // If part is serialized, validate and process serial numbers
                if (part != null && part.IsSerialized)
                {
                    var validSerials = serialNumbers?
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToList() ?? new List<string>();

                    if (validSerials.Count != (int)receivingLine.QuantityReceived)
                    {
                        throw new InvalidOperationException($"Part {part.PartNumber} is serialized. Exactly {(int)receivingLine.QuantityReceived} serial number(s) must be provided, but {validSerials.Count} were supplied.");
                    }

                    await SerializedPartService.ReceiveSerializedUnitsAsync(
                        context,
                        part.PartId,
                        receivingLine.ReceivingId,
                        receivingLine.ReceivingLineId,
                        validSerials,
                        user ?? "System");
                }

                // 1. Update Part Stock Levels
                if (part != null)
                {
                    part.QuantityOnHand += receivingLine.QuantityReceived;
                    part.AvailableQuantity = (part.QuantityOnHand - part.ReservedQuantity).ToString("F0");
                    part.ModifiedDate = DateTime.UtcNow;
                }

                // 2. Update Purchase Order Line & Order Status
                if (poLine != null)
                {
                    poLine.QuantityReceived += receivingLine.QuantityReceived;
                    poLine.QuantityOutstanding = Math.Max(0, poLine.Quantity - poLine.QuantityReceived).ToString("F0");
                    poLine.ModifiedDate = DateTime.UtcNow;

                    if (part != null && poLine.UnitCostEur > 0)
                    {
                        part.LastVendorCostEur = poLine.UnitCostEur;
                    }

                    var po = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.PurchaseOrderId == poLine.PurchaseOrderId);
                    if (po != null)
                    {
                        var allPoLines = await context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == po.PurchaseOrderId).ToListAsync();
                        if (allPoLines.All(l => l.QuantityReceived >= l.Quantity))
                        {
                            po.Status = "Received";
                        }
                        else if (allPoLines.Any(l => l.QuantityReceived > 0))
                        {
                            po.Status = "Partially Received";
                        }
                        po.ModifiedDate = DateTime.UtcNow;
                    }
                }

                // 3. Auto-Reserve Stock for Pending Customer Orders into Holding Locations (HOLD-BIN-XX)
                var pendingOrderLines = await context.CustomerOrderLine
                    .Where(col => col.PartId == receivingLine.PartId && col.Quantity > col.QuantityReserved && col.LineStatus != "Cancelled" && col.LineStatus != "Shipped")
                    .OrderBy(col => col.CreatedDate)
                    .ThenBy(col => col.OrderLineId)
                    .ToListAsync();

                decimal unallocatedQty = receivingLine.QuantityReceived;
                HoldingLocation? assignedHoldingBin = null;

                foreach (var col in pendingOrderLines)
                {
                    if (unallocatedQty <= 0) break;

                    decimal needed = col.Quantity - col.QuantityReserved;
                    decimal qtyToReserve = Math.Min(unallocatedQty, needed);

                    if (qtyToReserve > 0)
                    {
                        if (assignedHoldingBin == null)
                        {
                            assignedHoldingBin = await context.HoldingLocation.FirstOrDefaultAsync(h => h.IsAvailable);
                            if (assignedHoldingBin == null)
                            {
                                int binCount = await context.HoldingLocation.CountAsync() + 1;
                                assignedHoldingBin = new HoldingLocation
                                {
                                    LocationCode = $"HOLD-BIN-{binCount:D2}",
                                    LocationName = $"Holding Bin {binCount:D2}",
                                    Description = "Auto-assigned holding bin for customer order allocation",
                                    IsAvailable = true,
                                    CreatedDate = DateTime.UtcNow,
                                    CreatedBy = user ?? "System"
                                };
                                context.HoldingLocation.Add(assignedHoldingBin);
                                await context.SaveChangesAsync();
                            }
                        }

                        var reservation = new InventoryReservation
                        {
                            OrderLineId = col.OrderLineId,
                            PartId = col.PartId,
                            QuantityReserved = qtyToReserve,
                            HoldingLocationId = assignedHoldingBin.HoldingLocationId,
                            ReservationDate = DateTime.UtcNow,
                            Status = "Reserved",
                            Notes = $"Auto-reserved in holding bin {assignedHoldingBin.LocationCode} for Customer Order #{col.OrderId}",
                            CreatedBy = user ?? "System"
                        };
                        context.InventoryReservation.Add(reservation);

                        col.QuantityReserved += qtyToReserve;
                        col.ModifiedDate = DateTime.UtcNow;

                        if (part != null)
                        {
                            part.ReservedQuantity += qtyToReserve;
                            part.AvailableQuantity = (part.QuantityOnHand - part.ReservedQuantity).ToString("F0");
                        }

                        unallocatedQty -= qtyToReserve;
                    }
                }

                if (assignedHoldingBin != null)
                {
                    receivingLine.HoldingLocationId = assignedHoldingBin.HoldingLocationId;
                }

                // 4. Spool Zebra ZPL Barcode Print Requests for non-serialized parts
                if (part != null && !part.IsSerialized)
                {
                    int labelCount = receivingLine.LabelQty.HasValue && receivingLine.LabelQty.Value > 0
                        ? receivingLine.LabelQty.Value
                        : (int)Math.Max(1, receivingLine.QuantityReceived);

                    try
                    {
                        for (int i = 0; i < labelCount; i++)
                        {
                            var zpl = ZplService.GenerateZpl(
                                part.PartNumber,
                                part.Description,
                                $"REC-{receivingLine.ReceivingId}-{receivingLine.ReceivingLineId}-{i + 1}");

                            var printJob = new ZplPrintJob
                            {
                                PrinterName = "Zebra_Barcode_Printer",
                                ZplCommand = zpl,
                                Status = "Pending",
                                PartId = receivingLine.PartId,
                                ReceivingId = receivingLine.ReceivingId > 0 ? receivingLine.ReceivingId : null,
                                CreatedDate = DateTime.UtcNow
                            };
                            context.ZplPrintJob.Add(printJob);
                        }

                        if (receivingLine.ReceivingId > 0)
                        {
                            var recRecord = await context.ReceivingRecord.FirstOrDefaultAsync(r => r.ReceivingId == receivingLine.ReceivingId);
                            if (recRecord != null)
                            {
                                recRecord.LabelsPrinted = true;
                                recRecord.LabelCount = (recRecord.LabelCount ?? 0) + labelCount;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ZPL label spooling failed gracefully: {ex.Message}");
                    }
                }

                await context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
        }
    }
}
