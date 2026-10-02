namespace AmusementRideCRM.Models;

public class HoldingLocation
{
    public int HoldingLocationId { get; set; }
    public string LocationCode { get; set; }
    public string LocationName { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
