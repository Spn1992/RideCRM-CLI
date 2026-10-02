using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PurchasingQueueItem
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public AmusementRideCRM.Models.PurchasingQueueItem Item { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.PurchasingQueueItem.FirstOrDefaultAsync(m => m.QueueItemId == id);
            if (item == null) return NotFound();
            Item = item;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            _context.Attach(Item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
