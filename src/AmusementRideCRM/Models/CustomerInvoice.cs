namespace AmusementRideCRM.Models;

public class CustomerInvoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; }
    public int OrderId { get; set; }
    public int? ShipmentId { get; set; }
    public int CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string Currency { get; set; }
    public decimal SubTotal { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal RushCharge { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TotalAmount { get; set; }
    public string Status { get; set; }
    public string? BillToName { get; set; }
    public string? BillToAddress1 { get; set; }
    public string? BillToAddress2 { get; set; }
    public string? BillToCity { get; set; }
    public string? BillToState { get; set; }
    public string? BillToPostalCode { get; set; }
    public string? BillToCountry { get; set; }
    public string? QBInvoiceTxnID { get; set; }
    public string? QBEditSequence { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
