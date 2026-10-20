using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.PartMergeHistory
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IPartMergeService _partMergeService;

        public CreateModel(ApplicationDbContext context, IPartMergeService partMergeService)
        {
            _context = context;
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
            if (Item != null && Item.SurvivorPartId == Item.MergedPartId)
            {
                ModelState.AddModelError("Item.MergedPartId", "Survivor part ID cannot be the same as merged part ID.");
            }

            if (!ModelState.IsValid) return Page();

            try
            {
                await _partMergeService.ExecuteMergeAsync(
                    Item.SurvivorPartId,
                    Item.MergedPartId,
                    Item.MergedByUserName,
                    Item.Notes,
                    Item.MergedByUserId);

                return RedirectToPage("./Index");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }
        }
    }
}
