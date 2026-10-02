namespace AmusementRideCRM.Models;

public class ReceivingLine
{
    public int ReceivingLineId { get; set; }
    public int ReceivingId { get; set; }
    public int POLineId { get; set; }
    public int PartId { get; set; }
    public decimal QuantityExpected { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityDamaged { get; set; }
    public int? LocationId { get; set; }
    public int? HoldingLocationId { get; set; }
    public string? HasDiscrepancy { get; set; }
    public string? DiscrepancyNotes { get; set; }
    public int? LabelQty { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}
