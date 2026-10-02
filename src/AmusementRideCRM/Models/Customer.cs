namespace AmusementRideCRM.Models;

public class Customer
{
    public int CustomerId { get; set; }
    public string CompanyName { get; set; }
    public string? CustomerCode { get; set; }
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
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PaymentTerms { get; set; }
    public bool TaxExempt { get; set; }
    public decimal? CreditLimit { get; set; }
    public string? Notes { get; set; }
    public string? QBCustomerListID { get; set; }
    public string? QBFullName { get; set; }
    public string? QBEditSequence { get; set; }
    public bool IsSyncedFromQB { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
