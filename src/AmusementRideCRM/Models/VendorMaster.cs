namespace AmusementRideCRM.Models;

public class VendorMaster
{
    public int VendorId { get; set; }
    public string VendorName { get; set; }
    public string? VendorCode { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string Currency { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
    public string? QBVendorListID { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
