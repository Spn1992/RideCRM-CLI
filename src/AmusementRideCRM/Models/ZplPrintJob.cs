#nullable enable
using System;

namespace AmusementRideCRM.Models;

public class ZplPrintJob
{
    public int PrintJobId { get; set; }
    public string PrinterName { get; set; } = "Zebra_Barcode_Printer";
    public string ZplCommand { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Status: Pending, Printed, Failed
    public int? SerializedPartUnitId { get; set; }
    public SerializedPartUnit? SerializedPartUnit { get; set; }
    public int? PartId { get; set; }
    public Part? Part { get; set; }
    public int? ReceivingId { get; set; }
    public ReceivingRecord? ReceivingRecord { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedDate { get; set; }
    public string? ErrorMessage { get; set; }
}
