using GMSimulator.Models.Enums;

namespace GMSimulator.Core;

public class CalendarSystem
{
    public int CurrentYear { get; set; } = 2025;
    public GamePhase CurrentPhase { get; set; } = GamePhase.PostSeason;
    public int CurrentWeek { get; set; } = 1;

    private static readonly Dictionary<GamePhase, int> PhaseDurations = new()
    {
        { GamePhase.PostSeason, 2 },
        { GamePhase.CombineScouting, 2 },
        { GamePhase.FreeAgency, 4 },
        { GamePhase.PreDraft, 2 },
        { GamePhase.Draft, 1 },
        { GamePhase.PostDraft, 3 },
        { GamePhase.Preseason, 4 },
        { GamePhase.RegularSeason, 18 },
        { GamePhase.Playoffs, 4 },
        { GamePhase.SuperBowl, 1 },
    };

    private static readonly GamePhase[] PhaseOrder =
    {
        GamePhase.PostSeason,
        GamePhase.CombineScouting,
        GamePhase.FreeAgency,
        GamePhase.PreDraft,
        GamePhase.Draft,
        GamePhase.PostDraft,
        GamePhase.Preseason,
        GamePhase.RegularSeason,
        GamePhase.Playoffs,
        GamePhase.SuperBowl,
    };

    public int GetPhaseDuration(GamePhase phase) =>
        PhaseDurations.GetValueOrDefault(phase, 1);

    public int GetTotalWeeksInPhase() => GetPhaseDuration(CurrentPhase);

    public string GetPhaseDisplayName()
    {
        return CurrentPhase switch
        {
            GamePhase.PostSeason => "Post Season",
            GamePhase.CombineScouting => "Combine & Scouting",
            GamePhase.FreeAgency => "Free Agency",
            GamePhase.PreDraft => "Pre-Draft",
            GamePhase.Draft => "NFL Draft",
            GamePhase.PostDraft => "Post-Draft / OTAs",
            GamePhase.Preseason => "Preseason",
            GamePhase.RegularSeason => "Regular Season",
            GamePhase.Playoffs => "Playoffs",
            GamePhase.SuperBowl => "Super Bowl",
            _ => CurrentPhase.ToString()
        };
    }

    public bool CanAdvance()
    {
        // In the future, this checks for blocking events requiring player input
        return true;
    }

    public AdvanceResult AdvanceWeek()
    {
        if (!CanAdvance())
            return new AdvanceResult(false, false, CurrentPhase);

        CurrentWeek++;

        if (CurrentWeek > GetPhaseDuration(CurrentPhase))
        {
            return AdvanceToNextPhase();
        }

        return new AdvanceResult(true, false, CurrentPhase);
    }

    public AdvanceResult AdvanceToNextPhase()
    {
        int currentIndex = Array.IndexOf(PhaseOrder, CurrentPhase);
        int nextIndex = currentIndex + 1;

        if (nextIndex >= PhaseOrder.Length)
        {
            // Wrap to PostSeason of next year
            CurrentYear++;
            CurrentPhase = GamePhase.PostSeason;
            CurrentWeek = 1;
            return new AdvanceResult(true, true, CurrentPhase);
        }

        CurrentPhase = PhaseOrder[nextIndex];
        CurrentWeek = 1;
        return new AdvanceResult(true, false, CurrentPhase);
    }

    public bool IsOffseason()
    {
        return CurrentPhase is GamePhase.PostSeason
            or GamePhase.CombineScouting
            or GamePhase.FreeAgency
            or GamePhase.PreDraft
            or GamePhase.Draft
            or GamePhase.PostDraft;
    }

    public bool IsRegularSeason() => CurrentPhase == GamePhase.RegularSeason;
    public bool IsPlayoffs() => CurrentPhase is GamePhase.Playoffs or GamePhase.SuperBowl;
}

public record AdvanceResult(bool Success, bool YearChanged, GamePhase NewPhase);
