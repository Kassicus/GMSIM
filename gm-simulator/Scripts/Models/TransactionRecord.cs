using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class TransactionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public TransactionType Type { get; set; }
    public string? PlayerId { get; set; }
    public string? TeamId { get; set; }
    public string? OtherTeamId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Week { get; set; }
    public GamePhase Phase { get; set; }
}
