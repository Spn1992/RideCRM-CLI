namespace AmusementRideCRM.Models;

public class FileAttachment
{
    public int AttachmentId { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string FileName { get; set; }
    public string? FileExtension { get; set; }
    public string? ContentType { get; set; }
    public string? FileSizeBytes { get; set; }
    public string StoragePath { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedDate { get; set; }
    public string? UploadedBy { get; set; }
    public bool IsActive { get; set; }
}
