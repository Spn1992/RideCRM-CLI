#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.ReceivingLine
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public AmusementRideCRM.Models.ReceivingLine Item { get; set; } = default!;

        [BindProperty]
        public string? SupervisorUsername { get; set; }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            Item.CreatedDate = System.DateTime.UtcNow;
            _context.ReceivingLine.Add(Item);
            await _context.SaveChangesAsync();

            try
            {
                // Process Receiving Line: update Part stock levels, PO line receiving progress, discrepancy gates, holding bin allocations
                await ReceivingWorkflow.ProcessReceivingLineAsync(
                    _context,
                    Item,
                    user: User.Identity?.Name,
                    supervisorUsername: SupervisorUsername,
                    discrepancyNotes: Item.DiscrepancyNotes,
                    isSupervisorApproved: !string.IsNullOrWhiteSpace(SupervisorUsername));
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }

            return RedirectToPage("./Index");
        }
    }
}
