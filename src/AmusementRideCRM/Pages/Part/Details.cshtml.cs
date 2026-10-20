using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.Part
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.Part Item { get; set; } = default!;
        public List<SerializedPartUnit> SerializedUnits { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.Part.FirstOrDefaultAsync(m => m.PartId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;

            if (Item.IsSerialized)
            {
                SerializedUnits = await _context.SerializedPartUnit
                    .Include(u => u.CustomerRide)
                    .Where(u => u.PartId == id)
                    .OrderByDescending(u => u.CreatedDate)
                    .ToListAsync();
            }

            return Page();
        }
    }
}
