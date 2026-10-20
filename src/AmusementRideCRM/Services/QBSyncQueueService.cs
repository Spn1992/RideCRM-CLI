namespace AmusementRideCRM.Services;

using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

public class QBSyncQueueService : IQBSyncQueueService
{
    private readonly ApplicationDbContext _dbContext;

    public QBSyncQueueService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<QBSyncQueue?> GetNextPendingItemAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbContext.QBSyncQueue
            .Where(q => (q.Status == "Pending" || q.Status == "FailedRetryable")
                        && q.RetryCount < (q.MaxRetries <= 0 ? 3 : q.MaxRetries)
                        && (!q.ScheduledDate.HasValue || q.ScheduledDate.Value <= now))
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.CreatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task MarkProcessingAsync(int syncId, string? requestXml = null)
    {
        var item = await _dbContext.QBSyncQueue.FindAsync(syncId);
        if (item == null) return;

        item.Status = "Processing";
        item.ModifiedDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(requestXml))
        {
            item.QBRequestXml = requestXml;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task HandleSuccessAsync(int syncId, QbXmlResponseResult result, string rawResponseXml)
    {
        var item = await _dbContext.QBSyncQueue.FindAsync(syncId);
        if (item == null) return;

        item.Status = "Success";
        item.QBTxnID = result.TxnOrListID;
        item.QBEditSequence = result.EditSequence;
        item.QBResponseXml = rawResponseXml;
        item.ProcessedDate = DateTime.UtcNow;
        item.ModifiedDate = DateTime.UtcNow;
        item.ErrorMessage = null;

        // Update linked domain record
        await UpdateLinkedDomainEntityAsync(item.EntityType, item.EntityId, result.TxnOrListID, result.EditSequence);

        await _dbContext.SaveChangesAsync();
    }

    public async Task HandleFailureAsync(int syncId, string errorMessage, string? rawResponseXml = null, bool isFatal = false)
    {
        var item = await _dbContext.QBSyncQueue.FindAsync(syncId);
        if (item == null) return;

        item.RetryCount++;
        item.ErrorMessage = errorMessage;
        if (!string.IsNullOrWhiteSpace(rawResponseXml))
        {
            item.QBResponseXml = rawResponseXml;
        }
        item.ModifiedDate = DateTime.UtcNow;

        if (isFatal || item.RetryCount >= item.MaxRetries)
        {
            item.Status = "FailedFatal";
        }
        else
        {
            item.Status = "FailedRetryable";
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<QBSyncQueue> EnqueueSyncItemAsync(string entityType, int entityId, string syncAction = "Create", string syncDirection = "ToQB", int priority = 0)
    {
        var item = new QBSyncQueue
        {
            EntityType = entityType,
            EntityId = entityId,
            SyncDirection = syncDirection,
            SyncAction = syncAction,
            Status = "Pending",
            Priority = priority,
            RetryCount = 0,
            MaxRetries = 3,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.QBSyncQueue.Add(item);
        await _dbContext.SaveChangesAsync();
        return item;
    }

    public async Task<QBSyncQueue?> GetByIdAsync(int syncId)
    {
        return await _dbContext.QBSyncQueue.FindAsync(syncId);
    }

    private async Task UpdateLinkedDomainEntityAsync(string entityType, int entityId, string? txnOrListId, string? editSequence)
    {
        if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0) return;

        var now = DateTime.UtcNow;

        switch (entityType.ToLowerInvariant())
        {
            case "customer":
                var customer = await _dbContext.Customer.FindAsync(entityId);
                if (customer != null)
                {
                    customer.QBCustomerListID = txnOrListId;
                    customer.QBEditSequence = editSequence;
                    customer.IsSyncedFromQB = true;
                    customer.ModifiedDate = now;
                }
                break;

            case "customerinvoice":
            case "invoice":
                var invoice = await _dbContext.CustomerInvoice.FindAsync(entityId);
                if (invoice != null)
                {
                    invoice.QBInvoiceTxnID = txnOrListId;
                    invoice.QBEditSequence = editSequence;
                    invoice.ModifiedDate = now;
                }
                break;

            case "vendorbill":
            case "vendorinvoice":
            case "bill":
                var vendorInvoice = await _dbContext.VendorInvoice.FindAsync(entityId);
                if (vendorInvoice != null)
                {
                    vendorInvoice.QBBillTxnID = txnOrListId;
                    vendorInvoice.QBBillEditSequence = editSequence;
                    vendorInvoice.ModifiedDate = now;
                }
                break;

            case "vendor":
                var vendor = await _dbContext.VendorMaster.FindAsync(entityId);
                if (vendor != null)
                {
                    vendor.QBVendorListID = txnOrListId;
                    vendor.ModifiedDate = now;
                }
                break;
        }
    }
}
