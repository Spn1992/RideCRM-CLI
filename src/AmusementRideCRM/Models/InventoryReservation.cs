namespace AmusementRideCRM.Models;

public class InventoryReservation
{
    public int ReservationId { get; set; }
    public int OrderLineId { get; set; }
    public int PartId { get; set; }
    public decimal QuantityReserved { get; set; }
    public int? HoldingLocationId { get; set; }
    public DateTime ReservationDate { get; set; }
    public string Status { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
