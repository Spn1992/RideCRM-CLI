namespace AmusementRideCRM.Tests;

using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using Xunit;

public class QBWebConnectorSvcTests
{
    private ApplicationDbContext GetDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void ServerVersion_ShouldReturn200()
    {
        // Arrange
        using var db = GetDbContext(nameof(ServerVersion_ShouldReturn200));
        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        // Act
        var version = svc.serverVersion();

        // Assert
        Assert.Equal("2.0.0", version);
    }

    [Fact]
    public void Authenticate_WithNoQueueItems_ShouldReturnNone()
    {
        // Arrange
        using var db = GetDbContext(nameof(Authenticate_WithNoQueueItems_ShouldReturnNone));
        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        // Act
        var result = svc.authenticate("admin", "pass");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.False(string.IsNullOrWhiteSpace(result[0]));
        Assert.Equal("none", result[1]);
    }

    [Fact]
    public void Authenticate_WithPendingQueueItem_ShouldReturnEmptyWorkStatus()
    {
        // Arrange
        using var db = GetDbContext(nameof(Authenticate_WithPendingQueueItem_ShouldReturnEmptyWorkStatus));
        db.QBSyncQueue.Add(new QBSyncQueue
        {
            SyncId = 1,
            EntityType = "Customer",
            EntityId = 10,
            SyncDirection = "ToQB",
            SyncAction = "Create",
            Status = "Pending",
            Priority = 1,
            CreatedDate = DateTime.UtcNow
        });
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        // Act
        var result = svc.authenticate("admin", "pass");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.False(string.IsNullOrWhiteSpace(result[0]));
        Assert.Equal(string.Empty, result[1]);
    }

    [Fact]
    public void SendRequestXML_ShouldRetrieveItem_GenerateQbXml_AndMarkProcessing()
    {
        // Arrange
        using var db = GetDbContext(nameof(SendRequestXML_ShouldRetrieveItem_GenerateQbXml_AndMarkProcessing));
        var customer = new Customer
        {
            CustomerId = 10,
            CompanyName = "Funland Amusement Park",
            Phone = "555-9999",
            Email = "info@funland.com"
        };
        db.Customer.Add(customer);

        var queueItem = new QBSyncQueue
        {
            SyncId = 1,
            EntityType = "Customer",
            EntityId = 10,
            SyncDirection = "ToQB",
            SyncAction = "Create",
            Status = "Pending",
            Priority = 1,
            CreatedDate = DateTime.UtcNow
        };
        db.QBSyncQueue.Add(queueItem);
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        var authResult = svc.authenticate("admin", "pass");
        var ticket = authResult[0];

        // Act
        var reqXml = svc.sendRequestXML(ticket, "", "", "", 13, 0);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(reqXml));
        Assert.Contains("CustomerAddRq", reqXml);
        Assert.Contains("Funland Amusement Park", reqXml);

