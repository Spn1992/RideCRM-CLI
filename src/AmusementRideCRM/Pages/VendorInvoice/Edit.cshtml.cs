using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.VendorInvoice
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrencyExchangeService _currencyExchangeService;

        public EditModel(ApplicationDbContext context, ICurrencyExchangeService currencyExchangeService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currencyExchangeService = currencyExchangeService ?? throw new ArgumentNullException(nameof(currencyExchangeService));
        private readonly IVendorInvoiceVarianceService _varianceService;

        public EditModel(ApplicationDbContext context, IVendorInvoiceVarianceService varianceService)
        {
            _context = context;
            _varianceService = varianceService;
        }

        [BindProperty]
        public AmusementRideCRM.Models.VendorInvoice Item { get; set; } = default!;

        public bool IsSyncedToQB { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.VendorInvoice.FirstOrDefaultAsync(m => m.VendorInvoiceId == id);
            if (item == null) return NotFound();

            Item = item;
            IsSyncedToQB = !string.IsNullOrEmpty(item.QBBillTxnID);

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

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _context.VendorInvoice.AsNoTracking().FirstOrDefaultAsync(v => v.VendorInvoiceId == Item.VendorInvoiceId);
            if (existing == null) return NotFound();

            bool isSynced = !string.IsNullOrEmpty(existing.QBBillTxnID);
            IsSyncedToQB = isSynced;

            if (isSynced)
            {
                // Snapshot exchange rate and converted USD amounts are immutable once synced
                Item.ExchangeRate = existing.ExchangeRate;
                Item.ExchangeRateDate = existing.ExchangeRateDate;
                Item.SubTotalUsd = existing.SubTotalUsd;
                Item.FreightUsd = existing.FreightUsd;
                Item.TotalUsd = existing.TotalUsd;
            }
            else
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
            }

            Item.ModifiedDate = DateTime.UtcNow;

            if (!ModelState.IsValid) return Page();

            Item.ModifiedDate = System.DateTime.UtcNow;
            _context.Attach(Item).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _varianceService.EvaluateInvoiceVariancesAsync(Item.VendorInvoiceId);

            return RedirectToPage("./Details", new { id = Item.VendorInvoiceId });
        }
    }
}
