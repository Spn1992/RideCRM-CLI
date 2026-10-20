using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.CustomerRide
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.CustomerRide Item { get; set; } = default!;
        public AmusementRideCRM.Models.Customer Customer { get; set; }
        public List<SerializedPartUnit> InstalledComponents { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.CustomerRide.FirstOrDefaultAsync(m => m.RideId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;

            if (Item.CustomerId > 0)
            {
                Customer = await _context.Customer.FirstOrDefaultAsync(c => c.CustomerId == Item.CustomerId);
            }

            InstalledComponents = await SerializedPartService.GetRideComponentHistoryAsync(_context, id);

            return Page();
        }
    }
}
