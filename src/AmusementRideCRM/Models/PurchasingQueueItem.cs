namespace AmusementRideCRM.Models;

public class PurchasingQueueItem
{
    public int QueueItemId { get; set; }
    public int PartId { get; set; }
    public string SourceType { get; set; }
    public int? SourceOrderLineId { get; set; }
    public decimal QuantityRequired { get; set; }
    public decimal QuantityOrdered { get; set; }
    public int? SuggestedVendorId { get; set; }
    public decimal? SuggestedCostEur { get; set; }
    public string? RideManufacturer { get; set; }
    public string? RideModel { get; set; }
    public string? SerialNumber { get; set; }
    public string Status { get; set; }
    public int? PurchaseOrderLineId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
