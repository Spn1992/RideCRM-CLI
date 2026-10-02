namespace AmusementRideCRM.Models;

public class AuditLog
{
    public string AuditLogId { get; set; }
    public DateTime AuditDate { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string ActionType { get; set; }
    public string EntityName { get; set; }
    public string EntityId { get; set; }
    public string? PropertyName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Description { get; set; }
    public string? RelatedEntity { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? IpAddress { get; set; }
    public Guid? CorrelationId { get; set; }
}
