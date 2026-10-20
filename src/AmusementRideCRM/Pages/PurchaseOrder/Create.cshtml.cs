using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PurchaseOrder
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrencyExchangeService _currencyExchangeService;

        public CreateModel(ApplicationDbContext context, ICurrencyExchangeService currencyExchangeService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currencyExchangeService = currencyExchangeService ?? throw new ArgumentNullException(nameof(currencyExchangeService));
        }

        [BindProperty]
        public AmusementRideCRM.Models.PurchaseOrder Item { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (Item.OrderDate == default)
            {
                Item.OrderDate = DateTime.Today;
            }

            if (string.IsNullOrWhiteSpace(Item.Currency))
            {
                Item.Currency = "EUR";
            }

            if (!Item.ExchangeRate.HasValue || Item.ExchangeRate.Value <= 0)
            {
                Item.ExchangeRate = await _currencyExchangeService.GetEffectiveRateAsync(Item.Currency, "USD", Item.OrderDate);
            }

            if (!Item.ExchangeRateDate.HasValue)
            {
                Item.ExchangeRateDate = Item.OrderDate;
            }

            decimal? totalEurVal = null;
            if (!string.IsNullOrWhiteSpace(Item.TotalEur) && decimal.TryParse(Item.TotalEur, out var parsed))
            {
                totalEurVal = parsed;
            }

            var (subUsd, freightUsd, totalUsd) = _currencyExchangeService.CalculateUsdValues(Item.SubTotalEur, Item.FreightEur, totalEurVal, Item.ExchangeRate.Value);
            Item.SubTotalUsd = subUsd;
            Item.FreightUsd = freightUsd;
            Item.TotalUsd = totalUsd;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Item.Currency))
            {
                Item.Currency = "EUR";
            }

            if (!Item.ExchangeRateDate.HasValue)
            {
                Item.ExchangeRateDate = Item.OrderDate != default ? Item.OrderDate : DateTime.UtcNow;
            }

            if (!Item.ExchangeRate.HasValue || Item.ExchangeRate.Value <= 0)
            {
                Item.ExchangeRate = await _currencyExchangeService.GetEffectiveRateAsync(Item.Currency, "USD", Item.ExchangeRateDate);
            }

            decimal? totalEurVal = null;
            if (!string.IsNullOrWhiteSpace(Item.TotalEur) && decimal.TryParse(Item.TotalEur, out var parsed))
            {
                totalEurVal = parsed;
            }

            var (subUsd, freightUsd, totalUsd) = _currencyExchangeService.CalculateUsdValues(Item.SubTotalEur, Item.FreightEur, totalEurVal, Item.ExchangeRate.Value);
            Item.SubTotalUsd = subUsd;
            Item.FreightUsd = freightUsd;
            Item.TotalUsd = totalUsd;

            if (Item.CreatedDate == default)
            {
                Item.CreatedDate = DateTime.UtcNow;
            }

            _context.PurchaseOrder.Add(Item);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
