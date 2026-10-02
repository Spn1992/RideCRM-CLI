namespace AmusementRideCRM.Models;

public class PurchaseOrderLine
{
    public int POLineId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int LineNumber { get; set; }
    public int PartId { get; set; }
    public string? VendorPartNumber { get; set; }
    public string? VendorDescription { get; set; }
    public decimal Quantity { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? QuantityOutstanding { get; set; }
    public decimal UnitCostEur { get; set; }
    public string? LineTotalEur { get; set; }
    public string? RideManufacturer { get; set; }
    public string? RideModel { get; set; }
    public string? SerialNumber { get; set; }
    public string? OrderPurpose { get; set; }
    public int? SourceOrderId { get; set; }
    public int? SourceOrderLineId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public byte[] RowVersion { get; set; }
}
