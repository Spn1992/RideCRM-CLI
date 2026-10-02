namespace AmusementRideCRM.Models;

public class NotificationQueue
{
    public int NotificationId { get; set; }
    public int? RecipientUserId { get; set; }
    public string? RecipientRole { get; set; }
    public string NotificationType { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string? RelatedEntity { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
