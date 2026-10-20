using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.VendorInvoice
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IVendorInvoiceVarianceService _varianceService;

        public EditModel(ApplicationDbContext context, IVendorInvoiceVarianceService varianceService)
        {
            _context = context;
            _varianceService = varianceService;
        }

        [BindProperty]
        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.VendorInvoice.FirstOrDefaultAsync(m => m.VendorInvoiceId == id);
            if (item == null) return NotFound();
            Item = item;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            Item.ModifiedDate = System.DateTime.UtcNow;
            _context.Attach(Item).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _varianceService.EvaluateInvoiceVariancesAsync(Item.VendorInvoiceId);

            return RedirectToPage("./Details", new { id = Item.VendorInvoiceId });
        }
    }
}
