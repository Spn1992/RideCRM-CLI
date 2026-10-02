using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.Orders
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.CustomerOrder> Orders { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.CustomerOrder != null)
            {
                Orders = await _context.CustomerOrder.Take(50).ToListAsync();
            }
        }
    }
}
