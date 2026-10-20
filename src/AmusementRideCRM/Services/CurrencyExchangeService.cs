namespace AmusementRideCRM.Services;

using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

public class CurrencyExchangeService : ICurrencyExchangeService
{
    private readonly ApplicationDbContext _context;
    public const decimal DefaultEurToUsdRate = 1.08m;

    public CurrencyExchangeService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<decimal> GetEffectiveRateAsync(string? sourceCurrency, string targetCurrency = "USD", DateTime? effectiveDate = null)
    {
        var record = await GetEffectiveRateRecordAsync(sourceCurrency, targetCurrency, effectiveDate);
        if (record != null)
        {
            return record.ExchangeRate;
        }

        var source = (sourceCurrency ?? "EUR").Trim().ToUpperInvariant();
        var target = (targetCurrency ?? "USD").Trim().ToUpperInvariant();

        if (source == target)
        {
            return 1.0m;
        }

        if (source == "EUR" && target == "USD")
        {
            return DefaultEurToUsdRate;
        }

        return 1.0m;
    }

    public async Task<CurrencyExchangeRate?> GetEffectiveRateRecordAsync(string? sourceCurrency, string targetCurrency = "USD", DateTime? effectiveDate = null)
    {
        var source = (sourceCurrency ?? "EUR").Trim().ToUpperInvariant();
        var target = (targetCurrency ?? "USD").Trim().ToUpperInvariant();
        var date = effectiveDate ?? DateTime.UtcNow;

        return await _context.CurrencyExchangeRate
            .Where(r => r.SourceCurrency.ToUpper() == source
                     && r.TargetCurrency.ToUpper() == target
                     && r.EffectiveDate <= date
                     && (r.EffectiveEndDate == null || r.EffectiveEndDate >= date))
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync();
    }

    public (decimal subTotalUsd, decimal freightUsd, decimal totalUsd) CalculateUsdValues(decimal subTotalSource, decimal freightSource, decimal? totalSource, decimal exchangeRate)
    {
        var rate = exchangeRate > 0 ? exchangeRate : 1.0m;
        var subTotalUsd = Math.Round(subTotalSource * rate, 2, MidpointRounding.AwayFromZero);
        var freightUsd = Math.Round(freightSource * rate, 2, MidpointRounding.AwayFromZero);

        decimal totalUsd;
        if (totalSource.HasValue)
        {
            totalUsd = Math.Round(totalSource.Value * rate, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            totalUsd = Math.Round((subTotalSource + freightSource) * rate, 2, MidpointRounding.AwayFromZero);
        }

        return (subTotalUsd, freightUsd, totalUsd);
    }
}
