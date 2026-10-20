namespace AmusementRideCRM.Services;

using AmusementRideCRM.Models;

public interface ICurrencyExchangeService
{
    Task<decimal> GetEffectiveRateAsync(string? sourceCurrency, string targetCurrency = "USD", DateTime? effectiveDate = null);
    Task<CurrencyExchangeRate?> GetEffectiveRateRecordAsync(string? sourceCurrency, string targetCurrency = "USD", DateTime? effectiveDate = null);
    (decimal subTotalUsd, decimal freightUsd, decimal totalUsd) CalculateUsdValues(decimal subTotalSource, decimal freightSource, decimal? totalSource, decimal exchangeRate);
}
