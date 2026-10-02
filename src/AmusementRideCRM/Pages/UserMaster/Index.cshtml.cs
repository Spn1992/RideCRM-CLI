using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.UserMaster
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.UserMaster> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.UserMaster != null)
            {
                Items = await _context.UserMaster.Take(100).ToListAsync();
            }
        }
    }
}
