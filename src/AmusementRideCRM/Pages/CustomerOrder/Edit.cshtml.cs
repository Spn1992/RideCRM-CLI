using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.CustomerOrder
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderExceptionValidationService _validationService;

        public EditModel(ApplicationDbContext context, IOrderExceptionValidationService validationService)
        {
            _context = context;
            _validationService = validationService;
        }

        [BindProperty]
        public AmusementRideCRM.Models.CustomerOrder Item { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.CustomerOrder.FirstOrDefaultAsync(m => m.OrderId == id);
            if (item == null) return NotFound();
            Item = item;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            // Check if user is attempting to set status to Confirmed or Approved when unresolved exceptions exist
            if (Item.Status == "Confirmed" || Item.Status == "Approved")
            {
                var currentOrder = await _context.CustomerOrder.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == Item.OrderId);
                if (currentOrder != null && currentOrder.HasException && currentOrder.ExceptionStatus != "Approved")
                {
                    ModelState.AddModelError("Item.Status", "Cannot set status to Confirmed/Approved. Order has unresolved pricing or stock exceptions that require supervisor approval.");
                    return Page();
                }
            }

            _context.Attach(Item).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Validate exceptions synchronously
            await _validationService.ValidateAndApplyExceptionsAsync(Item.OrderId);

            return RedirectToPage("./Index");
        }
    }
}
