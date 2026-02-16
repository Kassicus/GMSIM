namespace GMSimulator.Models;

public class SeasonStats
{
    public int Season { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesStarted { get; set; }

    // Passing
    public int Completions { get; set; }
    public int Attempts { get; set; }
    public int PassingYards { get; set; }
    public int PassingTDs { get; set; }
    public int Interceptions { get; set; }
    public float PasserRating { get; set; }
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
    public int Drops { get; set; }

    // Defense
    public int TotalTackles { get; set; }
    public int SoloTackles { get; set; }
    public int AssistedTackles { get; set; }
    public float Sacks { get; set; }
    public int TacklesForLoss { get; set; }
    public int QBHits { get; set; }
    public int ForcedFumbles { get; set; }
    public int FumbleRecoveries { get; set; }
    public int InterceptionsDef { get; set; }
    public int PassesDefended { get; set; }
    public int DefensiveTDs { get; set; }
    public int Safeties { get; set; }

    // Kicking
    public int FGMade { get; set; }
    public int FGAttempted { get; set; }
    public int FGLong { get; set; }
    public int XPMade { get; set; }
    public int XPAttempted { get; set; }

    // Punting
    public int Punts { get; set; }
    public float PuntAverage { get; set; }
    public int PuntsInside20 { get; set; }
    public int Touchbacks { get; set; }

    // Return
    public int KickReturns { get; set; }
    public int KickReturnYards { get; set; }
    public int KickReturnTDs { get; set; }
    public int PuntReturns { get; set; }
    public int PuntReturnYards { get; set; }
    public int PuntReturnTDs { get; set; }

    // Snap Counts
    public int OffensiveSnaps { get; set; }
    public int DefensiveSnaps { get; set; }
    public int SpecialTeamsSnaps { get; set; }
}
