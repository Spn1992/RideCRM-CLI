using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.VendorInvoice
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IVendorInvoiceVarianceService _varianceService;

        public CreateModel(ApplicationDbContext context, IVendorInvoiceVarianceService varianceService)
        {
            _context = context;
            _varianceService = varianceService;
        }

        [BindProperty]
        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = new();

        [BindProperty]
        public IFormFile? UploadedFile { get; set; }

        public IActionResult OnGet()
        {
            Item.InvoiceDate = DateTime.Today;
            Item.DueDate = DateTime.Today.AddDays(30);
            Item.Currency = "EUR";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (UploadedFile != null && UploadedFile.Length > 0)
            {
                var processedInvoice = await _varianceService.ProcessUploadedInvoiceAsync(
                    UploadedFile,
                    Item.VendorId,
                    Item.PurchaseOrderId,
                    User?.Identity?.Name ?? "Admin");

                return RedirectToPage("./Details", new { id = processedInvoice.VendorInvoiceId });
            }

            if (!ModelState.IsValid) return Page();

            Item.CreatedDate = DateTime.UtcNow;
            Item.CreatedBy = User?.Identity?.Name ?? "Admin";
            Item.UploadedDate = DateTime.UtcNow;

            _context.VendorInvoice.Add(Item);
            await _context.SaveChangesAsync();

            await _varianceService.EvaluateInvoiceVariancesAsync(Item.VendorInvoiceId);

            return RedirectToPage("./Details", new { id = Item.VendorInvoiceId });
        }
    }
}
