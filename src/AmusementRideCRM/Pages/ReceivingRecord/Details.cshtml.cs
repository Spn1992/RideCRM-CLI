#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.ReceivingRecord
{
    public class PutawayDirectiveViewModel
    {
        public int PartId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string DestinationType { get; set; } = string.Empty; // "Holding Allocation" or "Standard Putaway"
        public string LocationCode { get; set; } = string.Empty;
        public string GuidanceNotes { get; set; } = string.Empty;
        public int? OrderId { get; set; }
    }

    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.ReceivingRecord Item { get; set; } = default!;
        public List<AmusementRideCRM.Models.ReceivingLine> ReceivingLines { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();
        public List<AmusementRideCRM.Models.PurchaseOrderLine> POLines { get; set; } = new();
        public List<AmusementRideCRM.Models.Part> AvailableParts { get; set; } = new();
        public AmusementRideCRM.Models.PurchaseOrder? PurchaseOrder { get; set; }
        public AmusementRideCRM.Models.VendorMaster? Vendor { get; set; }
        public List<AmusementRideCRM.Models.SerializedPartUnit> SerializedUnits { get; set; } = new();
        public List<AmusementRideCRM.Models.ZplPrintJob> PrintJobs { get; set; } = new();
        public List<PutawayDirectiveViewModel> PutawayDirectives { get; set; } = new();
        public List<AuditLog> DiscrepancyAuditLogs { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.HoldingLocation> HoldingLocationsDict { get; set; } = new();

        [BindProperty]
        public AmusementRideCRM.Models.ReceivingLine NewLine { get; set; } = new();

        [BindProperty]
        public string? SerialNumbersInput { get; set; }

        [BindProperty]
        public string? SupervisorUsername { get; set; }

        [BindProperty]
        public string? DiscrepancyNotesInput { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.ReceivingRecord.FirstOrDefaultAsync(m => m.ReceivingId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;

            if (Item.PurchaseOrderId > 0)
            {
                PurchaseOrder = await _context.PurchaseOrder.FirstOrDefaultAsync(p => p.PurchaseOrderId == Item.PurchaseOrderId);
                POLines = await _context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == Item.PurchaseOrderId).ToListAsync();
            }

            if (Item.VendorId > 0)
            {
                Vendor = await _context.VendorMaster.FirstOrDefaultAsync(v => v.VendorId == Item.VendorId);
            }

            await LoadReceivingDetailsAsync(id);
            return Page();
        }

        private async Task LoadReceivingDetailsAsync(int receivingId)
        {
            ReceivingLines = await _context.ReceivingLine
                .Where(l => l.ReceivingId == receivingId)
                .ToListAsync();

            var partIds = ReceivingLines.Select(l => l.PartId).Distinct().ToList();
            var parts = await _context.Part.Where(p => partIds.Contains(p.PartId)).ToListAsync();
            PartsDict = parts.ToDictionary(p => p.PartId);

            AvailableParts = await _context.Part.Where(p => p.IsActive || !p.IsMerged).Take(100).ToListAsync();

            SerializedUnits = await _context.SerializedPartUnit
                .Include(u => u.Part)
                .Where(u => u.ReceivingId == receivingId)
                .ToListAsync();

            PrintJobs = await _context.ZplPrintJob
                .Where(j => j.ReceivingId == receivingId)
                .ToListAsync();

            var lineIdsStr = ReceivingLines.Select(l => l.ReceivingLineId.ToString()).ToList();
            DiscrepancyAuditLogs = await _context.AuditLog
                .Where(a => a.EntityName == "ReceivingLine" && lineIdsStr.Contains(a.EntityId))
                .OrderByDescending(a => a.AuditDate)
                .ToListAsync();

            var hLocIds = ReceivingLines.Where(l => l.HoldingLocationId.HasValue).Select(l => l.HoldingLocationId!.Value).Distinct().ToList();
            var hLocs = await _context.HoldingLocation.Where(h => hLocIds.Contains(h.HoldingLocationId)).ToListAsync();
            HoldingLocationsDict = hLocs.ToDictionary(h => h.HoldingLocationId);

            // Build Putaway Directives
            PutawayDirectives = new List<PutawayDirectiveViewModel>();
            foreach (var line in ReceivingLines)
            {
                var part = PartsDict.ContainsKey(line.PartId) ? PartsDict[line.PartId] : null;
                var partNum = part?.PartNumber ?? $"Part #{line.PartId}";
                var desc = part?.Description ?? "N/A";

                // Check reservations tied to this part
                var reservations = await _context.InventoryReservation
                    .Where(r => r.PartId == line.PartId && r.HoldingLocationId.HasValue)
                    .ToListAsync();

                decimal totalReserved = 0;
                foreach (var res in reservations)
                {
                    totalReserved += res.QuantityReserved;
                    AmusementRideCRM.Models.HoldingLocation? hBin = null;
                    if (res.HoldingLocationId.HasValue)
                    {
                        hBin = await _context.HoldingLocation.FirstOrDefaultAsync(h => h.HoldingLocationId == res.HoldingLocationId.Value);
                    }

                    int? orderId = null;
                    var col = await _context.CustomerOrderLine.FirstOrDefaultAsync(c => c.OrderLineId == res.OrderLineId);
                    if (col != null) orderId = col.OrderId;

                    PutawayDirectives.Add(new PutawayDirectiveViewModel
                    {
                        PartId = line.PartId,
                        PartNumber = partNum,
                        Description = desc,
                        Quantity = res.QuantityReserved,
                        DestinationType = "Holding Allocation",
                        LocationCode = hBin?.LocationCode ?? "HOLD-BIN-01",
                        GuidanceNotes = $"Route {res.QuantityReserved:N0} unit(s) to Holding Location {hBin?.LocationCode ?? "HOLD-BIN-01"} reserved for Customer Order #{orderId}",
                        OrderId = orderId
                    });
                }

                decimal unreserved = Math.Max(0, line.QuantityReceived - totalReserved);
                if (unreserved > 0)
                {
                    PutawayDirectives.Add(new PutawayDirectiveViewModel
                    {
                        PartId = line.PartId,
                        PartNumber = partNum,
                        Description = desc,
                        Quantity = unreserved,
                        DestinationType = "Standard Putaway",
                        LocationCode = "MAIN-STOCK",
                        GuidanceNotes = $"Place {unreserved:N0} unit(s) into primary warehouse bin storage"
                    });
                }
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            var rec = await _context.ReceivingRecord.FirstOrDefaultAsync(m => m.ReceivingId == id);
            if (rec == null) return NotFound();

            rec.Status = status;
            rec.ModifiedDate = System.DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddLineAsync(int id)
        {
            var rec = await _context.ReceivingRecord.FirstOrDefaultAsync(m => m.ReceivingId == id);
            if (rec == null) return NotFound();

            NewLine.ReceivingId = id;
            NewLine.CreatedDate = System.DateTime.UtcNow;

            _context.ReceivingLine.Add(NewLine);
            await _context.SaveChangesAsync();

            List<string>? serials = null;
            if (!string.IsNullOrWhiteSpace(SerialNumbersInput))
            {
                serials = SerialNumbersInput.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            try
            {
                // Process Receiving Line: over-delivery check, stock levels, holding allocation, ZPL spooling
                await ReceivingWorkflow.ProcessReceivingLineAsync(
                    _context,
                    NewLine,
                    serials,
                    User.Identity?.Name,
                    SupervisorUsername,
                    DiscrepancyNotesInput ?? NewLine.DiscrepancyNotes,
                    isSupervisorApproved: !string.IsNullOrWhiteSpace(SupervisorUsername));
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                Item = rec;
                await LoadReceivingDetailsAsync(id);
                return Page();
            }

            return RedirectToPage(new { id });
        }
    }
}
