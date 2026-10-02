using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.VendorInvoice
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.VendorInvoice.FirstOrDefaultAsync(m => m.VendorInvoiceId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;
            return Page();
        }
    }
}
