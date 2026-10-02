namespace AmusementRideCRM.Models;

public class SystemConfiguration
{
    public int ConfigId { get; set; }
    public string ConfigKey { get; set; }
    public string ConfigValue { get; set; }
    public string DataType { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsEditable { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
