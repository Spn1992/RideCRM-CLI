namespace AmusementRideCRM.Models;

public class ShipmentLine
{
    public int ShipmentLineId { get; set; }
    public int ShipmentId { get; set; }
    public int OrderLineId { get; set; }
    public int PartId { get; set; }
    public decimal QuantityShipped { get; set; }
    public int? LocationId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}
