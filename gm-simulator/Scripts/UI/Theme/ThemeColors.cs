using Godot;
using GMSimulator.Models.Enums;

namespace GMSimulator.UI.Theme;

public static class ThemeColors
{
    // === BACKGROUNDS ===
    public static readonly Color BgDeep         = new("#0D1117");
    public static readonly Color BgBase         = new("#161B22");
    public static readonly Color BgSurface      = new("#1C2128");
    public static readonly Color BgSurfaceHover = new("#21262D");
    public static readonly Color BgElevated     = new("#282E36");
    public static readonly Color BgOverlay      = new("#30363D");

    // === BORDERS ===
    public static readonly Color Border       = new("#30363D");
    public static readonly Color BorderMuted  = new("#21262D");
    public static readonly Color BorderBright = new("#484F58");

    // === TEXT ===
    public static readonly Color TextPrimary     = new("#F0F6FC");
    public static readonly Color TextSecondary   = new("#8B949E");
    public static readonly Color TextTertiary    = new("#6E7681");
    public static readonly Color TextPlaceholder = new("#484F58");

    // === ACCENT (NFL Shield Blue) ===
    public static readonly Color Accent      = new("#1A7FD4");
    public static readonly Color AccentHover = new("#2196F3");
    public static readonly Color AccentMuted = new("#0D3A66");
    public static readonly Color AccentText  = new("#58A6FF");

    // === SEMANTIC STATUS ===
    public static readonly Color Success      = new("#3FB950");
    public static readonly Color SuccessMuted = new("#0D2818");
    public static readonly Color Warning      = new("#D29922");
    public static readonly Color WarningMuted = new("#2D2206");
    public static readonly Color Danger       = new("#F85149");
    public static readonly Color DangerMuted  = new("#3D1117");
    public static readonly Color Info         = new("#58A6FF");
    public static readonly Color InfoMuted    = new("#0C2D5E");

    // === OVERALL RATINGS ===
    public static readonly Color RatingElite   = new("#FFD700");   // 90+
    public static readonly Color RatingGreat   = new("#3FB950");   // 80-89
    public static readonly Color RatingGood    = new("#58A6FF");   // 70-79
    public static readonly Color RatingAverage = new("#D29922");   // 60-69
    public static readonly Color RatingPoor    = new("#F85149");   // <60

    // === SCOUTING GRADES ===
    public static readonly Color GradeFullyScouted = new("#3FB950");
    public static readonly Color GradeAdvanced     = new("#58A6FF");
    public static readonly Color GradeIntermediate = new("#D29922");
    public static readonly Color GradeInitial      = new("#E08A39");
    public static readonly Color GradeUnscouted    = new("#6E7681");

    // === TRANSACTION TYPES ===
    public static readonly Color TxnSigned  = new("#3FB950");
    public static readonly Color TxnCut     = new("#F85149");
    public static readonly Color TxnTraded  = new("#58A6FF");
    public static readonly Color TxnDrafted = new("#FFD700");
    public static readonly Color TxnInjured = new("#E08A39");
    public static readonly Color TxnNeutral = new("#8B949E");

    // === NOTIFICATION BACKGROUNDS ===
    public static readonly Color NotifAlertBg   = new(0.24f, 0.07f, 0.09f, 0.95f);
    public static readonly Color NotifAwardBg   = new(0.18f, 0.13f, 0.03f, 0.95f);
    public static readonly Color NotifInfoBg    = new(0.05f, 0.11f, 0.24f, 0.95f);
    public static readonly Color NotifDefaultBg = new(0.11f, 0.12f, 0.14f, 0.95f);

    // === PHASE ACCENT COLORS ===
    public static readonly Color PhasePostSeason     = new("#8B949E"); // Gray
    public static readonly Color PhaseCombine        = new("#A371F7"); // Purple
    public static readonly Color PhaseFreeAgency     = new("#3FB950"); // Green
    public static readonly Color PhasePreDraft       = new("#D29922"); // Amber
    public static readonly Color PhaseDraft          = new("#F78166"); // Orange
    public static readonly Color PhasePostDraft      = new("#58A6FF"); // Blue
    public static readonly Color PhasePreseason      = new("#79C0FF"); // Light blue
    public static readonly Color PhaseRegularSeason  = new("#1A7FD4"); // NFL Shield Blue
    public static readonly Color PhasePlayoffs       = new("#FFD700"); // Gold
    public static readonly Color PhaseSuperBowl      = new("#FFD700"); // Gold

