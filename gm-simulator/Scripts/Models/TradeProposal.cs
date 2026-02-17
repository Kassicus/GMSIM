using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public enum TradeStatus { Pending, Accepted, Rejected, Expired }

public class TradeProposal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Teams involved
    public string ProposingTeamId { get; set; } = string.Empty;
    public string ReceivingTeamId { get; set; } = string.Empty;

    // Assets offered by proposing team
    public List<string> ProposingPlayerIds { get; set; } = new();
    public List<string> ProposingPickIds { get; set; } = new();

    // Assets offered by receiving team
    public List<string> ReceivingPlayerIds { get; set; } = new();
    public List<string> ReceivingPickIds { get; set; } = new();

    // Value assessment (trade chart points)
    public int ProposingValuePoints { get; set; }
    public int ReceivingValuePoints { get; set; }

    // Cap impact (cents: positive = cap savings, negative = cap cost)
    public long ProposingCapImpact { get; set; }
    public long ReceivingCapImpact { get; set; }

    // Status
    public TradeStatus Status { get; set; } = TradeStatus.Pending;
    public string? RejectionReason { get; set; }
    public int ProposedYear { get; set; }
    public int ProposedWeek { get; set; }
    public GamePhase ProposedPhase { get; set; }

    // AI-initiated flag
    public bool IsAIInitiated { get; set; }
}
