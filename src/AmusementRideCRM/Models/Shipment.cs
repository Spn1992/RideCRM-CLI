namespace AmusementRideCRM.Models;

public class Shipment
{
    public int ShipmentId { get; set; }
    public string ShipmentNumber { get; set; }
    public int OrderId { get; set; }
    public DateTime ShipmentDate { get; set; }
    public int? ShippedByUserId { get; set; }
    public string? Carrier { get; set; }
    public string? ServiceType { get; set; }
    public string? TrackingNumber { get; set; }
    public string? ShippingLabelUrl { get; set; }
    public decimal? ShippingCostActual { get; set; }
    public decimal? ShippingCostBilled { get; set; }
    public string? ShipToName { get; set; }
    public string? ShipToAddress1 { get; set; }
    public string? ShipToAddress2 { get; set; }
    public string? ShipToCity { get; set; }
    public string? ShipToState { get; set; }
    public string? ShipToPostalCode { get; set; }
    public string? ShipToCountry { get; set; }
    public string Status { get; set; }
    public bool IsInvoiced { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
