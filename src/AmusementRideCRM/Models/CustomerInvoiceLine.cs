namespace AmusementRideCRM.Models;

public class CustomerInvoiceLine
{
    public int InvoiceLineId { get; set; }
    public int InvoiceId { get; set; }
    public int LineNumber { get; set; }
    public string LineType { get; set; }
    public int? PartId { get; set; }
    public int? ShipmentLineId { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? LineTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
