namespace GMSimulator.Models;

public class ScoutAssignment
{
    public string ScoutId { get; set; } = string.Empty;
    public string ProspectId { get; set; } = string.Empty;
    public int WeeksAssigned { get; set; }
    public int StartWeek { get; set; }
    public int StartYear { get; set; }
}
