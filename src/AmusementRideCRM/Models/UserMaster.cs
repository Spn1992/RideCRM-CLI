namespace AmusementRideCRM.Models;

public class UserMaster
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string NormalizedUserName { get; set; }
    public string Email { get; set; }
    public string NormalizedEmail { get; set; }
    public string? PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string RoleCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public string? LockoutEnd { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? PasswordChangedDate { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
