using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PartMergeHistory
{
    public class CreateModel : PageModel
    {
        private readonly IPartMergeService _partMergeService;

        public CreateModel(IPartMergeService partMergeService)
        {
            _partMergeService = partMergeService;
        }

        [BindProperty]
        public AmusementRideCRM.Models.PartMergeHistory Item { get; set; } = default!;

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (Item.SurvivorPartId == Item.MergedPartId)
            {
                ModelState.AddModelError(string.Empty, "Survivor part ID and Merged part ID cannot be the same.");
                return Page();
            }

            var result = await _partMergeService.ExecuteMergeAsync(
                Item.SurvivorPartId,
                Item.MergedPartId,
                Item.MergedByUserName,
                Item.Notes,
                Item.MergedByUserId,
                Item.MergeDetails
            );

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to perform part merge.");
                return Page();
            }

            return RedirectToPage("./Index");
        }
    }
}
