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

        [BindProperty]
        public AmusementRideCRM.Models.ReceivingLine NewLine { get; set; } = new();

        [BindProperty]
        public string? SerialNumbersInput { get; set; }

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
                // Process Receiving Line: update Part stock levels, PO line progress, & SerializedUnits + ZPL print jobs
                await ReceivingWorkflow.ProcessReceivingLineAsync(_context, NewLine, serials, User.Identity?.Name);
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
