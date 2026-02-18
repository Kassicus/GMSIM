using Godot;

namespace GMSimulator.Core;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; } = null!;

    // Calendar
    [Signal] public delegate void PhaseChangedEventHandler(int phase);
    [Signal] public delegate void WeekAdvancedEventHandler(int year, int week);
    [Signal] public delegate void SeasonStartedEventHandler(int year);
    [Signal] public delegate void SeasonEndedEventHandler(int year);

    // Roster
    [Signal] public delegate void PlayerSignedEventHandler(string playerId, string teamId);
    [Signal] public delegate void PlayerCutEventHandler(string playerId, string teamId);
    [Signal] public delegate void PlayerTradedEventHandler(string playerId, string fromTeamId, string toTeamId);
    [Signal] public delegate void PlayerInjuredEventHandler(string playerId, string injuryType, int weeksOut);
    [Signal] public delegate void PlayerRetiredEventHandler(string playerId);
    [Signal] public delegate void DepthChartChangedEventHandler(string teamId);

    // Draft
    [Signal] public delegate void DraftPickMadeEventHandler(int round, int pick, string prospectId, string teamId);
    [Signal] public delegate void DraftStartedEventHandler(int year);
    [Signal] public delegate void DraftCompletedEventHandler(int year);

    // Free Agency
    [Signal] public delegate void FreeAgencyOpenedEventHandler(int year);
    [Signal] public delegate void FranchiseTagAppliedEventHandler(string playerId, string teamId);
    [Signal] public delegate void FreeAgentSignedEventHandler(string playerId, string teamId, int years, long totalValue);
    [Signal] public delegate void ContractExtendedEventHandler(string playerId, string teamId);
    [Signal] public delegate void FreeAgencyWeekProcessedEventHandler(int week);
    [Signal] public delegate void CompensatoryPicksAwardedEventHandler();

    // Scouting & Draft
    [Signal] public delegate void ProspectScoutedEventHandler(string prospectId, int gradeLevel);
[Signal] public delegate void UDFASignedEventHandler(string prospectId, string teamId);

    // Trades
    [Signal] public delegate void TradeProposedEventHandler(string fromTeamId, string toTeamId);
    [Signal] public delegate void TradeAcceptedEventHandler(string tradeId);
    [Signal] public delegate void TradeRejectedEventHandler(string tradeId);

    // Staff
    [Signal] public delegate void CoachHiredEventHandler(string coachId, string teamId, int role);
    [Signal] public delegate void CoachFiredEventHandler(string coachId, string teamId);
    [Signal] public delegate void CoachingCarouselCompletedEventHandler(int year);

    // Progression & AI (Phase 8)
    [Signal] public delegate void ProgressionCompletedEventHandler(int year);
    [Signal] public delegate void AwardsCalculatedEventHandler(int year);
    [Signal] public delegate void OwnerPatienceLowEventHandler(string teamId, int patience);

    // Game
    [Signal] public delegate void GameCompletedEventHandler(string gameId);
    [Signal] public delegate void PlayoffTeamsSetEventHandler();
    [Signal] public delegate void SuperBowlCompletedEventHandler(string winnerTeamId);
    [Signal] public delegate void ScheduleGeneratedEventHandler(int season);
    [Signal] public delegate void WeekSimulatedEventHandler(int year, int week);
    [Signal] public delegate void PlayoffRoundCompletedEventHandler(int round);

    // UI
    [Signal] public delegate void NotificationCreatedEventHandler(string title, string message, int priority);
    [Signal] public delegate void PlayerSelectedEventHandler(string playerId);
    [Signal] public delegate void TeamSelectedEventHandler(string teamId);
    [Signal] public delegate void NavigationRequestedEventHandler(string screenName);

    public override void _Ready()
    {
        Instance = this;
    }
}
