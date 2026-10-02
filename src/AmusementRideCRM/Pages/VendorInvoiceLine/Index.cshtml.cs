using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.VendorInvoiceLine
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.VendorInvoiceLine> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.VendorInvoiceLine != null)
            {
                Items = await _context.VendorInvoiceLine.Take(100).ToListAsync();
            }
        }
    }
}
