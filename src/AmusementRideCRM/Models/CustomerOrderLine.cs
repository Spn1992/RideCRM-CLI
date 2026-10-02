namespace AmusementRideCRM.Models;

public class CustomerOrderLine
{
    public int OrderLineId { get; set; }
    public int OrderId { get; set; }
    public int LineNumber { get; set; }
    public int PartId { get; set; }
    public int? RideId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceUsd { get; set; }
    public string? LineTotal { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityShipped { get; set; }
    public string? QuantityBackordered { get; set; }
    public string LineStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public byte[] RowVersion { get; set; }
}
