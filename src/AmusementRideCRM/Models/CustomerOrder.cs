namespace AmusementRideCRM.Models;

public class CustomerOrder
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public string OrderType { get; set; }
    public int CustomerId { get; set; }
    public int? ContactId { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? BillingAddress1 { get; set; }
    public string? BillingAddress2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }
    public string? ShippingAddress1 { get; set; }
    public string? ShippingAddress2 { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingState { get; set; }
    public string? ShippingPostalCode { get; set; }
    public string? ShippingCountry { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string ShipmentPreference { get; set; }
    public string Status { get; set; }
    public int? HoldingLocationId { get; set; }
    public DateTime? QuoteExpirationDate { get; set; }
    public string? QuotePricingLanguage { get; set; }
    public DateTime? ConvertedToOrderDate { get; set; }
    public int? OriginalQuoteId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal RushCharge { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal TotalAmount { get; set; }
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }
    public string? QBInvoiceTxnID { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
