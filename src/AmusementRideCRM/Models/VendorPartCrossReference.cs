namespace AmusementRideCRM.Models;

public class VendorPartCrossReference
{
    public int CrossRefId { get; set; }
    public int VendorId { get; set; }
    public string VendorPartNumber { get; set; }
    public string? VendorDescription { get; set; }
    public int PartId { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool IsConfirmed { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
