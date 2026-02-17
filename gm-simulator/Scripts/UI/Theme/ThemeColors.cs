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
}
