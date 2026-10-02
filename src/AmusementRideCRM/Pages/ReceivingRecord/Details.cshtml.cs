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
        public AmusementRideCRM.Models.PurchaseOrder PurchaseOrder { get; set; }
        public AmusementRideCRM.Models.VendorMaster Vendor { get; set; }

        [BindProperty]
        public AmusementRideCRM.Models.ReceivingLine NewLine { get; set; } = new();

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

            // Process Receiving Line: update Part stock levels & PO line receiving progress
            await ReceivingWorkflow.ProcessReceivingLineAsync(_context, NewLine);

            return RedirectToPage(new { id });
        }
    }
}
