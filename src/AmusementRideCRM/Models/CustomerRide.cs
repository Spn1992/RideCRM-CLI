namespace AmusementRideCRM.Models;

public class CustomerRide
{
    public int RideId { get; set; }
    public int CustomerId { get; set; }
    public string RideManufacturer { get; set; }
    public string RideModel { get; set; }
    public string? SerialNumber { get; set; }
    public int? YearManufactured { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
