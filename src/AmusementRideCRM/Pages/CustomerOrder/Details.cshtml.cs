using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.CustomerOrder
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public AmusementRideCRM.Models.CustomerOrder Item { get; set; } = default!;
        public List<AmusementRideCRM.Models.CustomerOrderLine> OrderLines { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();
        public List<AmusementRideCRM.Models.Part> AvailableParts { get; set; } = new();
        public AmusementRideCRM.Models.Customer Customer { get; set; }

        [BindProperty]
        public AmusementRideCRM.Models.CustomerOrderLine NewLine { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.CustomerOrder.FirstOrDefaultAsync(m => m.OrderId == id);
            if (item == null)
            {
                return NotFound();
            }
            Item = item;

            if (Item.CustomerId > 0)
            {
                Customer = await _context.Customer.FirstOrDefaultAsync(c => c.CustomerId == Item.CustomerId);
            }

            await LoadOrderDetailsAsync(id);
            return Page();
        }

        private async Task LoadOrderDetailsAsync(int orderId)
        {
            OrderLines = await _context.CustomerOrderLine
                .Where(l => l.OrderId == orderId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();

            var partIds = OrderLines.Select(l => l.PartId).Distinct().ToList();
            var parts = await _context.Part.Where(p => partIds.Contains(p.PartId)).ToListAsync();
            PartsDict = parts.ToDictionary(p => p.PartId);

            AvailableParts = await _context.Part.Where(p => p.IsActive || !p.IsMerged).Take(100).ToListAsync();

            RecalculateTotals();
            await _context.SaveChangesAsync();
        }

        public void RecalculateTotals()
        {
            decimal subtotal = 0;
            foreach (var line in OrderLines)
            {
                decimal lineTotal = line.Quantity * line.UnitPriceUsd;
                line.LineTotal = lineTotal.ToString("F2");
                subtotal += lineTotal;
            }
            Item.SubTotal = subtotal;
            Item.TotalAmount = Item.SubTotal + Item.ShippingCharge + Item.RushCharge + Item.OtherCharges - Item.DiscountAmount;
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(m => m.OrderId == id);
            if (order == null) return NotFound();

            order.Status = status;
            order.ModifiedDate = System.DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddLineAsync(int id)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(m => m.OrderId == id);
            if (order == null) return NotFound();

            var existingLines = await _context.CustomerOrderLine.Where(l => l.OrderId == id).ToListAsync();
            int maxLineNo = existingLines.Any() ? existingLines.Max(l => l.LineNumber) : 0;

            NewLine.OrderId = id;
            NewLine.LineNumber = maxLineNo + 1;
            NewLine.LineTotal = (NewLine.Quantity * NewLine.UnitPriceUsd).ToString("F2");
            NewLine.LineStatus = string.IsNullOrEmpty(NewLine.LineStatus) ? "Open" : NewLine.LineStatus;
            NewLine.CreatedDate = System.DateTime.UtcNow;

            _context.CustomerOrderLine.Add(NewLine);
            await _context.SaveChangesAsync();

            // Recalculate Order totals
            var allLines = await _context.CustomerOrderLine.Where(l => l.OrderId == id).ToListAsync();
            decimal subtotal = allLines.Sum(l => l.Quantity * l.UnitPriceUsd);
            order.SubTotal = subtotal;
            order.TotalAmount = order.SubTotal + order.ShippingCharge + order.RushCharge + order.OtherCharges - order.DiscountAmount;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteLineAsync(int id, int lineId)
        {
            var order = await _context.CustomerOrder.FirstOrDefaultAsync(m => m.OrderId == id);
            if (order == null) return NotFound();

            var line = await _context.CustomerOrderLine.FirstOrDefaultAsync(l => l.OrderLineId == lineId && l.OrderId == id);
            if (line != null)
            {
                _context.CustomerOrderLine.Remove(line);
                await _context.SaveChangesAsync();
            }

            var remainingLines = await _context.CustomerOrderLine.Where(l => l.OrderId == id).ToListAsync();
            decimal subtotal = remainingLines.Sum(l => l.Quantity * l.UnitPriceUsd);
            order.SubTotal = subtotal;
            order.TotalAmount = order.SubTotal + order.ShippingCharge + order.RushCharge + order.OtherCharges - order.DiscountAmount;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }
    }
}
