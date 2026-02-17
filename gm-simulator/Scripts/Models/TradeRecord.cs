using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class TradeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Team1Id { get; set; } = string.Empty;
    public string Team2Id { get; set; } = string.Empty;

    // What team1 sent TO team2 (player/pick IDs)
    public List<string> Team1SentPlayerIds { get; set; } = new();
    public List<string> Team1SentPickIds { get; set; } = new();

    // What team2 sent TO team1
    public List<string> Team2SentPlayerIds { get; set; } = new();
    public List<string> Team2SentPickIds { get; set; } = new();

    // Snapshot names at trade time (players may move later)
    public List<string> Team1SentPlayerNames { get; set; } = new();
    public List<string> Team2SentPlayerNames { get; set; } = new();
    public List<string> Team1SentPickDescriptions { get; set; } = new();
    public List<string> Team2SentPickDescriptions { get; set; } = new();

    // Value assessment
    public int Team1ValueGiven { get; set; }
    public int Team2ValueGiven { get; set; }

    // When
    public int Year { get; set; }
    public int Week { get; set; }
    public GamePhase Phase { get; set; }
}
