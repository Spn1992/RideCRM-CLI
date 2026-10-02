using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PurchaseOrderLine
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.PurchaseOrderLine> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.PurchaseOrderLine != null)
            {
                Items = await _context.PurchaseOrderLine.Take(100).ToListAsync();
            }
        }
    }
}
