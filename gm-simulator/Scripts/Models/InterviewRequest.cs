using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class InterviewRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RequestingTeamId { get; set; } = string.Empty;
    public string CoachId { get; set; } = string.Empty;
    public string CurrentTeamId { get; set; } = string.Empty;
    public CoachRole TargetRole { get; set; }
    public InterviewStatus Status { get; set; }
    public BlockReason BlockReason { get; set; }
    public int RequestWeek { get; set; }
    public string? Notes { get; set; }
}

public enum InterviewStatus { Pending, Approved, Blocked, Hired, Expired }
public enum BlockReason { None, LateralMove, PlannedPromotion }
