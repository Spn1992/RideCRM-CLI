#nullable enable
using System;
using System.Threading.Tasks;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Services;

public static class ZplService
{
    public static string GenerateZpl(string partNumber, string description, string serialNumber)
    {
        var cleanPart = partNumber ?? string.Empty;
        var cleanDesc = !string.IsNullOrEmpty(description) && description.Length > 30
            ? description.Substring(0, 30)
            : (description ?? string.Empty);
        var cleanSerial = serialNumber ?? string.Empty;

        return $@"^XA
^FO50,30^A0N,30,30^FDPart: {cleanPart}^FS
^FO50,70^A0N,24,24^FDDesc: {cleanDesc}^FS
^FO50,110^BCN,80,Y,N,N
^FD{cleanSerial}^FS
^XZ";
    }

    public static async Task<ZplPrintJob> QueueZplPrintJobAsync(
        ApplicationDbContext context,
        int partId,
        int serializedPartUnitId,
        int receivingId,
        string zplCommand,
        string printerName = "Zebra_Barcode_Printer")
    {
        var printJob = new ZplPrintJob
        {
            PrinterName = printerName,
            ZplCommand = zplCommand,
            Status = "Pending",
            PartId = partId,
            SerializedPartUnitId = serializedPartUnitId,
            ReceivingId = receivingId > 0 ? receivingId : null,
            CreatedDate = DateTime.UtcNow
        };

        context.ZplPrintJob.Add(printJob);
        await context.SaveChangesAsync();
        return printJob;
    }
}
