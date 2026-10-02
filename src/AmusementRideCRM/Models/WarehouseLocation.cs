namespace AmusementRideCRM.Models;

public class WarehouseLocation
{
    public int LocationId { get; set; }
    public string LocationCode { get; set; }
    public string LocationName { get; set; }
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public string? Bin { get; set; }
    public bool IsHoldingArea { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
