using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class AIGMProfile
{
    public string TeamId { get; set; } = string.Empty;
    public AIStrategy Strategy { get; set; }
    public float RiskTolerance { get; set; }
    public float DraftPreference { get; set; }
    public float FreeAgencyAggression { get; set; }
    public float TradeFrequency { get; set; }
    public int CompetitiveWindowYears { get; set; }
}
