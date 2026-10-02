namespace AmusementRideCRM.Models;

public class PartRideApplication
{
    public int PartRideAppId { get; set; }
    public int PartId { get; set; }
    public string RideManufacturer { get; set; }
    public string RideModel { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
}
