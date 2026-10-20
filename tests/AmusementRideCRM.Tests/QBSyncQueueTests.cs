namespace AmusementRideCRM.Tests;

using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using Xunit;

public class QBSyncQueueTests
{
    private ApplicationDbContext GetDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task EnqueueSyncItemAsync_ShouldCreatePendingRecord()
    {
        // Arrange
        using var db = GetDbContext(nameof(EnqueueSyncItemAsync_ShouldCreatePendingRecord));
        var queueSvc = new QBSyncQueueService(db);

        // Act
        var item = await queueSvc.EnqueueSyncItemAsync("VendorInvoice", 5, "Create", "ToQB", priority: 10);

        // Assert
        Assert.NotNull(item);
        Assert.True(item.SyncId > 0);
        Assert.Equal("Pending", item.Status);
        Assert.Equal("VendorInvoice", item.EntityType);
        Assert.Equal(5, item.EntityId);
        Assert.Equal(10, item.Priority);
    }

    [Fact]
    public async Task PriorityOrdering_ShouldReturnHigherPriorityItemFirst()
    {
        // Arrange
        using var db = GetDbContext(nameof(PriorityOrdering_ShouldReturnHigherPriorityItemFirst));
        var queueSvc = new QBSyncQueueService(db);

        await queueSvc.EnqueueSyncItemAsync("Customer", 1, priority: 1);
        await queueSvc.EnqueueSyncItemAsync("CustomerInvoice", 2, priority: 10);
        await queueSvc.EnqueueSyncItemAsync("VendorInvoice", 3, priority: 5);

        // Act
        var nextItem = await queueSvc.GetNextPendingItemAsync();

        // Assert
        Assert.NotNull(nextItem);
        Assert.Equal("CustomerInvoice", nextItem.EntityType);
        Assert.Equal(2, nextItem.EntityId);
        Assert.Equal(10, nextItem.Priority);
    }

    [Fact]
    public void VendorBill_FullSyncFlow_ShouldUpdateVendorInvoiceRecord()
    {
        // Arrange
        using var db = GetDbContext(nameof(VendorBill_FullSyncFlow_ShouldUpdateVendorInvoiceRecord));
        var vendor = new VendorMaster
        {
            VendorId = 5,
            VendorName = "Super Coasters Inc",
            QBVendorListID = "80000005-12345678"
        };
        db.VendorMaster.Add(vendor);

        var bill = new VendorInvoice
        {
            VendorInvoiceId = 15,
            InvoiceNumber = "V-BILL-2026",
            VendorId = 5,
            InvoiceDate = DateTime.Today,
            TotalEur = 5000.00m
        };
        db.VendorInvoice.Add(bill);

        var billLine = new VendorInvoiceLine
        {
            VILineId = 1,
            VendorInvoiceId = 15,
            LineNumber = 1,
            VendorPartNumber = "HYD-VALVE",
            Description = "Hydraulic Brake Valve",
            Quantity = 2,
            UnitCostEur = 2500.00m
        };
        db.VendorInvoiceLine.Add(billLine);

        var queueItem = new QBSyncQueue
        {
            SyncId = 50,
            EntityType = "VendorBill",
            EntityId = 15,
            SyncDirection = "ToQB",
            SyncAction = "Create",
            Status = "Pending",
            Priority = 5
        };
        db.QBSyncQueue.Add(queueItem);
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        var auth = svc.authenticate("admin", "pass");
        var ticket = auth[0];

        // Act 1: send request
        var reqXml = svc.sendRequestXML(ticket, "", "", "", 13, 0);
        Assert.Contains("BillAddRq", reqXml);
        Assert.Contains("HYD-VALVE", reqXml);

        // Act 2: receive response
        var respXml = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <BillAddRs requestID=""50"" statusCode=""0"" statusSeverity=""Info"" statusMessage=""Status OK"">
      <BillRet>
        <TxnID>TXN-BILL-7777</TxnID>
        <EditSequence>11223344</EditSequence>
        <RefNumber>V-BILL-2026</RefNumber>
      </BillRet>
    </BillAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        var progress = svc.receiveResponseXML(ticket, respXml, "", "");

        // Assert
        Assert.Equal(100, progress);

        var updatedBill = db.VendorInvoice.Find(15);
        Assert.NotNull(updatedBill);
        Assert.Equal("TXN-BILL-7777", updatedBill.QBBillTxnID);
        Assert.Equal("11223344", updatedBill.QBBillEditSequence);

        var updatedQueue = db.QBSyncQueue.Find(50);
        Assert.NotNull(updatedQueue);
        Assert.Equal("Success", updatedQueue.Status);
    }

    [Fact]
    public async Task RetryableError_ShouldIncrementRetryCount_AndAllowRetry()
    {
        // Arrange
        using var db = GetDbContext(nameof(RetryableError_ShouldIncrementRetryCount_AndAllowRetry));
        var customer = new Customer { CustomerId = 1, CompanyName = "Retry Park" };
        db.Customer.Add(customer);

        var queueItem = new QBSyncQueue
        {
            SyncId = 100,
            EntityType = "Customer",
            EntityId = 1,
            Status = "Pending",
            MaxRetries = 3,
            RetryCount = 0
        };
        db.QBSyncQueue.Add(queueItem);
        db.SaveChanges();

        var serializer = new QbXmlSerializer();
        var sessionMgr = new QBSessionManager();
        var queueSvc = new QBSyncQueueService(db);
        var svc = new QBWebConnectorSvc(db, serializer, queueSvc, sessionMgr);

        var auth = svc.authenticate("admin", "pass");
        var ticket = auth[0];

        svc.sendRequestXML(ticket, "", "", "", 13, 0);

        // Act: Temporary QB error (e.g. status code 500 or connection issue)
        var tempErrResp = @"<?xml version=""1.0""?>
<QBXML>
  <QBXMLMsgsRs>
    <CustomerAddRs requestID=""100"" statusCode=""500"" statusSeverity=""Error"" statusMessage=""QuickBooks is busy, try again later."">
    </CustomerAddRs>
  </QBXMLMsgsRs>
</QBXML>";

        svc.receiveResponseXML(ticket, tempErrResp, "", "");

        // Assert
        var updatedQueue = db.QBSyncQueue.Find(100);
        Assert.NotNull(updatedQueue);
        Assert.Equal("FailedRetryable", updatedQueue.Status);
        Assert.Equal(1, updatedQueue.RetryCount);

        // Verify that GetNextPendingItemAsync can retrieve it again for retry
        var retryItem = await queueSvc.GetNextPendingItemAsync();
        Assert.NotNull(retryItem);
        Assert.Equal(100, retryItem.SyncId);
    }
}
