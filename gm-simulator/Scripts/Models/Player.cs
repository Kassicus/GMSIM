using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class Player
{
    // Identity
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public int Age { get; set; }
    public int YearsInLeague { get; set; }
    public string College { get; set; } = string.Empty;
    public int DraftYear { get; set; }
    public int DraftRound { get; set; }
    public int DraftPick { get; set; }
    public bool IsUndrafted { get; set; }
    public int HeightInches { get; set; }
    public int WeightLbs { get; set; }

    // Football
    public Position Position { get; set; }
    public Archetype Archetype { get; set; }
    public int Overall { get; set; }
    public int PotentialCeiling { get; set; }
    public PlayerAttributes Attributes { get; set; } = new();
    public PlayerTraits Traits { get; set; } = new();

    // Status
    public string? TeamId { get; set; }
    public Contract? CurrentContract { get; set; }
    public Injury? CurrentInjury { get; set; }
    public RosterStatus RosterStatus { get; set; }
    public int Morale { get; set; } = 75;
    public int Fatigue { get; set; }

    // Career Stats
    public Dictionary<int, SeasonStats> CareerStats { get; set; } = new();

    // Progression
    public DevelopmentTrait DevTrait { get; set; }
    public int TrajectoryModifier { get; set; }
}
