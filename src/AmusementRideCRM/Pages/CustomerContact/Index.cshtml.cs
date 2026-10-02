using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.CustomerContact
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AmusementRideCRM.Models.CustomerContact> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.CustomerContact != null)
            {
                Items = await _context.CustomerContact.Take(100).ToListAsync();
            }
        }
    }
}
