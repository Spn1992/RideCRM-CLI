namespace AmusementRideCRM.Models;

public class VendorInvoice
{
    public int VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; }
    public int? VendorId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Currency { get; set; }
    public decimal? SubTotalEur { get; set; }
    public decimal? FreightEur { get; set; }
    public decimal? TaxEur { get; set; }
    public decimal? TotalEur { get; set; }
    public string Status { get; set; }
    public string? UploadedFileName { get; set; }
    public string? UploadedFilePath { get; set; }
    public string? AiExtractionJson { get; set; }
    public decimal? AiConfidenceScore { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewNotes { get; set; }
    public bool HasDiscrepancy { get; set; }
    public string? DiscrepancyNotes { get; set; }
    public string? QBBillTxnID { get; set; }
    public string? QBBillEditSequence { get; set; }
    public DateTime UploadedDate { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
