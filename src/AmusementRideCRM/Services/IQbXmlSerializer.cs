namespace AmusementRideCRM.Services;

using AmusementRideCRM.Models;

public class QbXmlResponseResult
{
    public bool IsSuccess { get; set; }
    public string StatusCode { get; set; }
    public string StatusSeverity { get; set; }
    public string StatusMessage { get; set; }
    public string TxnOrListID { get; set; }
    public string EditSequence { get; set; }
    public string RefNumber { get; set; }
}

public interface IQbXmlSerializer
{
    string BuildCustomerAddRq(Customer customer, int requestId = 1);
    string BuildInvoiceAddRq(CustomerInvoice invoice, List<CustomerInvoiceLine> lines, Customer customer, int requestId = 1);
    string BuildBillAddRq(VendorInvoice bill, List<VendorInvoiceLine> lines, VendorMaster vendor, int requestId = 1);
    string BuildPurchaseOrderAddRq(PurchaseOrder po, List<PurchaseOrderLine> lines, VendorMaster vendor, int requestId = 1);
    QbXmlResponseResult ParseResponseXml(string xmlResponse);
}
