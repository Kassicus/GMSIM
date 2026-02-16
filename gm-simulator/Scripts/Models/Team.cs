using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class Team
{
    // Identity
    public string Id { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string FullName => $"{City} {Name}";
    public Conference Conference { get; set; }
    public Division Division { get; set; }

    // Branding (colors stored as hex strings for JSON serialization)
    public string PrimaryColorHex { get; set; } = "#000000";
    public string SecondaryColorHex { get; set; } = "#FFFFFF";
    public string LogoPath { get; set; } = string.Empty;

    // Roster
    public List<string> PlayerIds { get; set; } = new();
    public DepthChart DepthChart { get; set; } = new();
    public List<string> PracticeSquadIds { get; set; } = new();
    public List<string> IRPlayerIds { get; set; } = new();

    // Front Office
    public string? HeadCoachId { get; set; }
    public string? OffensiveCoordinatorId { get; set; }
    public string? DefensiveCoordinatorId { get; set; }
    public string? SpecialTeamsCoordId { get; set; }
    public List<string> PositionCoachIds { get; set; } = new();
    public List<string> ScoutIds { get; set; } = new();

    // Financials (all in cents)
    public long SalaryCap { get; set; }
    public long CurrentCapUsed { get; set; }
    public long DeadCapTotal { get; set; }
    public long CapSpace => SalaryCap - CurrentCapUsed;
    public long CarryoverCap { get; set; }

    // Draft Capital
    public List<DraftPick> DraftPicks { get; set; } = new();

    // Record
    public TeamRecord CurrentRecord { get; set; } = new();
    public List<TeamRecord> SeasonHistory { get; set; } = new();

    // Team Needs & Settings
    public SchemeType OffensiveScheme { get; set; }
    public SchemeType DefensiveScheme { get; set; }
    public List<Position> TeamNeeds { get; set; } = new();
    public int FanSatisfaction { get; set; } = 50;
    public int OwnerPatience { get; set; } = 50;
}
