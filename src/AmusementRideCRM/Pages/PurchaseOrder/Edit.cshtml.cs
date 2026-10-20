using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using System;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.PurchaseOrder
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrencyExchangeService _currencyExchangeService;

        public EditModel(ApplicationDbContext context, ICurrencyExchangeService currencyExchangeService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currencyExchangeService = currencyExchangeService ?? throw new ArgumentNullException(nameof(currencyExchangeService));
        }

        [BindProperty]
        public AmusementRideCRM.Models.PurchaseOrder Item { get; set; } = default!;

        public bool IsSyncedToQB { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _context.PurchaseOrder.FirstOrDefaultAsync(m => m.PurchaseOrderId == id);
            if (item == null) return NotFound();

            Item = item;
            IsSyncedToQB = !string.IsNullOrEmpty(item.QBPOTxnID);

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

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _context.PurchaseOrder.FirstOrDefaultAsync(p => p.PurchaseOrderId == Item.PurchaseOrderId);
            if (existing == null) return NotFound();

            bool isSynced = !string.IsNullOrEmpty(existing.QBPOTxnID);
            IsSyncedToQB = isSynced;

            existing.PONumber = Item.PONumber;
            existing.VendorId = Item.VendorId;
            existing.Status = Item.Status;
            existing.OrderDate = Item.OrderDate;
            existing.ExpectedDeliveryDate = Item.ExpectedDeliveryDate;
            existing.SentToVendorDate = Item.SentToVendorDate;
            existing.ShippingMethod = Item.ShippingMethod;
            existing.VendorNotes = Item.VendorNotes;
            existing.InternalNotes = Item.InternalNotes;
            existing.ModifiedBy = Item.ModifiedBy;
            existing.ModifiedDate = DateTime.UtcNow;

            if (!isSynced)
            {
                existing.Currency = string.IsNullOrWhiteSpace(Item.Currency) ? "EUR" : Item.Currency;
                existing.SubTotalEur = Item.SubTotalEur;
                existing.FreightEur = Item.FreightEur;
                existing.TotalEur = Item.TotalEur;

                existing.ExchangeRateDate = Item.ExchangeRateDate ?? (Item.OrderDate != default ? Item.OrderDate : DateTime.UtcNow);
                if (Item.ExchangeRate.HasValue && Item.ExchangeRate.Value > 0)
                {
                    existing.ExchangeRate = Item.ExchangeRate.Value;
                }
                else
                {
                    existing.ExchangeRate = await _currencyExchangeService.GetEffectiveRateAsync(existing.Currency, "USD", existing.ExchangeRateDate);
                }

                decimal? totalEurVal = null;
                if (!string.IsNullOrWhiteSpace(existing.TotalEur) && decimal.TryParse(existing.TotalEur, out var parsed))
                {
                    totalEurVal = parsed;
                }

                var (subUsd, freightUsd, totalUsd) = _currencyExchangeService.CalculateUsdValues(existing.SubTotalEur, existing.FreightEur, totalEurVal, existing.ExchangeRate.Value);
                existing.SubTotalUsd = subUsd;
                existing.FreightUsd = freightUsd;
                existing.TotalUsd = totalUsd;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
