using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.CustomerOrderLine
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.CustomerOrderLine> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.CustomerOrderLine != null)
            {
                Items = await _context.CustomerOrderLine.Take(100).ToListAsync();
            }
        }
    }
}
