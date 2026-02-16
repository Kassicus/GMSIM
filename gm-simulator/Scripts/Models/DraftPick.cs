namespace GMSimulator.Models;

public class DraftPick
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Year { get; set; }
    public int Round { get; set; }
    public string OriginalTeamId { get; set; } = string.Empty;
    public string CurrentTeamId { get; set; } = string.Empty;
    public int? OverallNumber { get; set; }
    public bool IsCompensatory { get; set; }
    public bool IsConditional { get; set; }
    public string? Condition { get; set; }
    public string? SelectedPlayerId { get; set; }
    public bool IsUsed { get; set; }
}