    // Phase muted backgrounds
    public static readonly Color PhasePostSeasonBg     = new("#1C2128");
    public static readonly Color PhaseCombineBg        = new("#1A1530");
    public static readonly Color PhaseFreeAgencyBg     = new("#0D2818");
    public static readonly Color PhasePreDraftBg       = new("#2D2206");
    public static readonly Color PhaseDraftBg          = new("#2D1508");
    public static readonly Color PhasePostDraftBg      = new("#0C2D5E");
    public static readonly Color PhasePreseasonBg      = new("#0D2040");
    public static readonly Color PhaseRegularSeasonBg  = new("#0A2E52");
    public static readonly Color PhasePlayoffsBg       = new("#2D2800");
    public static readonly Color PhaseSuperBowlBg      = new("#2D2800");

    // === HIGHLIGHTS ===
    public static readonly Color PlayerHighlight = new(0.06f, 0.18f, 0.37f, 0.6f);

    // === NAV BAR ===
    public static readonly Color NavBg             = new("#0D1117");
    public static readonly Color NavItemActive     = new("#1A7FD4");
    public static readonly Color NavItemText       = new("#8B949E");
    public static readonly Color NavItemActiveText = new("#F0F6FC");

    // === TOP BAR ===
    public static readonly Color TopBarBg = new("#161B22");

    // === HELPERS ===

    public static Color GetRatingColor(int overall) => overall switch
    {
        >= 90 => RatingElite,
        >= 80 => RatingGreat,
        >= 70 => RatingGood,
        >= 60 => RatingAverage,
        _     => RatingPoor,
    };

    public static Color GetScoutGradeColor(ScoutingGrade grade) => grade switch
    {
        ScoutingGrade.FullyScouted  => GradeFullyScouted,
        ScoutingGrade.Advanced      => GradeAdvanced,
        ScoutingGrade.Intermediate  => GradeIntermediate,
        ScoutingGrade.Initial       => GradeInitial,
        _                           => GradeUnscouted,
    };

    public static Color GetTransactionColor(TransactionType type) => type switch
    {
        TransactionType.Signed or TransactionType.Extended or TransactionType.Restructured
            or TransactionType.Tagged or TransactionType.Claimed or TransactionType.Promoted => TxnSigned,
        TransactionType.Cut or TransactionType.Retired or TransactionType.ContractExpired
            or TransactionType.Demoted => TxnCut,
        TransactionType.Traded  => TxnTraded,
        TransactionType.Drafted => TxnDrafted,
        TransactionType.Injured => TxnInjured,
        _                       => TxnNeutral,
    };

    public static Color GetPhaseAccentColor(GamePhase phase) => phase switch
    {
        GamePhase.PostSeason      => PhasePostSeason,
        GamePhase.CombineScouting => PhaseCombine,
        GamePhase.FreeAgency      => PhaseFreeAgency,
        GamePhase.PreDraft        => PhasePreDraft,
        GamePhase.Draft           => PhaseDraft,
        GamePhase.PostDraft       => PhasePostDraft,
        GamePhase.Preseason       => PhasePreseason,
        GamePhase.RegularSeason   => PhaseRegularSeason,
        GamePhase.Playoffs        => PhasePlayoffs,
        GamePhase.SuperBowl       => PhaseSuperBowl,
        _                         => Accent,
    };

    public static Color GetPhaseAccentBg(GamePhase phase) => phase switch
    {
        GamePhase.PostSeason      => PhasePostSeasonBg,
        GamePhase.CombineScouting => PhaseCombineBg,
        GamePhase.FreeAgency      => PhaseFreeAgencyBg,
        GamePhase.PreDraft        => PhasePreDraftBg,
        GamePhase.Draft           => PhaseDraftBg,
        GamePhase.PostDraft       => PhasePostDraftBg,
        GamePhase.Preseason       => PhasePreseasonBg,
        GamePhase.RegularSeason   => PhaseRegularSeasonBg,
        GamePhase.Playoffs        => PhasePlayoffsBg,
        GamePhase.SuperBowl       => PhaseSuperBowlBg,
        _                         => BgSurface,
    };
}
