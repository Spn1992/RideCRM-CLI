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

            // Process Receiving Line: update Part stock levels & PO line receiving progress
            await ReceivingWorkflow.ProcessReceivingLineAsync(_context, Item);

            return RedirectToPage("./Index");
        }
    }
}
