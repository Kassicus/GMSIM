namespace GMSimulator.Models;

/// <summary>
/// Transient output of SimulationEngine.SimulateGame().
/// Carries all generated data before it gets written back to Game / Player / Team objects.
/// </summary>
public class GameResult
{
    public string GameId { get; set; } = string.Empty;

    // Score
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int[] HomeQuarterScores { get; set; } = new int[4];
    public int[] AwayQuarterScores { get; set; } = new int[4];

    // Team stats
    public TeamGameStats HomeTeamStats { get; set; } = new();
    public TeamGameStats AwayTeamStats { get; set; } = new();

    // Per-player stat lines keyed by playerId
    public Dictionary<string, PlayerGameStats> PlayerStats { get; set; } = new();

    // Injuries that occurred during this game
    public List<GameInjuryEvent> Injuries { get; set; } = new();

    // Narrative
    public List<string> KeyPlays { get; set; } = new();
    public string? PlayerOfTheGameId { get; set; }
    public string? PlayerOfTheGameLine { get; set; }
}

public class TeamGameStats
{
    public int TotalYards { get; set; }
    public int PassingYards { get; set; }
    public int RushingYards { get; set; }
    public int Turnovers { get; set; }
    public int FirstDowns { get; set; }
    public int ThirdDownConversions { get; set; }
    public int ThirdDownAttempts { get; set; }
    public int Penalties { get; set; }
    public int PenaltyYards { get; set; }
    public int TimeOfPossessionSeconds { get; set; }
    public int Sacks { get; set; }
    public int SackYards { get; set; }
}

/// <summary>
/// Individual player stat line for a single game.
/// Only populated fields are non-zero; position determines which fields are relevant.
/// </summary>
public class PlayerGameStats
{
    // Passing
    public int Completions { get; set; }
    public int Attempts { get; set; }
    public int PassingYards { get; set; }
    public int PassingTDs { get; set; }
    public int Interceptions { get; set; }
    public int Sacked { get; set; }

    // Rushing
    public int RushAttempts { get; set; }
    public int RushingYards { get; set; }
    public int RushingTDs { get; set; }
    public int Fumbles { get; set; }
    public int FumblesLost { get; set; }

    // Receiving
    public int Targets { get; set; }
    public int Receptions { get; set; }
    public int ReceivingYards { get; set; }
    public int ReceivingTDs { get; set; }

    // Defense
    public int TotalTackles { get; set; }
    public int SoloTackles { get; set; }
    public float Sacks { get; set; }
    public int TacklesForLoss { get; set; }
    public int QBHits { get; set; }
    public int ForcedFumbles { get; set; }
    public int FumbleRecoveries { get; set; }
    public int InterceptionsDef { get; set; }
    public int PassesDefended { get; set; }
    public int DefensiveTDs { get; set; }

    // Kicking
    public int FGMade { get; set; }
    public int FGAttempted { get; set; }
    public int XPMade { get; set; }
    public int XPAttempted { get; set; }

    // Punting
    public int Punts { get; set; }
    public int PuntYards { get; set; }
}

public class GameInjuryEvent
{
    public string PlayerId { get; set; } = string.Empty;
    public string InjuryType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int WeeksOut { get; set; }
    public bool CanReturn { get; set; } = true;
}
