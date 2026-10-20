using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
        private readonly IVendorInvoiceVarianceService _varianceService;

        public CreateModel(
            ApplicationDbContext context,
            ICurrencyExchangeService currencyExchangeService,
            IVendorInvoiceVarianceService varianceService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currencyExchangeService = currencyExchangeService ?? throw new ArgumentNullException(nameof(currencyExchangeService));
            _varianceService = varianceService ?? throw new ArgumentNullException(nameof(varianceService));
        }

        [BindProperty]
        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = new();

        [BindProperty]
        public IFormFile? UploadedFile { get; set; }

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

            Item.InvoiceDate = DateTime.Today;
            Item.DueDate = DateTime.Today.AddDays(30);
            Item.Currency = "EUR";
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
            if (UploadedFile != null && UploadedFile.Length > 0)
            {
                var processedInvoice = await _varianceService.ProcessUploadedInvoiceAsync(
                    UploadedFile,
                    Item.VendorId,
                    Item.PurchaseOrderId,
                    User?.Identity?.Name ?? "Admin");

                return RedirectToPage("./Details", new { id = processedInvoice.VendorInvoiceId });
            }

            if (!ModelState.IsValid) return Page();

            Item.CreatedDate = DateTime.UtcNow;
            Item.CreatedBy = User?.Identity?.Name ?? "Admin";
            Item.UploadedDate = DateTime.UtcNow;

            _context.VendorInvoice.Add(Item);
            await _context.SaveChangesAsync();

            await _varianceService.EvaluateInvoiceVariancesAsync(Item.VendorInvoiceId);

            return RedirectToPage("./Details", new { id = Item.VendorInvoiceId });
        }
    }
}
