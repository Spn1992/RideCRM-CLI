using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.InventoryReservation
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryReservationService _inventoryService;

        public CreateModel(ApplicationDbContext context, IInventoryReservationService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
        }

        [BindProperty]
        public AmusementRideCRM.Models.InventoryReservation Item { get; set; } = default!;

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var result = await _inventoryService.CreateManualReservationAsync(Item, User?.Identity?.Name);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create reservation due to stock unavailability.");
                return Page();
            }

            return RedirectToPage("./Index");
        }
    }
}
