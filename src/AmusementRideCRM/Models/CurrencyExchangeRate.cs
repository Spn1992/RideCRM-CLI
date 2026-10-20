namespace AmusementRideCRM.Models;

public class CurrencyExchangeRate
{
    public int CurrencyExchangeRateId { get; set; }
    public string SourceCurrency { get; set; } = "EUR";
    public string TargetCurrency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
