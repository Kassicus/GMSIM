namespace GMSimulator.Models;

public class Season
{
    public int Year { get; set; }
    public List<Game> Games { get; set; } = new();
    public string? ChampionTeamId { get; set; }
}
