namespace AmusementRideCRM.Services;

using System.Text;
using System.Xml;
using System.Xml.Linq;
using AmusementRideCRM.Models;

public class QbXmlSerializer : IQbXmlSerializer
{
    public string BuildCustomerAddRq(Customer customer, int requestId = 1)
    {
        if (customer == null) throw new ArgumentNullException(nameof(customer));

        var name = !string.IsNullOrWhiteSpace(customer.CompanyName)
            ? customer.CompanyName
            : (!string.IsNullOrWhiteSpace(customer.CustomerCode) ? customer.CustomerCode : $"Customer_{customer.CustomerId}");

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XProcessingInstruction("qbxml", "version=\"13.0\""),
            new XElement("QBXML",
                new XElement("QBXMLMsgsRq",
                    new XAttribute("onError", "stopOnError"),
                    new XElement("CustomerAddRq",
                        new XAttribute("requestID", requestId.ToString()),
                        new XElement("CustomerAdd",
                            new XElement("Name", name.Length > 41 ? name.Substring(0, 41) : name),
                            !string.IsNullOrWhiteSpace(customer.CompanyName) ? new XElement("CompanyName", customer.CompanyName) : null,
                            BuildAddressElement("BillAddress", customer.BillingAddress1, customer.BillingAddress2, customer.BillingCity, customer.BillingState, customer.BillingPostalCode, customer.BillingCountry),
                            BuildAddressElement("ShipAddress", customer.ShippingAddress1, customer.ShippingAddress2, customer.ShippingCity, customer.ShippingState, customer.ShippingPostalCode, customer.ShippingCountry),
                            !string.IsNullOrWhiteSpace(customer.Phone) ? new XElement("Phone", customer.Phone) : null,
                            !string.IsNullOrWhiteSpace(customer.Fax) ? new XElement("Fax", customer.Fax) : null,
                            !string.IsNullOrWhiteSpace(customer.Email) ? new XElement("Email", customer.Email) : null
                        )
                    )
                )
            )
        );

