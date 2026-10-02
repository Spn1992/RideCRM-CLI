namespace AmusementRideCRM.Models;

public class ReceivingRecord
{
    public int ReceivingId { get; set; }
    public string ReceivingNumber { get; set; }
    public int PurchaseOrderId { get; set; }
    public int VendorId { get; set; }
    public DateTime ReceivingDate { get; set; }
    public int? ReceivedByUserId { get; set; }
    public int? WarehouseLocationId { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? PackingSlipNumber { get; set; }
    public string Status { get; set; }
    public bool HasDiscrepancy { get; set; }
    public string? DiscrepancyNotes { get; set; }
    public string? Notes { get; set; }
    public bool LabelsPrinted { get; set; }
    public int? LabelCount { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
