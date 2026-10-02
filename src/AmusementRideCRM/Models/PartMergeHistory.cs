namespace AmusementRideCRM.Models;

public class PartMergeHistory
{
    public int MergeId { get; set; }
    public int SurvivorPartId { get; set; }
    public int MergedPartId { get; set; }
    public DateTime MergedDate { get; set; }
    public int? MergedByUserId { get; set; }
    public string? MergedByUserName { get; set; }
    public string? MergeDetails { get; set; }
    public string? Notes { get; set; }
}
