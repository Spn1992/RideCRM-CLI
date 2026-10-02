namespace AmusementRideCRM.Models;

public class VendorSource
{
    public int VendorSourceId { get; set; }
    public int PartId { get; set; }
    public int VendorId { get; set; }
    public string? VendorPartNumber { get; set; }
    public string? VendorDescription { get; set; }
    public decimal? LastKnownCostEur { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
