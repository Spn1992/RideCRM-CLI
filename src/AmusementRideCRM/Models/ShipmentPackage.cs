namespace AmusementRideCRM.Models;

public class ShipmentPackage
{
    public int PackageId { get; set; }
    public int ShipmentId { get; set; }
    public int PackageNumber { get; set; }
    public decimal? WeightLbs { get; set; }
    public decimal? LengthIn { get; set; }
    public decimal? WidthIn { get; set; }
    public decimal? HeightIn { get; set; }
    public string? TrackingNumber { get; set; }
    public string? LabelImageUrl { get; set; }
    public decimal? InsuredValue { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}
