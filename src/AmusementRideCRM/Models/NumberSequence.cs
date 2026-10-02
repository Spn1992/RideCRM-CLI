namespace AmusementRideCRM.Models;

public class NumberSequence
{
    public string SequenceName { get; set; }
    public string Prefix { get; set; }
    public int CurrentValue { get; set; }
    public int IncrementBy { get; set; }
    public int PadWidth { get; set; }
    public string? Description { get; set; }
}
