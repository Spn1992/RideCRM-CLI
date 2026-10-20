using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.VendorInvoice
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
        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!Item.InvoiceDate.HasValue)
            {
                Item.InvoiceDate = DateTime.Today;
            }

            if (string.IsNullOrWhiteSpace(Item.Currency))
            {
                Item.Currency = "EUR";
            }

            if (!Item.ExchangeRate.HasValue || Item.ExchangeRate.Value <= 0)
            {
                Item.ExchangeRate = await _currencyExchangeService.GetEffectiveRateAsync(Item.Currency, "USD", Item.InvoiceDate);
            }

            if (!Item.ExchangeRateDate.HasValue)
            {
                Item.ExchangeRateDate = Item.InvoiceDate;
            }

            var (subUsd, freightUsd, totalUsd) = _currencyExchangeService.CalculateUsdValues(Item.SubTotalEur ?? 0m, Item.FreightEur ?? 0m, Item.TotalEur, Item.ExchangeRate.Value);
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
                Item.ExchangeRateDate = Item.InvoiceDate ?? DateTime.UtcNow;
            }

            if (!Item.ExchangeRate.HasValue || Item.ExchangeRate.Value <= 0)
            {
                Item.ExchangeRate = await _currencyExchangeService.GetEffectiveRateAsync(Item.Currency, "USD", Item.ExchangeRateDate);
            }

            var (subUsd, freightUsd, totalUsd) = _currencyExchangeService.CalculateUsdValues(Item.SubTotalEur ?? 0m, Item.FreightEur ?? 0m, Item.TotalEur, Item.ExchangeRate.Value);
            Item.SubTotalUsd = subUsd;
            Item.FreightUsd = freightUsd;
            Item.TotalUsd = totalUsd;

            if (Item.CreatedDate == default)
            {
                Item.CreatedDate = DateTime.UtcNow;
            }
            if (Item.UploadedDate == default)
            {
                Item.UploadedDate = DateTime.UtcNow;
            }

            _context.VendorInvoice.Add(Item);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
