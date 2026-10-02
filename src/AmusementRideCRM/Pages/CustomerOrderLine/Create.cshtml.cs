using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Linq;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.CustomerOrderLine
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public AmusementRideCRM.Models.CustomerOrderLine Item { get; set; } = default!;

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            Item.LineTotal = (Item.Quantity * Item.UnitPriceUsd).ToString("F2");
            Item.CreatedDate = System.DateTime.UtcNow;
            if (string.IsNullOrEmpty(Item.LineStatus)) Item.LineStatus = "Open";

            _context.CustomerOrderLine.Add(Item);
            await _context.SaveChangesAsync();

            // Recalculate parent CustomerOrder totals
            var parentOrder = await _context.CustomerOrder.FirstOrDefaultAsync(o => o.OrderId == Item.OrderId);
            if (parentOrder != null)
            {
                var lines = await _context.CustomerOrderLine.Where(l => l.OrderId == Item.OrderId).ToListAsync();
                parentOrder.SubTotal = lines.Sum(l => l.Quantity * l.UnitPriceUsd);
                parentOrder.TotalAmount = parentOrder.SubTotal + parentOrder.ShippingCharge + parentOrder.RushCharge + parentOrder.OtherCharges - parentOrder.DiscountAmount;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
