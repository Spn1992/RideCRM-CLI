using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.VendorInvoice
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.VendorInvoice> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.VendorInvoice != null)
            {
                Items = await _context.VendorInvoice.Take(100).ToListAsync();
            }
        }
    }
}
