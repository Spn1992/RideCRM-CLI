namespace AmusementRideCRM.Models;

public class Part
{
    public int PartId { get; set; }
    public string PartNumber { get; set; }
    public string Description { get; set; }
    public string? DetailedDescription { get; set; }
    public string? CategoryCode { get; set; }
    public string UnitOfMeasure { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedQuantity { get; set; }
    public string? AvailableQuantity { get; set; }
    public decimal? ReorderPoint { get; set; }
    public decimal? ReorderQuantity { get; set; }
    public bool IsNonStocked { get; set; }
    public decimal? LastVendorCostEur { get; set; }
    public decimal? SellingPriceUsd { get; set; }
    public int? PrimaryLocationId { get; set; }
    public decimal? Weight { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsMerged { get; set; }
    public int? MergedIntoPartId { get; set; }
    public DateTime? MergedDate { get; set; }
    public string? MergedBy { get; set; }
    public string? QBItemListID { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; }
}
