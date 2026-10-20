namespace AmusementRideCRM.Tests;

using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using Xunit;

public class QbXmlSerializerTests
{
    private readonly QbXmlSerializer _serializer;

    public QbXmlSerializerTests()
    {
        _serializer = new QbXmlSerializer();
    }

    [Fact]
    public void BuildCustomerAddRq_ShouldGenerateValidXml()
    {
        // Arrange
        var customer = new Customer
        {
            CustomerId = 1,
            CompanyName = "Coaster World Parks",
            CustomerCode = "CUST-001",
            BillingAddress1 = "100 Fun St",
            BillingCity = "Orlando",
            BillingState = "FL",
            BillingPostalCode = "32801",
            BillingCountry = "USA",
            Phone = "555-0100",
            Email = "billing@coasterworld.com"
        };

        // Act
        var xml = _serializer.BuildCustomerAddRq(customer, requestId: 10);

        // Assert
        Assert.NotNull(xml);
        Assert.Contains("<?qbxml version=\"13.0\"?>", xml);
        Assert.Contains("<CustomerAddRq requestID=\"10\">", xml);
        Assert.Contains("<Name>Coaster World Parks</Name>", xml);
        Assert.Contains("<CompanyName>Coaster World Parks</CompanyName>", xml);
        Assert.Contains("<Addr1>100 Fun St</Addr1>", xml);
        Assert.Contains("<City>Orlando</City>", xml);
        Assert.Contains("<Phone>555-0100</Phone>", xml);
    }

    [Fact]
    public void BuildInvoiceAddRq_ShouldGenerateValidXml()
    {
        // Arrange
        var invoice = new CustomerInvoice
        {
            InvoiceId = 5,
            InvoiceNumber = "INV-2026-001",
            CustomerId = 1,
            InvoiceDate = new DateTime(2026, 10, 15),
            TotalAmount = "2500.00",
            BillToAddress1 = "100 Fun St",
            BillToCity = "Orlando"
        };

        var customer = new Customer
        {
            CustomerId = 1,
            QBCustomerListID = "80000001-1234567890",
            CompanyName = "Coaster World Parks"
        };

        var lines = new List<CustomerInvoiceLine>
        {
            new CustomerInvoiceLine
            {
                InvoiceLineId = 1,
                InvoiceId = 5,
                LineNumber = 1,
                LineType = "Roller Coaster Wheel Set",
                Description = "Heavy duty polyurethane wheel set",
                Quantity = 4,
                UnitPrice = 625.00m
            }
        };

        // Act
        var xml = _serializer.BuildInvoiceAddRq(invoice, lines, customer, requestId: 2);

        // Assert
        Assert.NotNull(xml);
        Assert.Contains("<InvoiceAddRq requestID=\"2\">", xml);
        Assert.Contains("<ListID>80000001-1234567890</ListID>", xml);
        Assert.Contains("<RefNumber>INV-2026-001</RefNumber>", xml);
        Assert.Contains("<TxnDate>2026-10-15</TxnDate>", xml);
        Assert.Contains("<FullName>Roller Coaster Wheel Set</FullName>", xml);
        Assert.Contains("<Quantity>4.00</Quantity>", xml);
        Assert.Contains("<Rate>625.00</Rate>", xml);
    }

    [Fact]
    public void BuildBillAddRq_ShouldGenerateValidXml()
    {
        // Arrange
        var bill = new VendorInvoice
        {
            VendorInvoiceId = 12,
            InvoiceNumber = "V-BILL-99",
            VendorId = 3,
            InvoiceDate = new DateTime(2026, 10, 1),
            TotalEur = 1200.00m,
            ReviewNotes = "Parts supply bill"
        };

        var vendor = new VendorMaster
        {
            VendorId = 3,
            VendorName = "Thrill Rides Supply Co",
            QBVendorListID = "80000002-9876543210"
        };

        var lines = new List<VendorInvoiceLine>
        {
            new VendorInvoiceLine
            {
                VILineId = 1,
                VendorInvoiceId = 12,
                LineNumber = 1,
                VendorPartNumber = "BEARING-XYZ",
                Description = "High precision bearing",
                Quantity = 10,
                UnitCostEur = 120.00m
            }
        };

        // Act
        var xml = _serializer.BuildBillAddRq(bill, lines, vendor, requestId: 3);

        // Assert
        Assert.NotNull(xml);
        Assert.Contains("<BillAddRq requestID=\"3\">", xml);
        Assert.Contains("<ListID>80000002-9876543210</ListID>", xml);
        Assert.Contains("<RefNumber>V-BILL-99</RefNumber>", xml);
        Assert.Contains("<FullName>BEARING-XYZ</FullName>", xml);
        Assert.Contains("<Cost>120.00</Cost>", xml);
    }

    [Fact]
    public void ParseResponseXml_CustomerAddRs_ShouldExtractIdentifiers()
    {
        // Arrange
        var xmlResponse = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <CustomerAddRs requestID=""1"" statusCode=""0"" statusSeverity=""Info"" statusMessage=""Status OK"">
      <CustomerRet>
        <ListID>80000001-11112222</ListID>
        <TimeCreated>2026-10-20T08:00:00</TimeCreated>
        <TimeModified>2026-10-20T08:00:00</TimeModified>
        <EditSequence>1700000000</EditSequence>
        <Name>Coaster World Parks</Name>
        <FullName>Coaster World Parks</FullName>
      </CustomerRet>
    </CustomerAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        // Act
        var result = _serializer.ParseResponseXml(xmlResponse);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("0", result.StatusCode);
        Assert.Equal("80000001-11112222", result.TxnOrListID);
        Assert.Equal("1700000000", result.EditSequence);
    }

    [Fact]
    public void ParseResponseXml_InvoiceAddRs_ShouldExtractTxnID()
    {
        // Arrange
        var xmlResponse = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <InvoiceAddRs requestID=""2"" statusCode=""0"" statusSeverity=""Info"" statusMessage=""Status OK"">
      <InvoiceRet>
        <TxnID>TXN-INV-9999</TxnID>
        <EditSequence>1700000005</EditSequence>
        <RefNumber>INV-2026-001</RefNumber>
      </InvoiceRet>
    </InvoiceAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        // Act
        var result = _serializer.ParseResponseXml(xmlResponse);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("0", result.StatusCode);
        Assert.Equal("TXN-INV-9999", result.TxnOrListID);
        Assert.Equal("1700000005", result.EditSequence);
        Assert.Equal("INV-2026-001", result.RefNumber);
    }

    [Fact]
    public void ParseResponseXml_ErrorResponse_ShouldReportFailure()
    {
        // Arrange
        var xmlResponse = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <CustomerAddRs requestID=""1"" statusCode=""3000"" statusSeverity=""Error"" statusMessage=""The name 'Duplicate Customer' is already in use."">
    </CustomerAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        // Act
        var result = _serializer.ParseResponseXml(xmlResponse);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("3000", result.StatusCode);
        Assert.Equal("Error", result.StatusSeverity);
        Assert.Contains("Duplicate Customer", result.StatusMessage);
    }
}
