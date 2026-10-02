using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.CustomerRide
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.CustomerRide> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.CustomerRide != null)
            {
                Items = await _context.CustomerRide.Take(100).ToListAsync();
            }
        }
    }
}
