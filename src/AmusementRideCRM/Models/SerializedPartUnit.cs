#nullable enable
using System;

namespace AmusementRideCRM.Models;

public class SerializedPartUnit
{
    public int SerializedPartUnitId { get; set; }
    public int PartId { get; set; }
    public Part? Part { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "InStock"; // Status: Received, InStock, Installed, Returned, WarrantyClaim, Scrapped
    public int? ReceivingId { get; set; }
    public ReceivingRecord? ReceivingRecord { get; set; }
    public int? ReceivingLineId { get; set; }
    public ReceivingLine? ReceivingLine { get; set; }
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public int? ShipmentLineId { get; set; }
    public ShipmentLine? ShipmentLine { get; set; }
    public int? RideId { get; set; }
    public CustomerRide? CustomerRide { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
