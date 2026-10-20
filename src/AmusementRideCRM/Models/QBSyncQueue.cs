namespace AmusementRideCRM.Models;

public class QBSyncQueue
{
    public int SyncId { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string SyncDirection { get; set; } = "ToQB";
    public string SyncAction { get; set; } = "Create";
    public string Status { get; set; } = "Pending";
    public string? QBTxnID { get; set; }
    public string? QBEditSequence { get; set; }
    public string? QBRequestXml { get; set; }
    public string? QBResponseXml { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public int Priority { get; set; } = 0;
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public byte[] RowVersion { get; set; }
}
