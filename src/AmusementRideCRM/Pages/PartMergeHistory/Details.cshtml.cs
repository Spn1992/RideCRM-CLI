using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PartMergeHistory
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.PartMergeHistory Item { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.PartMergeHistory.FirstOrDefaultAsync(m => m.MergeId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;
            return Page();
        }
    }
}
