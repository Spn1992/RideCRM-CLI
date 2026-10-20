using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Pages.CustomerOrder
{
    public class ExceptionsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderExceptionValidationService _validationService;

        public ExceptionsModel(ApplicationDbContext context, IOrderExceptionValidationService validationService)
        {
            _context = context;
            _validationService = validationService;
        }

        public List<AmusementRideCRM.Models.CustomerOrder> FlaggedOrders { get; set; } = new();
        public Dictionary<int, List<AmusementRideCRM.Models.CustomerOrderLine>> OrderLinesDict { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Part> PartsDict { get; set; } = new();
        public Dictionary<int, AmusementRideCRM.Models.Customer> CustomersDict { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        public async Task OnGetAsync()
        {
            ViewData["ActivePage"] = "OrderExceptions";

            FlaggedOrders = await _context.CustomerOrder
                .Where(o => o.HasException)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var orderIds = FlaggedOrders.Select(o => o.OrderId).ToList();
            var lines = await _context.CustomerOrderLine
                .Where(l => orderIds.Contains(l.OrderId))
                .OrderBy(l => l.LineNumber)
                .ToListAsync();

            OrderLinesDict = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.ToList());

            var partIds = lines.Select(l => l.PartId).Distinct().ToList();
            var parts = await _context.Part.Where(p => partIds.Contains(p.PartId)).ToListAsync();
            PartsDict = parts.ToDictionary(p => p.PartId);

            var customerIds = FlaggedOrders.Select(o => o.CustomerId).Distinct().ToList();
            var customers = await _context.Customer.Where(c => customerIds.Contains(c.CustomerId)).ToListAsync();
            CustomersDict = customers.ToDictionary(c => c.CustomerId);
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string? comments)
        {
            var username = User?.Identity?.Name ?? "Supervisor";
            await _validationService.ApproveOrderExceptionsAsync(id, username, comments);
            SuccessMessage = $"Order #{id} exceptions have been approved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string? comments)
        {
            var username = User?.Identity?.Name ?? "Supervisor";
            await _validationService.RejectOrderExceptionsAsync(id, username, comments);
            SuccessMessage = $"Order #{id} exceptions have been rejected.";
            return RedirectToPage();
        }
    }
}
