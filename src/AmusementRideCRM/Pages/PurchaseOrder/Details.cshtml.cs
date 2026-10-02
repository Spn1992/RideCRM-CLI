using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PurchaseOrder
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.PurchaseOrder Item { get; set; } = default!;
        public List<AmusementRideCRM.Models.PurchaseOrderLine> POLines { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();
        public List<AmusementRideCRM.Models.Part> AvailableParts { get; set; } = new();
        public AmusementRideCRM.Models.VendorMaster Vendor { get; set; }

        [BindProperty]
        public AmusementRideCRM.Models.PurchaseOrderLine NewLine { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.PurchaseOrder.FirstOrDefaultAsync(m => m.PurchaseOrderId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;

            if (Item.VendorId > 0)
            {
                Vendor = await _context.VendorMaster.FirstOrDefaultAsync(v => v.VendorId == Item.VendorId);
            }

            await LoadPODetailsAsync(id);
            return Page();
        }

        private async Task LoadPODetailsAsync(int poId)
        {
            POLines = await _context.PurchaseOrderLine
                .Where(l => l.PurchaseOrderId == poId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();

            var partIds = POLines.Select(l => l.PartId).Distinct().ToList();
            var parts = await _context.Part.Where(p => partIds.Contains(p.PartId)).ToListAsync();
            PartsDict = parts.ToDictionary(p => p.PartId);

            AvailableParts = await _context.Part.Where(p => p.IsActive || !p.IsMerged).Take(100).ToListAsync();

            RecalculateTotals();
            await _context.SaveChangesAsync();
        }

        public void RecalculateTotals()
        {
            decimal subtotal = 0;
            foreach (var line in POLines)
            {
                decimal lineTotal = line.Quantity * line.UnitCostEur;
                line.LineTotalEur = lineTotal.ToString("F2");
                line.QuantityOutstanding = (line.Quantity - line.QuantityReceived).ToString("F0");
                subtotal += lineTotal;
            }
            Item.SubTotalEur = subtotal;
            Item.TotalEur = (Item.SubTotalEur + Item.FreightEur).ToString("F2");
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            var po = await _context.PurchaseOrder.FirstOrDefaultAsync(m => m.PurchaseOrderId == id);
            if (po == null) return NotFound();

            po.Status = status;
            po.ModifiedDate = System.DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddLineAsync(int id)
        {
            var po = await _context.PurchaseOrder.FirstOrDefaultAsync(m => m.PurchaseOrderId == id);
            if (po == null) return NotFound();

            var existingLines = await _context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == id).ToListAsync();
            int maxLineNo = existingLines.Any() ? existingLines.Max(l => l.LineNumber) : 0;

            NewLine.PurchaseOrderId = id;
            NewLine.LineNumber = maxLineNo + 1;
            NewLine.LineTotalEur = (NewLine.Quantity * NewLine.UnitCostEur).ToString("F2");
            NewLine.QuantityOutstanding = NewLine.Quantity.ToString("F0");
            NewLine.CreatedDate = System.DateTime.UtcNow;

            _context.PurchaseOrderLine.Add(NewLine);
            await _context.SaveChangesAsync();

            // Recalculate PO totals
            var allLines = await _context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == id).ToListAsync();
            decimal subtotal = allLines.Sum(l => l.Quantity * l.UnitCostEur);
            po.SubTotalEur = subtotal;
            po.TotalEur = (po.SubTotalEur + po.FreightEur).ToString("F2");
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteLineAsync(int id, int lineId)
        {
            var po = await _context.PurchaseOrder.FirstOrDefaultAsync(m => m.PurchaseOrderId == id);
            if (po == null) return NotFound();

            var line = await _context.PurchaseOrderLine.FirstOrDefaultAsync(l => l.POLineId == lineId && l.PurchaseOrderId == id);
            if (line != null)
            {
                _context.PurchaseOrderLine.Remove(line);
                await _context.SaveChangesAsync();
            }

            var remainingLines = await _context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == id).ToListAsync();
            decimal subtotal = remainingLines.Sum(l => l.Quantity * l.UnitCostEur);
            po.SubTotalEur = subtotal;
            po.TotalEur = (po.SubTotalEur + po.FreightEur).ToString("F2");
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }
    }
}
