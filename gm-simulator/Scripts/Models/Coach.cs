using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class Coach
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public int Age { get; set; }
    public CoachRole Role { get; set; }
    public string? TeamId { get; set; }

    // Ratings 0-99
    public int OffenseRating { get; set; }
    public int DefenseRating { get; set; }
    public int SpecialTeamsRating { get; set; }
    public int GameManagement { get; set; }
    public int PlayerDevelopment { get; set; }
    public int Motivation { get; set; }
    public int Adaptability { get; set; }
    public int Recruiting { get; set; }

    // Scheme Preferences
    public SchemeType PreferredOffense { get; set; }
    public SchemeType PreferredDefense { get; set; }

    // Personality
    public CoachPersonality Personality { get; set; }
    public int Prestige { get; set; }
    public int Experience { get; set; }

    // Track Record
    public int CareerWins { get; set; }
    public int CareerLosses { get; set; }
    public int PlayoffAppearances { get; set; }
    public int SuperBowlWins { get; set; }
}
