namespace AmusementRideCRM.Models;

public class VendorInvoiceLine
{
    public int VILineId { get; set; }
    public int VendorInvoiceId { get; set; }
    public int LineNumber { get; set; }
    public int? PartId { get; set; }
    public string? VendorPartNumber { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitCostEur { get; set; }
    public string? LineTotalEur { get; set; }
    public int? POLineId { get; set; }
    public decimal? AiMatchConfidence { get; set; }
    public bool IsMatchConfirmed { get; set; }
    public int? MatchedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
