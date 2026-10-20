using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.VendorInvoice
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IVendorInvoiceVarianceService _varianceService;

        public DetailsModel(ApplicationDbContext context, IVendorInvoiceVarianceService varianceService)
        {
            _context = context;
            _varianceService = varianceService;
        }

        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = default!;
        public List<AmusementRideCRM.Models.VendorInvoiceLine> Lines { get; set; } = new();
        public VendorInvoiceEvaluationResult Evaluation { get; set; } = new();

        [BindProperty]
        public string? ReviewNotes { get; set; }

        [BindProperty]
        public bool UpdatePartMasterCosts { get; set; } = true;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.VendorInvoice.FirstOrDefaultAsync(m => m.VendorInvoiceId == id);
            if (item == null)
            {
                return NotFound();
            }

            Item = item;
            Lines = await _context.VendorInvoiceLine
                .Where(l => l.VendorInvoiceId == id)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();

            Evaluation = await _varianceService.EvaluateInvoiceVariancesAsync(Item, Lines);

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var item = await _context.VendorInvoice.FirstOrDefaultAsync(m => m.VendorInvoiceId == id);
            if (item == null) return NotFound();

            await _varianceService.ApproveReviewAsync(
                id,
                reviewedByUserId: 1, // Default authorized reviewer ID
                reviewNotes: ReviewNotes ?? "Approved cost shift during split-screen review.",
                updatePartMasterCosts: UpdatePartMasterCosts
            );

            TempData["SuccessMessage"] = "Invoice cost shifts approved! Invoice queued for QuickBooks accounting sync.";
            return RedirectToPage("./Details", new { id });
        }
    }
}
