using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.Part
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public IList<AmusementRideCRM.Models.Part> Items { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.Part != null)
            {
                var query = _context.Part.AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    var term = SearchTerm.Trim().ToLower();
                    query = query.Where(p => p.PartNumber.ToLower().Contains(term) 
                                          || p.Description.ToLower().Contains(term) 
                                          || (p.CategoryCode != null && p.CategoryCode.ToLower().Contains(term)));
                }

                Items = await query.Take(100).ToListAsync();

                foreach (var item in Items)
                {
                    item.AvailableQuantity = (item.QuantityOnHand - item.ReservedQuantity).ToString("F0");
                }
            }
        }
    }
}
