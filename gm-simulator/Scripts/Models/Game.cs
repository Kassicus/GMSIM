namespace GMSimulator.Models;

public class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Season { get; set; }
    public int Week { get; set; }
    public string HomeTeamId { get; set; } = string.Empty;
    public string AwayTeamId { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsPlayoff { get; set; }
    public string? Weather { get; set; }
    public string? PlayerOfTheGameId { get; set; }
}