        return DocumentToString(doc);
    }

    public string BuildInvoiceAddRq(CustomerInvoice invoice, List<CustomerInvoiceLine> lines, Customer customer, int requestId = 1)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));

        var customerRef = new XElement("CustomerRef");
        if (customer != null && !string.IsNullOrWhiteSpace(customer.QBCustomerListID))
        {
            customerRef.Add(new XElement("ListID", customer.QBCustomerListID));
        }
        else if (customer != null && !string.IsNullOrWhiteSpace(customer.QBFullName))
        {
            customerRef.Add(new XElement("FullName", customer.QBFullName));
        }
        else if (customer != null && !string.IsNullOrWhiteSpace(customer.CompanyName))
        {
            customerRef.Add(new XElement("FullName", customer.CompanyName));
        }
        else if (!string.IsNullOrWhiteSpace(invoice.BillToName))
        {
            customerRef.Add(new XElement("FullName", invoice.BillToName));
        }
        else
        {
            customerRef.Add(new XElement("FullName", $"Customer_{invoice.CustomerId}"));
        }

        var invoiceAdd = new XElement("InvoiceAdd",
            customerRef,
            new XElement("TxnDate", (invoice.InvoiceDate != default ? invoice.InvoiceDate : DateTime.Today).ToString("yyyy-MM-dd")),
            !string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? new XElement("RefNumber", invoice.InvoiceNumber) : null,
            BuildAddressElement("BillAddress", invoice.BillToAddress1, invoice.BillToAddress2, invoice.BillToCity, invoice.BillToState, invoice.BillToPostalCode, invoice.BillToCountry),
            !string.IsNullOrWhiteSpace(invoice.PaymentTerms) ? new XElement("TermsRef", new XElement("FullName", invoice.PaymentTerms)) : null,
            !string.IsNullOrWhiteSpace(invoice.DueDate.HasValue ? invoice.DueDate.Value.ToString("yyyy-MM-dd") : null) ? new XElement("DueDate", invoice.DueDate.Value.ToString("yyyy-MM-dd")) : null,
            !string.IsNullOrWhiteSpace(invoice.Notes) ? new XElement("Memo", invoice.Notes) : null
        );

        if (lines != null && lines.Count > 0)
        {
            foreach (var line in lines)
            {
                var itemRefName = !string.IsNullOrWhiteSpace(line.LineType) ? line.LineType : "Sales";
                var desc = !string.IsNullOrWhiteSpace(line.Description) ? line.Description : itemRefName;
                var qty = line.Quantity > 0 ? line.Quantity : 1m;
                var rate = line.UnitPrice;

                invoiceAdd.Add(new XElement("InvoiceLineAdd",
                    new XElement("ItemRef", new XElement("FullName", itemRefName)),
                    new XElement("Desc", desc),
                    new XElement("Quantity", qty.ToString("0.00")),
                    new XElement("Rate", rate.ToString("0.00"))
                ));
            }
        }
        else
        {
            decimal totalVal = 0m;
            decimal.TryParse(invoice.TotalAmount, out totalVal);

            invoiceAdd.Add(new XElement("InvoiceLineAdd",
                new XElement("ItemRef", new XElement("FullName", "Sales")),
                new XElement("Desc", !string.IsNullOrWhiteSpace(invoice.Notes) ? invoice.Notes : "Customer Invoice"),
                new XElement("Quantity", "1.00"),
                new XElement("Rate", totalVal.ToString("0.00"))
            ));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XProcessingInstruction("qbxml", "version=\"13.0\""),
            new XElement("QBXML",
                new XElement("QBXMLMsgsRq",
                    new XAttribute("onError", "stopOnError"),
                    new XElement("InvoiceAddRq",
                        new XAttribute("requestID", requestId.ToString()),
                        invoiceAdd
                    )
                )
            )
        );

        return DocumentToString(doc);
    }

    public string BuildBillAddRq(VendorInvoice bill, List<VendorInvoiceLine> lines, VendorMaster vendor, int requestId = 1)
    {
        if (bill == null) throw new ArgumentNullException(nameof(bill));

        var rate = bill.ExchangeRate.HasValue && bill.ExchangeRate.Value > 0 ? bill.ExchangeRate.Value : 1.0m;

        var vendorRef = new XElement("VendorRef");
        if (vendor != null && !string.IsNullOrWhiteSpace(vendor.QBVendorListID))
        {
            vendorRef.Add(new XElement("ListID", vendor.QBVendorListID));
        }
        else if (vendor != null && !string.IsNullOrWhiteSpace(vendor.VendorName))
        {
            vendorRef.Add(new XElement("FullName", vendor.VendorName));
        }
        else
        {
            vendorRef.Add(new XElement("FullName", $"Vendor_{bill.VendorId}"));
        }

        var billAdd = new XElement("BillAdd",
            vendorRef,
            new XElement("TxnDate", (bill.InvoiceDate.HasValue ? bill.InvoiceDate.Value : DateTime.Today).ToString("yyyy-MM-dd")),
            !string.IsNullOrWhiteSpace(bill.InvoiceNumber) ? new XElement("RefNumber", bill.InvoiceNumber) : null,
            !string.IsNullOrWhiteSpace(bill.DueDate.HasValue ? bill.DueDate.Value.ToString("yyyy-MM-dd") : null) ? new XElement("DueDate", bill.DueDate.Value.ToString("yyyy-MM-dd")) : null,
            !string.IsNullOrWhiteSpace(bill.ReviewNotes) ? new XElement("Memo", bill.ReviewNotes) : null
        );

        if (lines != null && lines.Count > 0)
        {
            foreach (var line in lines)
            {
                var itemName = !string.IsNullOrWhiteSpace(line.VendorPartNumber) ? line.VendorPartNumber : "Supplies";
                var desc = !string.IsNullOrWhiteSpace(line.Description) ? line.Description : itemName;
                var qty = line.Quantity.HasValue && line.Quantity.Value > 0 ? line.Quantity.Value : 1m;
                var costEur = line.UnitCostEur.HasValue ? line.UnitCostEur.Value : 0m;
                var costUsd = Math.Round(costEur * rate, 2, MidpointRounding.AwayFromZero);

                billAdd.Add(new XElement("ItemLineAdd",
                    new XElement("ItemRef", new XElement("FullName", itemName)),
                    new XElement("Desc", desc),
                    new XElement("Quantity", qty.ToString("0.00")),
                    new XElement("Cost", costUsd.ToString("0.00"))
                ));
            }
        }
        else
        {
            var totalUsd = bill.TotalUsd.HasValue
                ? bill.TotalUsd.Value
                : Math.Round((bill.TotalEur.HasValue ? bill.TotalEur.Value : 0m) * rate, 2, MidpointRounding.AwayFromZero);

            billAdd.Add(new XElement("ExpenseLineAdd",
                new XElement("AccountRef", new XElement("FullName", "Accounts Payable")),
                new XElement("Amount", totalUsd.ToString("0.00")),
                new XElement("Memo", !string.IsNullOrWhiteSpace(bill.ReviewNotes) ? bill.ReviewNotes : "Vendor Bill")
            ));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XProcessingInstruction("qbxml", "version=\"13.0\""),
            new XElement("QBXML",
                new XElement("QBXMLMsgsRq",
                    new XAttribute("onError", "stopOnError"),
                    new XElement("BillAddRq",
                        new XAttribute("requestID", requestId.ToString()),
                        billAdd
                    )
                )
            )
        );

        return DocumentToString(doc);
    }

    public string BuildPurchaseOrderAddRq(PurchaseOrder po, List<PurchaseOrderLine> lines, VendorMaster vendor, int requestId = 1)
    {
        if (po == null) throw new ArgumentNullException(nameof(po));

        var rate = po.ExchangeRate.HasValue && po.ExchangeRate.Value > 0 ? po.ExchangeRate.Value : 1.0m;

        var vendorRef = new XElement("VendorRef");
        if (vendor != null && !string.IsNullOrWhiteSpace(vendor.QBVendorListID))
        {
            vendorRef.Add(new XElement("ListID", vendor.QBVendorListID));
        }
        else if (vendor != null && !string.IsNullOrWhiteSpace(vendor.VendorName))
        {
            vendorRef.Add(new XElement("FullName", vendor.VendorName));
        }
        else
        {
            vendorRef.Add(new XElement("FullName", $"Vendor_{po.VendorId}"));
        }

        var memoText = !string.IsNullOrWhiteSpace(po.VendorNotes)
            ? po.VendorNotes
            : (!string.IsNullOrWhiteSpace(po.InternalNotes) ? po.InternalNotes : null);

        var poAdd = new XElement("PurchaseOrderAdd",
            vendorRef,
            new XElement("TxnDate", (po.OrderDate != default ? po.OrderDate : DateTime.Today).ToString("yyyy-MM-dd")),
            !string.IsNullOrWhiteSpace(po.PONumber) ? new XElement("RefNumber", po.PONumber) : null,
            !string.IsNullOrWhiteSpace(memoText) ? new XElement("Memo", memoText) : null
        );

        if (lines != null && lines.Count > 0)
        {
            foreach (var line in lines)
            {
                var itemName = !string.IsNullOrWhiteSpace(line.VendorPartNumber) ? line.VendorPartNumber : $"Part_{line.PartId}";
                var desc = !string.IsNullOrWhiteSpace(line.VendorDescription) ? line.VendorDescription : itemName;
                var qty = line.Quantity > 0 ? line.Quantity : 1m;
                var costUsd = Math.Round(line.UnitCostEur * rate, 2, MidpointRounding.AwayFromZero);

                poAdd.Add(new XElement("PurchaseOrderLineAdd",
                    new XElement("ItemRef", new XElement("FullName", itemName)),
                    new XElement("Desc", desc),
                    new XElement("Quantity", qty.ToString("0.00")),
                    new XElement("Rate", costUsd.ToString("0.00"))
                ));
            }
        }
        else
        {
            decimal totalEurVal = 0m;
            if (!string.IsNullOrWhiteSpace(po.TotalEur))
            {
                decimal.TryParse(po.TotalEur, out totalEurVal);
            }
            if (totalEurVal <= 0m)
            {
                totalEurVal = po.SubTotalEur + po.FreightEur;
            }

            var totalUsd = po.TotalUsd.HasValue
                ? po.TotalUsd.Value
                : Math.Round(totalEurVal * rate, 2, MidpointRounding.AwayFromZero);

            poAdd.Add(new XElement("PurchaseOrderLineAdd",
                new XElement("ItemRef", new XElement("FullName", "Supplies")),
                new XElement("Desc", !string.IsNullOrWhiteSpace(memoText) ? memoText : "Purchase Order"),
                new XElement("Quantity", "1.00"),
                new XElement("Rate", totalUsd.ToString("0.00"))
            ));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XProcessingInstruction("qbxml", "version=\"13.0\""),
            new XElement("QBXML",
                new XElement("QBXMLMsgsRq",
                    new XAttribute("onError", "stopOnError"),
                    new XElement("PurchaseOrderAddRq",
                        new XAttribute("requestID", requestId.ToString()),
                        poAdd
                    )
                )
            )
        );

        return DocumentToString(doc);
    }

    public QbXmlResponseResult ParseResponseXml(string xmlResponse)
    {
        var result = new QbXmlResponseResult
        {
            IsSuccess = false,
            StatusCode = "-1",
            StatusSeverity = "Error",
            StatusMessage = "Unknown XML response format"
        };

        if (string.IsNullOrWhiteSpace(xmlResponse))
        {
            result.StatusMessage = "Empty XML response";
            return result;
        }

        try
        {
            var doc = XDocument.Parse(xmlResponse);
            var msgsRs = doc.Descendants("QBXMLMsgsRs").FirstOrDefault();
            if (msgsRs == null)
            {
                msgsRs = doc.Root;
            }

            var responseElement = msgsRs?.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Rs"));
            if (responseElement == null)
            {
                responseElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.EndsWith("Rs"));
            }

            if (responseElement != null)
            {
                result.StatusCode = responseElement.Attribute("statusCode")?.Value ?? "-1";
                result.StatusSeverity = responseElement.Attribute("statusSeverity")?.Value ?? "Info";
                result.StatusMessage = responseElement.Attribute("statusMessage")?.Value ?? "OK";
                result.IsSuccess = result.StatusCode == "0";

                var retElement = responseElement.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Ret"));
                if (retElement != null)
                {
                    var listId = retElement.Element("ListID")?.Value;
                    var txnId = retElement.Element("TxnID")?.Value;
                    result.TxnOrListID = !string.IsNullOrEmpty(txnId) ? txnId : listId;
                    result.EditSequence = retElement.Element("EditSequence")?.Value;
                    result.RefNumber = retElement.Element("RefNumber")?.Value;
                }
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.StatusCode = "-1";
            result.StatusSeverity = "Error";
            result.StatusMessage = $"XML Parse Error: {ex.Message}";
        }

        return result;
    }

    private static XElement BuildAddressElement(string elementName, string addr1, string addr2, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(addr1) && string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(postalCode))
            return null;

        return new XElement(elementName,
            !string.IsNullOrWhiteSpace(addr1) ? new XElement("Addr1", addr1) : null,
            !string.IsNullOrWhiteSpace(addr2) ? new XElement("Addr2", addr2) : null,
            !string.IsNullOrWhiteSpace(city) ? new XElement("City", city) : null,
            !string.IsNullOrWhiteSpace(state) ? new XElement("State", state) : null,
            !string.IsNullOrWhiteSpace(postalCode) ? new XElement("PostalCode", postalCode) : null,
            !string.IsNullOrWhiteSpace(country) ? new XElement("Country", country) : null
        );
    }

    private static string DocumentToString(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = false
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            doc.Save(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
