using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class Prospect
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public int Age { get; set; }
    public string College { get; set; } = string.Empty;
    public Position Position { get; set; }
    public Archetype Archetype { get; set; }
    public int HeightInches { get; set; }
    public int WeightLbs { get; set; }

    // True ratings (hidden from player until scouted)
    public PlayerAttributes TrueAttributes { get; set; } = new();
    public int TruePotential { get; set; }
    public DevelopmentTrait TrueDevTrait { get; set; }
    public PlayerTraits TrueTraits { get; set; } = new();

    // Scouted ratings (what the player sees)
    public PlayerAttributes? ScoutedAttributes { get; set; }
    public int? ScoutedPotential { get; set; }
    public float ScoutingProgress { get; set; }
    public ScoutingGrade ScoutGrade { get; set; }

    // Combine / Pro Day
    public CombineResults? CombineResults { get; set; }
    public bool AttendedCombine { get; set; }
    public bool HadProDay { get; set; }

    // Draft
    public int ProjectedRound { get; set; }
    public float DraftValue { get; set; }
    public List<string> RedFlags { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();

    // After Draft
    public bool IsDrafted { get; set; }
    public int? DraftedRound { get; set; }
    public int? DraftedPick { get; set; }
    public string? DraftedByTeamId { get; set; }
}
