namespace GMSimulator.Models;

public class SeasonAwards
{
    public int Year { get; set; }
    public string? MvpId { get; set; }
    public string? DpoyId { get; set; }
    public string? OroyId { get; set; }
    public string? DroyId { get; set; }
    public List<string> FirstTeamAllPro { get; set; } = new();
    public List<string> SecondTeamAllPro { get; set; } = new();
    public List<string> ProBowlIds { get; set; } = new();
}
