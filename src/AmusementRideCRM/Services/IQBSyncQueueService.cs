namespace AmusementRideCRM.Services;

using AmusementRideCRM.Models;

public interface IQBSyncQueueService
{
    Task<QBSyncQueue?> GetNextPendingItemAsync();
    Task MarkProcessingAsync(int syncId, string? requestXml = null);
    Task HandleSuccessAsync(int syncId, QbXmlResponseResult result, string rawResponseXml);
    Task HandleFailureAsync(int syncId, string errorMessage, string? rawResponseXml = null, bool isFatal = false);
    Task<QBSyncQueue> EnqueueSyncItemAsync(string entityType, int entityId, string syncAction = "Create", string syncDirection = "ToQB", int priority = 0);
    Task<QBSyncQueue?> GetByIdAsync(int syncId);
}
