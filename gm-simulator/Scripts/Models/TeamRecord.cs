namespace GMSimulator.Models;

public class TeamRecord
{
    public int Season { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Ties { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }
    public int DivisionRank { get; set; }
    public bool MadePlayoffs { get; set; }
    public string? PlayoffResult { get; set; }
}
