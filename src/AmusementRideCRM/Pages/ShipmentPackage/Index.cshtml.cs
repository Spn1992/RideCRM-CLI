using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.ShipmentPackage
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.ShipmentPackage> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.ShipmentPackage != null)
            {
                Items = await _context.ShipmentPackage.Take(100).ToListAsync();
            }
        }
    }
}
