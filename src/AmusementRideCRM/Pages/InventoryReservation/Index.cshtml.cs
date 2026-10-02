using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.InventoryReservation
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.InventoryReservation> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.InventoryReservation != null)
            {
                Items = await _context.InventoryReservation.Take(100).ToListAsync();
            }
        }
    }
}
