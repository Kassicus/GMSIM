namespace GMSimulator.Models;

public class PlayerAttributes
{
    // Physical
    public int Speed { get; set; }
    public int Acceleration { get; set; }
    public int Agility { get; set; }
    public int Strength { get; set; }
    public int Jumping { get; set; }
    public int Stamina { get; set; }
    public int Toughness { get; set; }
    public int InjuryResistance { get; set; }

    // Passing (primarily QB)
    public int ThrowPower { get; set; }
    public int ShortAccuracy { get; set; }
    public int MediumAccuracy { get; set; }
    public int DeepAccuracy { get; set; }
    public int ThrowOnRun { get; set; }
    public int PlayAction { get; set; }

    // Rushing
    public int Carrying { get; set; }
    public int BallCarrierVision { get; set; }
    public int BreakTackle { get; set; }
    public int Trucking { get; set; }
    public int Elusiveness { get; set; }
    public int SpinMove { get; set; }
    public int JukeMove { get; set; }
    public int StiffArm { get; set; }

    // Receiving
    public int Catching { get; set; }
    public int CatchInTraffic { get; set; }
    public int SpectacularCatch { get; set; }
    public int RouteRunning { get; set; }
    public int Release { get; set; }

    // Blocking
    public int RunBlock { get; set; }
    public int PassBlock { get; set; }
    public int ImpactBlock { get; set; }
    public int LeadBlock { get; set; }

    // Defense
    public int Tackle { get; set; }
    public int HitPower { get; set; }
    public int PowerMoves { get; set; }
    public int FinesseMoves { get; set; }
    public int BlockShedding { get; set; }
    public int Pursuit { get; set; }
    public int PlayRecognition { get; set; }
    public int ManCoverage { get; set; }
    public int ZoneCoverage { get; set; }
    public int Press { get; set; }

    // Special Teams
    public int KickPower { get; set; }
    public int KickAccuracy { get; set; }

    // Mental
    public int Awareness { get; set; }
    public int Clutch { get; set; }
    public int Consistency { get; set; }
    public int Leadership { get; set; }
}
