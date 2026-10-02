namespace AmusementRideCRM.Models;

public class PurchaseOrder
{
    public int PurchaseOrderId { get; set; }
    public string PONumber { get; set; }
    public int VendorId { get; set; }
    public string Status { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? SentToVendorDate { get; set; }
    public string? ShippingMethod { get; set; }
    public string Currency { get; set; }
    public decimal SubTotalEur { get; set; }
    public decimal FreightEur { get; set; }
    public string? TotalEur { get; set; }
    public string? VendorNotes { get; set; }
    public string? InternalNotes { get; set; }
    public string? QBPOTxnID { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
