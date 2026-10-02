namespace AmusementRideCRM.Models;

public class CustomerContact
{
    public int ContactId { get; set; }
    public int CustomerId { get; set; }
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobilePhone { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