        var dbQueueItem = db.QBSyncQueue.Find(1);
        Assert.NotNull(dbQueueItem);
        Assert.Equal("Processing", dbQueueItem.Status);
        Assert.False(string.IsNullOrWhiteSpace(dbQueueItem.QBRequestXml));
    }

    [Fact]
    public void ReceiveResponseXML_Success_ShouldMarkSuccess_AndStoreTxnIDInDomainEntity()
    {
        // Arrange
        using var db = GetDbContext(nameof(ReceiveResponseXML_Success_ShouldMarkSuccess_AndStoreTxnIDInDomainEntity));
        var customer = new Customer
        {
            CustomerId = 10,
            CompanyName = "Funland Amusement Park"
        };
        db.Customer.Add(customer);

        var queueItem = new QBSyncQueue
        {
            SyncId = 1,
            EntityType = "Customer",
            EntityId = 10,
            SyncDirection = "ToQB",
            SyncAction = "Create",
            Status = "Pending",
            Priority = 1,
            CreatedDate = DateTime.UtcNow
        };
        db.QBSyncQueue.Add(queueItem);
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        var authResult = svc.authenticate("admin", "pass");
        var ticket = authResult[0];
        svc.sendRequestXML(ticket, "", "", "", 13, 0);

        var responseXml = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <CustomerAddRs requestID=""1"" statusCode=""0"" statusSeverity=""Info"" statusMessage=""Status OK"">
      <CustomerRet>
        <ListID>80000001-99998888</ListID>
        <EditSequence>12345678</EditSequence>
        <Name>Funland Amusement Park</Name>
      </CustomerRet>
    </CustomerAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        // Act
        var progress = svc.receiveResponseXML(ticket, responseXml, "", "");

        // Assert
        Assert.Equal(100, progress);

        var dbQueueItem = db.QBSyncQueue.Find(1);
        Assert.NotNull(dbQueueItem);
        Assert.Equal("Success", dbQueueItem.Status);
        Assert.Equal("80000001-99998888", dbQueueItem.QBTxnID);
        Assert.Equal("12345678", dbQueueItem.QBEditSequence);
        Assert.NotNull(dbQueueItem.ProcessedDate);

        var updatedCustomer = db.Customer.Find(10);
        Assert.NotNull(updatedCustomer);
        Assert.Equal("80000001-99998888", updatedCustomer.QBCustomerListID);
        Assert.Equal("12345678", updatedCustomer.QBEditSequence);
        Assert.True(updatedCustomer.IsSyncedFromQB);
    }

    [Fact]
    public void ReceiveResponseXML_InvoiceSuccess_ShouldUpdateCustomerInvoiceRecord()
    {
        // Arrange
        using var db = GetDbContext(nameof(ReceiveResponseXML_InvoiceSuccess_ShouldUpdateCustomerInvoiceRecord));
        var invoice = new CustomerInvoice
        {
            InvoiceId = 20,
            InvoiceNumber = "INV-100",
            CustomerId = 5,
            InvoiceDate = DateTime.Today,
            TotalAmount = "1500.00"
        };
        db.CustomerInvoice.Add(invoice);

        var queueItem = new QBSyncQueue
        {
            SyncId = 2,
            EntityType = "CustomerInvoice",
            EntityId = 20,
            SyncDirection = "ToQB",
            SyncAction = "Create",
            Status = "Pending",
            Priority = 1,
            CreatedDate = DateTime.UtcNow
        };
        db.QBSyncQueue.Add(queueItem);
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        var authResult = svc.authenticate("admin", "pass");
        var ticket = authResult[0];
        svc.sendRequestXML(ticket, "", "", "", 13, 0);

        var responseXml = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <InvoiceAddRs requestID=""2"" statusCode=""0"" statusSeverity=""Info"" statusMessage=""Status OK"">
      <InvoiceRet>
        <TxnID>TXN-INV-8888</TxnID>
        <EditSequence>98765432</EditSequence>
        <RefNumber>INV-100</RefNumber>
      </InvoiceRet>
    </InvoiceAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        // Act
        var progress = svc.receiveResponseXML(ticket, responseXml, "", "");

        // Assert
        Assert.Equal(100, progress);

        var updatedInvoice = db.CustomerInvoice.Find(20);
        Assert.NotNull(updatedInvoice);
        Assert.Equal("TXN-INV-8888", updatedInvoice.QBInvoiceTxnID);
        Assert.Equal("98765432", updatedInvoice.QBEditSequence);
    }

    [Fact]
    public void ReceiveResponseXML_FatalError_ShouldSetFailedFatalStatus()
    {
        // Arrange
        using var db = GetDbContext(nameof(ReceiveResponseXML_FatalError_ShouldSetFailedFatalStatus));
        var customer = new Customer
        {
            CustomerId = 30,
            CompanyName = "Invalid Customer Corp"
        };
        db.Customer.Add(customer);

        var queueItem = new QBSyncQueue
        {
            SyncId = 3,
            EntityType = "Customer",
            EntityId = 30,
            SyncDirection = "ToQB",
            SyncAction = "Create",
            Status = "Pending",
            MaxRetries = 3,
            RetryCount = 0,
            CreatedDate = DateTime.UtcNow
        };
        db.QBSyncQueue.Add(queueItem);
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        var authResult = svc.authenticate("admin", "pass");
        var ticket = authResult[0];
        svc.sendRequestXML(ticket, "", "", "", 13, 0);

        var responseXml = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <CustomerAddRs requestID=""3"" statusCode=""3000"" statusSeverity=""Error"" statusMessage=""The string provided is too long."">
    </CustomerAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        // Act
        svc.receiveResponseXML(ticket, responseXml, "", "");

        // Assert
        var dbQueueItem = db.QBSyncQueue.Find(3);
        Assert.NotNull(dbQueueItem);
        Assert.Equal("FailedFatal", dbQueueItem.Status);
        Assert.Equal(1, dbQueueItem.RetryCount);
        Assert.Contains("3000", dbQueueItem.ErrorMessage);
    }
}
