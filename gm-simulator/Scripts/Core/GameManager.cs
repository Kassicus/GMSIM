using System.Text.Json;
using Godot;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.Systems;

namespace GMSimulator.Core;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    // Core State
    public CalendarSystem Calendar { get; private set; } = new();
    public string PlayerTeamId { get; set; } = string.Empty;
    public int Seed { get; private set; }
    public Random Rng { get; private set; } = new();

    // Entities
    public List<Team> Teams { get; private set; } = new();
    public List<Player> Players { get; private set; } = new();
    public List<Coach> Coaches { get; private set; } = new();
    public List<Scout> Scouts { get; private set; } = new();
    public List<Scout> ScoutMarket { get; set; } = new();
    public List<DraftPick> AllDraftPicks { get; private set; } = new();
    public List<Prospect> CurrentDraftClass { get; private set; } = new();
    public List<TransactionRecord> TransactionLog { get; private set; } = new();
    public List<Season> SeasonHistory { get; private set; } = new();
    public Dictionary<string, AIGMProfile> AIProfiles { get; private set; } = new();

    // Lookup caches
    private Dictionary<string, Player> _playerLookup = new();
    private Dictionary<string, Team> _teamLookup = new();
    private Dictionary<string, Coach> _coachLookup = new();

    // Generation
    private PlayerGenerator? _playerGenerator;
    private ProspectGenerator? _prospectGenerator;

    // Systems (Phase 2)
    public SalaryCapManager SalaryCapManager { get; private set; } = new();
    public RosterManager RosterManager { get; private set; } = null!;

    // Systems (Phase 3)
    public SimulationEngine SimEngine { get; private set; } = null!;
    public InjurySystem InjurySystem { get; private set; } = null!;

    // Systems (Phase 4)
    public FreeAgencySystem FreeAgency { get; private set; } = null!;
    public int FreeAgencyWeekNumber { get; private set; }

    // Systems (Phase 5)
    public ScoutingSystem Scouting { get; private set; } = null!;
    public DraftSystem Draft { get; private set; } = null!;
    public List<string> DraftBoardOrder { get; set; } = new();
    public Dictionary<string, int> DraftBoardTags { get; set; } = new();

    // Systems (Phase 6)
    public TradeSystem Trading { get; private set; } = null!;

    // Systems (Phase 7)
    public StaffSystem Staff { get; private set; } = null!;

    // Systems (Phase 8)
    public ProgressionSystem Progression { get; private set; } = null!;
    public AIGMController AIGM { get; private set; } = null!;
    public List<SeasonAwards> AllAwards { get; private set; } = new();
    public List<string> RetiredPlayerIds { get; private set; } = new();

    // Season State (Phase 3)
    public Season CurrentSeason { get; private set; } = new();
    public List<GameResult> RecentGameResults { get; private set; } = new();
    public List<PlayoffSeed> AFCPlayoffSeeds { get; private set; } = new();
    public List<PlayoffSeed> NFCPlayoffSeeds { get; private set; } = new();

    // Coaching Market State
    public HashSet<string> ActivePlayoffTeamIds { get; private set; } = new();
    public bool IsCoachingMarketOpen { get; private set; }
    public int CoachingMarketWeekNumber { get; private set; }

    public bool IsGameActive { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public void StartNewGame(string teamId, int seed)
    {
        Seed = seed;
        Rng = new Random(seed);
        PlayerTeamId = teamId;

        Calendar = new CalendarSystem
        {
            CurrentYear = 2026,
            CurrentPhase = GamePhase.PostSeason,
            CurrentWeek = 1
        };

        // Clear everything
        Teams.Clear();
        Players.Clear();
        Coaches.Clear();
        Scouts.Clear();
        ScoutMarket.Clear();
        AllDraftPicks.Clear();
        CurrentDraftClass.Clear();
        TransactionLog.Clear();
        SeasonHistory.Clear();
        AIProfiles.Clear();
        _playerLookup.Clear();
        _teamLookup.Clear();
        _coachLookup.Clear();
        CurrentSeason = new Season();
        RecentGameResults.Clear();
        AFCPlayoffSeeds.Clear();
        NFCPlayoffSeeds.Clear();
        ActivePlayoffTeamIds.Clear();
        IsCoachingMarketOpen = false;
        CoachingMarketWeekNumber = 0;

        // Load data and generate league
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        LoadTeams(dataPath);
        LoadHistoricalStandings(dataPath);
        InitializePlayerGenerator(dataPath);
        InitializeSystems(dataPath);
        GenerateAllRosters();
        GenerateCoachingStaffs();
        GenerateScouts();
        GenerateScoutMarket();
        GenerateDraftPicks();
        InitializeAIProfiles();
        CalculateAllTeamCaps();

        // Generate initial draft class so scouting is available from game start
        if (_prospectGenerator != null)
        {
            CurrentDraftClass = _prospectGenerator.GenerateDraftClass(Calendar.CurrentYear, Rng);
            GD.Print($"Generated {CurrentDraftClass.Count} draft prospects for {Calendar.CurrentYear}.");
            Scouting.InitializeForDraftCycle();
        }

        IsGameActive = true;

        GD.Print($"New game started. Team: {GetPlayerTeam()?.FullName}, Seed: {seed}");
        GD.Print($"Teams: {Teams.Count}, Players: {Players.Count}, Coaches: {Coaches.Count}");
    }

    public void LoadFromSave(SaveData save)
    {
        Seed = save.Seed;
        Rng = new Random(save.Seed);
        PlayerTeamId = save.PlayerTeamId;

        Calendar = new CalendarSystem
        {
            CurrentYear = save.CurrentYear,
            CurrentPhase = save.CurrentPhase,
            CurrentWeek = save.CurrentWeek
        };

        Teams = save.Teams;
        Players = save.Players;
        Coaches = save.Coaches;
        Scouts = save.Scouts;
        ScoutMarket = save.ScoutMarket;
        AllDraftPicks = save.AllDraftPicks;
        CurrentDraftClass = save.CurrentDraftClass;
        TransactionLog = save.TransactionLog;
        SeasonHistory = save.SeasonHistory;
        AIProfiles = save.AIProfiles;
        CurrentSeason = save.CurrentSeason ?? new Season();
        AFCPlayoffSeeds = save.AFCPlayoffSeeds ?? new List<PlayoffSeed>();
        NFCPlayoffSeeds = save.NFCPlayoffSeeds ?? new List<PlayoffSeed>();

        RebuildLookups();

        // Initialize systems for loaded save
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        InitializeSystems(dataPath);

        // Restore FA state
        FreeAgencyWeekNumber = save.FreeAgencyWeek;
        if (save.FreeAgentPool.Count > 0 || save.PendingOffers.Count > 0)
            FreeAgency.SetState(save.FreeAgentPool, save.PendingOffers, save.FreeAgencyWeek);

        // Restore scouting & draft state
        DraftBoardOrder = save.DraftBoardOrder;
        DraftBoardTags = save.DraftBoardTags;
        if (save.ScoutingWeeklyPool > 0 || save.ScoutingCurrentPoints > 0)
            Scouting.SetState(save.ScoutingWeeklyPool, save.ScoutingCurrentPoints);
        if (save.DraftCurrentRound > 0)
            Draft.SetState(save.DraftCurrentPick, save.DraftCurrentRound, save.DraftCurrentPick);

        // Restore trade state
        if (save.TradeHistory.Count > 0 || save.PendingTradeProposals.Count > 0
            || save.TradeBlockPlayerIds.Count > 0)
            Trading.SetState(save.TradeHistory, save.TradeBlockPlayerIds,
                save.PendingTradeProposals, save.TradeRelationships);

        // Restore coaching market state
        if (save.CoachingMarketIds.Count > 0 || save.InterviewRequests.Count > 0)
            Staff.SetState(save.CoachingMarketIds, save.InterviewRequests,
                save.PromotionIntentCoachIds, save.AIPromotionIntents);

        // Restore coaching market window state
        ActivePlayoffTeamIds = new HashSet<string>(save.ActivePlayoffTeamIds);
        IsCoachingMarketOpen = save.IsCoachingMarketOpen;
        CoachingMarketWeekNumber = save.CoachingMarketWeekNumber;

        // Restore progression & AI state
        AllAwards = save.AllAwards;
        RetiredPlayerIds = save.RetiredPlayerIds;

        IsGameActive = true;
    }

    public SaveData CreateSaveData(string saveName)
    {
        var _staffState = Staff?.GetState() ?? (new List<string>(), new List<InterviewRequest>(), new List<string>(), new Dictionary<string, string>());
        return new SaveData
        {
            SaveName = saveName,
            SaveDate = DateTime.UtcNow,
            Seed = Seed,
            CurrentYear = Calendar.CurrentYear,
            CurrentPhase = Calendar.CurrentPhase,
            CurrentWeek = Calendar.CurrentWeek,
            PlayerTeamId = PlayerTeamId,
            Teams = Teams,
            Players = Players,
            Coaches = Coaches,
            Scouts = Scouts,
            ScoutMarket = ScoutMarket,
            AllDraftPicks = AllDraftPicks,
            CurrentDraftClass = CurrentDraftClass,
            TransactionLog = TransactionLog,
            SeasonHistory = SeasonHistory,
            AIProfiles = AIProfiles,
            CurrentSeason = CurrentSeason,
            AFCPlayoffSeeds = AFCPlayoffSeeds,
            NFCPlayoffSeeds = NFCPlayoffSeeds,
            FreeAgencyWeek = FreeAgencyWeekNumber,
            PendingOffers = FreeAgency?.GetState().Offers ?? new List<FreeAgentOffer>(),
            FreeAgentPool = FreeAgency?.GetState().Pool ?? new List<string>(),
            DraftBoardOrder = DraftBoardOrder,
            ScoutingWeeklyPool = Scouting?.GetState().WeeklyPool ?? 0,
            ScoutingCurrentPoints = Scouting?.GetState().CurrentPoints ?? 0,
            DraftBoardTags = DraftBoardTags,
            DraftCurrentRound = Draft?.GetState().PickIndex > 0 ? (Draft.GetCurrentPick()?.Round ?? 0) : 0,
            DraftCurrentPick = Draft?.GetState().PickIndex ?? 0,
            TradeHistory = Trading?.GetState().History ?? new List<TradeRecord>(),
            TradeBlockPlayerIds = Trading?.GetState().Block ?? new List<string>(),
            PendingTradeProposals = Trading?.GetState().Pending ?? new List<TradeProposal>(),
            TradeRelationships = Trading?.GetState().Relationships ?? new Dictionary<string, float>(),
            CoachingMarketIds = _staffState.MarketIds,
            InterviewRequests = _staffState.Requests,
            PromotionIntentCoachIds = _staffState.PromotionIntents,
            AIPromotionIntents = _staffState.AIIntents,
            ActivePlayoffTeamIds = ActivePlayoffTeamIds.ToList(),
            IsCoachingMarketOpen = IsCoachingMarketOpen,
            CoachingMarketWeekNumber = CoachingMarketWeekNumber,
            AllAwards = AllAwards,
            RetiredPlayerIds = RetiredPlayerIds,
        };
    }

    // --- Advancement ---

    public void AdvanceWeek()
    {
        if (!Calendar.CanAdvance()) return;

        var oldPhase = Calendar.CurrentPhase;
        var result = Calendar.AdvanceWeek();
        if (!result.Success) return;

        // Detect phase transition
        if (result.NewPhase != oldPhase)
            OnPhaseTransition(result.NewPhase);

        // Process scouting year-round (as long as a draft class exists)
        if (CurrentDraftClass.Count > 0)
        {
            Scouting.ProcessScoutingWeek();
        }

        // Process free agency week
        if (Calendar.CurrentPhase == GamePhase.FreeAgency)
        {
            FreeAgencyWeekNumber++;
            FreeAgency.ProcessFreeAgencyWeek(FreeAgencyWeekNumber);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.FreeAgencyWeekProcessed, FreeAgencyWeekNumber);
        }

        // Process coaching market weekly
        if (IsCoachingMarketOpen)
        {
            CoachingMarketWeekNumber++;
            Staff.ProcessCoachingMarketWeek(CoachingMarketWeekNumber);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachingMarketWeekProcessed, CoachingMarketWeekNumber);
        }

        // Simulate games for current week if in season
        if (Calendar.IsRegularSeason() || Calendar.IsPlayoffs() ||
            Calendar.CurrentPhase == GamePhase.SuperBowl)
        {
            SimulateCurrentWeekGames();
        }

        // Tick injuries every week during season
        if (Calendar.IsRegularSeason() || Calendar.IsPlayoffs() ||
            Calendar.CurrentPhase == GamePhase.SuperBowl)
        {
            InjurySystem.TickInjuries();
        }

        // Process AI trades during trade window
        if (Trading.IsTradeWindowOpen())
        {
            Trading.GenerateAITradeProposals();
        }

        if (result.YearChanged)
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.SeasonEnded, Calendar.CurrentYear - 1);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.SeasonStarted, Calendar.CurrentYear);
        }

        EventBus.Instance?.EmitSignal(EventBus.SignalName.PhaseChanged, (int)Calendar.CurrentPhase);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.WeekAdvanced, Calendar.CurrentYear, Calendar.CurrentWeek);
    }

    public void AdvanceToNextPhase()
    {
        var oldPhase = Calendar.CurrentPhase;
        var result = Calendar.AdvanceToNextPhase();
        if (!result.Success) return;

        if (result.NewPhase != oldPhase)
            OnPhaseTransition(result.NewPhase);

        if (result.YearChanged)
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.SeasonEnded, Calendar.CurrentYear - 1);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.SeasonStarted, Calendar.CurrentYear);
        }

        EventBus.Instance?.EmitSignal(EventBus.SignalName.PhaseChanged, (int)Calendar.CurrentPhase);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.WeekAdvanced, Calendar.CurrentYear, Calendar.CurrentWeek);
    }

    // --- Phase 3: Season Simulation ---

    private void OnPhaseTransition(GamePhase newPhase)
    {
        // Emit phase change notification
        if (SettingsManager.Current.ShowPhaseNotifications)
        {
            string phaseName = Calendar.GetPhaseDisplayName();
            EventBus.Instance?.EmitSignal(EventBus.SignalName.NotificationCreated,
                "Phase Change", $"Entering {phaseName}", 1);
        }

        switch (newPhase)
        {
            case GamePhase.PostSeason:
                CloseCoachingMarket();
                RunEndOfSeasonProcessing();
                GenerateScoutMarket();
                break;
            case GamePhase.CombineScouting:
                StartCombineScouting();
                break;
            case GamePhase.FreeAgency:
                StartFreeAgency();
                break;
            case GamePhase.PreDraft:
                EndFreeAgency();
                break;
            case GamePhase.Draft:
                StartDraft();
                break;
            case GamePhase.PostDraft:
                EndDraft();
                AIGM.RunAICuts();
                AIGM.RunAIExtensions();
                // Generate next year's draft class so scouting can begin immediately
                if (_prospectGenerator != null)
                {
                    CurrentDraftClass = _prospectGenerator.GenerateDraftClass(Calendar.CurrentYear + 1, Rng);
                    GD.Print($"Generated {CurrentDraftClass.Count} draft prospects for {Calendar.CurrentYear + 1}.");
                }
                Scouting.InitializeForDraftCycle();
                DraftBoardOrder.Clear();
                DraftBoardTags.Clear();
                break;
            case GamePhase.Preseason:
                AIGM.SetAIDepthCharts();
                AIGM.UpdateAIStrategies();
                break;
            case GamePhase.RegularSeason:
                GenerateSeasonSchedule();
                break;
            case GamePhase.Playoffs:
                SetupPlayoffBracket();
                OpenCoachingMarket();
                break;
            case GamePhase.SuperBowl:
                SetupSuperBowl();
                break;
        }
    }

    // --- Phase 7: Coaching Market Lifecycle ---

    private void OpenCoachingMarket()
    {
        // Only run after an actual season has been played
        if (SeasonHistory.Count == 0 && CurrentSeason.Games.Count == 0) return;

        // Initialize ActivePlayoffTeamIds from current playoff seeds
        ActivePlayoffTeamIds.Clear();
        foreach (var seed in AFCPlayoffSeeds.Concat(NFCPlayoffSeeds))
            ActivePlayoffTeamIds.Add(seed.TeamId);

        IsCoachingMarketOpen = true;
        CoachingMarketWeekNumber = 0;

        // Fire underperforming AI HCs
        var firings = Staff.FireUnderperformingHCs();
        foreach (var change in firings)
            GD.Print($"Coaching Market: {change}");

        // Determine AI promotion intents
        Staff.DetermineAIPromotionIntents();

        // Generate free agent coaches for the market
        Staff.GenerateAndAddMarketCoaches();

        GD.Print($"Coaching market opened. {ActivePlayoffTeamIds.Count} playoff teams active.");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachingMarketOpened, Calendar.CurrentYear);
    }

    private void CloseCoachingMarket()
    {
        if (!IsCoachingMarketOpen) return;

        Staff.CloseMarketFillVacancies();

        IsCoachingMarketOpen = false;
        CoachingMarketWeekNumber = 0;
        ActivePlayoffTeamIds.Clear();

        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachingMarketClosed, Calendar.CurrentYear);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachingCarouselCompleted, Calendar.CurrentYear);
    }

    private void UpdateActivePlayoffTeams()
    {
        var playoffGames = CurrentSeason.Games.Where(g => g.IsPlayoff && g.IsCompleted).ToList();

        foreach (var game in playoffGames)
        {
            // Determine the loser
            string? loserId = null;
            if (game.HomeScore > game.AwayScore)
                loserId = game.AwayTeamId;
            else if (game.AwayScore > game.HomeScore)
                loserId = game.HomeTeamId;

            if (loserId != null && ActivePlayoffTeamIds.Remove(loserId))
            {
                GD.Print($"Coaching market: {GetTeam(loserId)?.FullName} eliminated — coaches now available for interviews");
            }
        }
    }

    // --- Phase 8: End of Season Processing ---

    private void RunEndOfSeasonProcessing()
    {
        // Only run after an actual season has been played
        if (SeasonHistory.Count == 0 && CurrentSeason.Games.Count == 0) return;

        int year = Calendar.CurrentYear;

        // 1. Calculate awards (before progression so awards feed dev trait changes)
        var awards = AwardsCalculator.Calculate(year, Players, Teams, CurrentSeason);
        CurrentSeason.Awards = awards;
        AllAwards.Add(awards);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.AwardsCalculated, year);

        if (awards.MvpId != null)
        {
            GD.Print($"Season Awards: MVP — {GetPlayer(awards.MvpId)?.FullName}");
            if (SettingsManager.Current.ShowAwardNotifications)
                EventBus.Instance?.EmitSignal(EventBus.SignalName.NotificationCreated,
                    "Season MVP", $"{GetPlayer(awards.MvpId)?.FullName} wins MVP!", 2);
        }
        if (awards.DpoyId != null)
        {
            GD.Print($"Season Awards: DPOY — {GetPlayer(awards.DpoyId)?.FullName}");
            if (SettingsManager.Current.ShowAwardNotifications)
                EventBus.Instance?.EmitSignal(EventBus.SignalName.NotificationCreated,
                    "Defensive POY", $"{GetPlayer(awards.DpoyId)?.FullName} wins DPOY!", 2);
        }

        // 2. Dev trait changes based on awards
        Progression.ProcessDevTraitChanges(awards);

        // 3. Run attribute progression
        var report = Progression.RunOffseasonProgression();
        GD.Print($"Progression: {report.Improved.Count} improved, {report.Declined.Count} declined");

        // 4. Process retirements
        var retirements = Progression.ProcessRetirements();
        foreach (var (playerId, reason) in retirements)
        {
            RetiredPlayerIds.Add(playerId);
            var p = GetPlayer(playerId);
            GD.Print($"Retired: {p?.FullName ?? playerId} ({reason})");
        }

        // 5. Update owner patience & fan satisfaction for player's team
        var playerTeam = GetPlayerTeam();
        if (playerTeam != null)
        {
            var completedGames = CurrentSeason.Games.Where(g => g.IsCompleted && !g.IsPlayoff).ToList();
            int wins = completedGames.Count(g =>
                (g.HomeTeamId == playerTeam.Id && g.HomeScore > g.AwayScore) ||
                (g.AwayTeamId == playerTeam.Id && g.AwayScore > g.HomeScore));
            int losses = completedGames.Count(g =>
                (g.HomeTeamId == playerTeam.Id && g.HomeScore < g.AwayScore) ||
                (g.AwayTeamId == playerTeam.Id && g.AwayScore < g.HomeScore));

            var playoffGames = CurrentSeason.Games.Where(g => g.IsPlayoff && g.IsCompleted).ToList();
            bool madePlayoffs = playoffGames.Any(g =>
                g.HomeTeamId == playerTeam.Id || g.AwayTeamId == playerTeam.Id);
            bool madeSuperBowl = playoffGames.Any(g =>
                g.Week >= 21 && (g.HomeTeamId == playerTeam.Id || g.AwayTeamId == playerTeam.Id));
            bool wonSuperBowl = CurrentSeason.ChampionTeamId == playerTeam.Id;

            Progression.UpdateOwnerPatience(playerTeam, wins, losses, madePlayoffs, madeSuperBowl);
            Progression.UpdateFanSatisfaction(playerTeam, wins, madePlayoffs, wonSuperBowl);
        }

        // 6. Analyze team needs for all teams
        AIGM.AnalyzeAllTeamNeeds();

        // 7. Calculate division ranks before archiving
        var divisionGroups = Teams.GroupBy(t => (t.Conference, t.Division));
        foreach (var group in divisionGroups)
        {
            var ranked = group
                .OrderByDescending(t => {
                    int total = t.CurrentRecord.Wins + t.CurrentRecord.Losses + t.CurrentRecord.Ties;
                    return total == 0 ? 0f : (t.CurrentRecord.Wins + t.CurrentRecord.Ties * 0.5f) / total;
                })
                .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
                .ThenByDescending(t => t.CurrentRecord.PointsFor)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
                ranked[i].CurrentRecord.DivisionRank = i + 1; // 1-4
        }

        // 8. Archive team records to team history
        foreach (var team in Teams)
            team.SeasonHistory.Add(team.CurrentRecord);

        // 9. Archive season and prepare for next year
        SeasonHistory.Add(CurrentSeason);
        CurrentSeason = new Season { Year = year + 1 };

        EventBus.Instance?.EmitSignal(EventBus.SignalName.ProgressionCompleted, year);
    }

    // --- Phase 4: Free Agency ---

    private void StartFreeAgency()
    {
        // Reset team tag flags
        foreach (var team in Teams)
        {
            team.FranchiseTagUsed = false;
            team.TaggedPlayerId = null;
            team.TransitionTagUsed = false;
            team.TransitionTagPlayerId = null;
        }

        FreeAgencyWeekNumber = 0;
        FreeAgency.InitializeFreeAgency(Calendar.CurrentYear);

        GD.Print($"Free agency opened. {FreeAgency.FreeAgentPool.Count} free agents available.");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.FreeAgencyOpened, Calendar.CurrentYear);
    }

    private void EndFreeAgency()
    {
        // Calculate compensatory picks for the upcoming draft
        var compPicks = CompensatoryPickCalculator.CalculateCompensatoryPicks(
            TransactionLog, Players, Teams, Calendar.CurrentYear);

        foreach (var pick in compPicks)
        {
            AllDraftPicks.Add(pick);
            var team = GetTeam(pick.CurrentTeamId);
            team?.DraftPicks.Add(pick);
        }

        if (compPicks.Count > 0)
        {
            GD.Print($"Awarded {compPicks.Count} compensatory picks.");
            EventBus.Instance?.EmitSignal(EventBus.SignalName.CompensatoryPicksAwarded);
        }
    }

    // --- Phase 5: Scouting & Draft ---

    private void StartCombineScouting()
    {
        // Generate draft class only if not already generated (PostDraft generates next year's class early)
        if (CurrentDraftClass.Count == 0 && _prospectGenerator != null)
        {
            CurrentDraftClass = _prospectGenerator.GenerateDraftClass(Calendar.CurrentYear, Rng);
            GD.Print($"Generated {CurrentDraftClass.Count} draft prospects for {Calendar.CurrentYear}.");
            Scouting.InitializeForDraftCycle();
        }

        // Combine gives baseline scouting to all prospects
        Scouting.AutoScoutCombine();
    }

    private void StartDraft()
    {
        Draft.InitializeDraft(Calendar.CurrentYear);
    }

    private void EndDraft()
    {
        var udfaResults = Draft.ProcessUDFA();
        if (udfaResults.Count > 0)
            GD.Print($"Signed {udfaResults.Count} UDFAs across the league.");

        // Rebuild player lookup since new players were added
        RebuildLookups();
    }

    private void GenerateScouts()
    {
        string[] scoutFirstNames = { "Mike", "Dave", "Tom", "Chris", "Steve", "Mark", "Jim", "Dan" };
        string[] scoutLastNames = { "Reynolds", "Harrison", "Cooper", "Mitchell", "Brooks", "Palmer", "Walsh", "Grant" };
        var specialties = Enum.GetValues<ScoutSpecialty>();
        var regions = Enum.GetValues<ScoutRegion>();

        for (int i = 0; i < 6; i++)
        {
            Scouts.Add(new Scout
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{scoutFirstNames[Rng.Next(scoutFirstNames.Length)]} {scoutLastNames[Rng.Next(scoutLastNames.Length)]}",
                Accuracy = 55 + Rng.Next(35),
                Speed = 50 + Rng.Next(40),
                Specialty = specialties[Rng.Next(specialties.Length)],
                Region = regions[Rng.Next(regions.Length)],
                Salary = 50000 + Rng.Next(50000),
                Experience = Rng.Next(20),
            });
        }

        GD.Print($"Generated {Scouts.Count} scouts.");
    }

    private void GenerateScoutMarket()
    {
        ScoutMarket.Clear();

        string[] scoutFirstNames = { "Mike", "Dave", "Tom", "Chris", "Steve", "Mark", "Jim", "Dan" };
        string[] scoutLastNames = { "Reynolds", "Harrison", "Cooper", "Mitchell", "Brooks", "Palmer", "Walsh", "Grant" };
        var specialties = Enum.GetValues<ScoutSpecialty>();
        var regions = Enum.GetValues<ScoutRegion>();

        for (int i = 0; i < 5; i++)
        {
            ScoutMarket.Add(new Scout
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{scoutFirstNames[Rng.Next(scoutFirstNames.Length)]} {scoutLastNames[Rng.Next(scoutLastNames.Length)]}",
                Accuracy = 55 + Rng.Next(35),
                Speed = 50 + Rng.Next(40),
                Specialty = specialties[Rng.Next(specialties.Length)],
                Region = regions[Rng.Next(regions.Length)],
                Salary = 50000 + Rng.Next(50000),
                Experience = Rng.Next(20),
            });
        }

        GD.Print($"Generated {ScoutMarket.Count} scouts for market.");
    }

    public (bool Success, string Message) HireScout(string scoutId)
    {
        if (Scouts.Count >= 10)
            return (false, "Cannot hire more scouts. Maximum of 10 reached.");

        var scout = ScoutMarket.FirstOrDefault(s => s.Id == scoutId);
        if (scout == null)
            return (false, "Scout not found in market.");

        ScoutMarket.Remove(scout);
        Scouts.Add(scout);
        Scouting.RecalculatePoints();

        return (true, $"Hired {scout.Name}.");
    }

    public (bool Success, string Message) FireScout(string scoutId)
    {
        if (Scouts.Count <= 1)
            return (false, "Cannot fire your last scout.");

        var scout = Scouts.FirstOrDefault(s => s.Id == scoutId);
        if (scout == null)
            return (false, "Scout not found.");

        Scouts.Remove(scout);
        Scouting.RecalculatePoints();

        return (true, $"Fired {scout.Name}.");
    }

    // --- Phase 3: Season Schedule ---

    private void GenerateSeasonSchedule()
    {
        CurrentSeason = new Season { Year = Calendar.CurrentYear };

        // Build prior-year division ranks from team history
        Dictionary<string, int>? priorRanks = null;
        if (Teams.Any(t => t.SeasonHistory.Count > 0))
        {
            priorRanks = new Dictionary<string, int>();
            foreach (var team in Teams)
            {
                var lastRecord = team.SeasonHistory.LastOrDefault();
                priorRanks[team.Id] = lastRecord?.DivisionRank ?? 0;
            }
        }

        var games = ScheduleGenerator.GenerateRegularSeason(Teams, Calendar.CurrentYear, Rng, priorRanks);
        CurrentSeason.Games.AddRange(games);

        // Reset all team records for new season
        foreach (var team in Teams)
        {
            team.CurrentRecord = new TeamRecord { Season = Calendar.CurrentYear };
        }

        GD.Print($"Generated {games.Count} regular season games for {Calendar.CurrentYear}");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.ScheduleGenerated, Calendar.CurrentYear);
    }

    private void SimulateCurrentWeekGames()
    {
        RecentGameResults.Clear();

        var weekGames = CurrentSeason.Games
            .Where(g => g.Week == Calendar.CurrentWeek && !g.IsCompleted)
            .ToList();

        if (weekGames.Count == 0) return;

        foreach (var game in weekGames)
        {
            var result = SimEngine.SimulateGame(game);

            // Apply to Game model
            game.HomeScore = result.HomeScore;
            game.AwayScore = result.AwayScore;
            game.IsCompleted = true;
            game.PlayerOfTheGameId = result.PlayerOfTheGameId;

            // Apply player stats
            ApplyPlayerStats(result);

            // Apply injuries
            var injuredBefore = new HashSet<string>(
                Players.Where(p => p.CurrentInjury != null && p.TeamId == PlayerTeamId).Select(p => p.Id));
            InjurySystem.ApplyInjuries(result, Calendar.CurrentYear, Calendar.CurrentWeek);

            // Notify about player team injuries
            if (SettingsManager.Current.ShowInjuryNotifications)
            {
                foreach (var p in Players.Where(p => p.CurrentInjury != null && p.TeamId == PlayerTeamId && !injuredBefore.Contains(p.Id)))
                {
                    var inj = p.CurrentInjury!;
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.NotificationCreated,
                        "Player Injured", $"{p.FullName} — {inj.InjuryType} ({inj.WeeksRemaining} weeks)", 3);
                }
            }

            // Update team records
            UpdateTeamRecords(game);

            // Store for UI
            RecentGameResults.Add(result);

            EventBus.Instance?.EmitSignal(EventBus.SignalName.GameCompleted, game.Id);
        }

        GD.Print($"Simulated {weekGames.Count} games for week {Calendar.CurrentWeek}");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.WeekSimulated, Calendar.CurrentYear, Calendar.CurrentWeek);

        // Check if a playoff round just completed
        if (Calendar.IsPlayoffs())
        {
            CheckAndAdvancePlayoffRound();
            UpdateActivePlayoffTeams();
        }
    }

    private void ApplyPlayerStats(GameResult result)
    {
        foreach (var (playerId, gameStats) in result.PlayerStats)
        {
            var player = GetPlayer(playerId);
            if (player == null) continue;

            if (!player.CareerStats.TryGetValue(Calendar.CurrentYear, out var seasonStats))
            {
                seasonStats = new SeasonStats { Season = Calendar.CurrentYear };
                player.CareerStats[Calendar.CurrentYear] = seasonStats;
            }

            seasonStats.GamesPlayed++;
            seasonStats.Completions += gameStats.Completions;
            seasonStats.Attempts += gameStats.Attempts;
            seasonStats.PassingYards += gameStats.PassingYards;
            seasonStats.PassingTDs += gameStats.PassingTDs;
            seasonStats.Interceptions += gameStats.Interceptions;
            seasonStats.Sacked += gameStats.Sacked;
            seasonStats.RushAttempts += gameStats.RushAttempts;
            seasonStats.RushingYards += gameStats.RushingYards;
            seasonStats.RushingTDs += gameStats.RushingTDs;
            seasonStats.Fumbles += gameStats.Fumbles;
            seasonStats.FumblesLost += gameStats.FumblesLost;
            seasonStats.Targets += gameStats.Targets;
            seasonStats.Receptions += gameStats.Receptions;
            seasonStats.ReceivingYards += gameStats.ReceivingYards;
            seasonStats.ReceivingTDs += gameStats.ReceivingTDs;
            seasonStats.TotalTackles += gameStats.TotalTackles;
            seasonStats.SoloTackles += gameStats.SoloTackles;
            seasonStats.Sacks += gameStats.Sacks;
            seasonStats.TacklesForLoss += gameStats.TacklesForLoss;
            seasonStats.QBHits += gameStats.QBHits;
            seasonStats.ForcedFumbles += gameStats.ForcedFumbles;
            seasonStats.FumbleRecoveries += gameStats.FumbleRecoveries;
            seasonStats.InterceptionsDef += gameStats.InterceptionsDef;
            seasonStats.PassesDefended += gameStats.PassesDefended;
            seasonStats.DefensiveTDs += gameStats.DefensiveTDs;
            seasonStats.FGMade += gameStats.FGMade;
            seasonStats.FGAttempted += gameStats.FGAttempted;
            seasonStats.XPMade += gameStats.XPMade;
            seasonStats.XPAttempted += gameStats.XPAttempted;
        }
    }

    private void UpdateTeamRecords(Game game)
    {
        var homeTeam = GetTeam(game.HomeTeamId);
        var awayTeam = GetTeam(game.AwayTeamId);
        if (homeTeam == null || awayTeam == null) return;

        homeTeam.CurrentRecord.PointsFor += game.HomeScore;
        homeTeam.CurrentRecord.PointsAgainst += game.AwayScore;
        awayTeam.CurrentRecord.PointsFor += game.AwayScore;
        awayTeam.CurrentRecord.PointsAgainst += game.HomeScore;

        if (game.HomeScore > game.AwayScore)
        {
            homeTeam.CurrentRecord.Wins++;
            awayTeam.CurrentRecord.Losses++;
        }
        else if (game.AwayScore > game.HomeScore)
        {
            awayTeam.CurrentRecord.Wins++;
            homeTeam.CurrentRecord.Losses++;
        }
        else
        {
            homeTeam.CurrentRecord.Ties++;
            awayTeam.CurrentRecord.Ties++;
        }
    }

    // --- Playoff Flow ---

    private void SetupPlayoffBracket()
    {
        var regularGames = CurrentSeason.Games.Where(g => !g.IsPlayoff).ToList();
        var (afc, nfc) = ScheduleGenerator.DeterminePlayoffSeeds(Teams, regularGames);
        AFCPlayoffSeeds = afc;
        NFCPlayoffSeeds = nfc;

        foreach (var seed in afc.Concat(nfc))
        {
            var team = GetTeam(seed.TeamId);
            if (team != null) team.CurrentRecord.MadePlayoffs = true;
        }

        // Generate Wild Card round (week 1 of Playoffs)
        var wildCardGames = ScheduleGenerator.GeneratePlayoffRound(
            afc, nfc, PlayoffRound.WildCard, Calendar.CurrentYear, 1);
        CurrentSeason.Games.AddRange(wildCardGames);

        GD.Print($"Playoff bracket set. AFC #1: {GetTeam(afc[0].TeamId)?.Abbreviation}, NFC #1: {GetTeam(nfc[0].TeamId)?.Abbreviation}");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayoffTeamsSet);
    }

    private void CheckAndAdvancePlayoffRound()
    {
        var playoffGames = CurrentSeason.Games.Where(g => g.IsPlayoff).ToList();
        var currentWeekGames = playoffGames.Where(g => g.Week == Calendar.CurrentWeek).ToList();

        if (currentWeekGames.All(g => g.IsCompleted))
        {
            int completedWeeks = playoffGames.Where(g => g.IsCompleted).Select(g => g.Week).Distinct().Count();

            switch (completedWeeks)
            {
                case 1: // Wild Card done → generate Divisional
                    var afcWC = ScheduleGenerator.FilterToWinners(AFCPlayoffSeeds, playoffGames);
                    var nfcWC = ScheduleGenerator.FilterToWinners(NFCPlayoffSeeds, playoffGames);
                    var divGames = ScheduleGenerator.GeneratePlayoffRound(
                        afcWC, nfcWC, PlayoffRound.Divisional, Calendar.CurrentYear, 2);
                    CurrentSeason.Games.AddRange(divGames);
                    GD.Print("Divisional round games generated");
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayoffRoundCompleted, 1);
                    break;

                case 2: // Divisional done → generate Conference Championship
                    var afcDiv = ScheduleGenerator.FilterToWinners(AFCPlayoffSeeds, playoffGames);
                    var nfcDiv = ScheduleGenerator.FilterToWinners(NFCPlayoffSeeds, playoffGames);
                    var ccGames = ScheduleGenerator.GeneratePlayoffRound(
                        afcDiv, nfcDiv, PlayoffRound.ConferenceChampionship, Calendar.CurrentYear, 3);
                    CurrentSeason.Games.AddRange(ccGames);
                    GD.Print("Conference Championship games generated");
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayoffRoundCompleted, 2);
                    break;

                case 3: // Conference Championship done → advance to SuperBowl phase handled by calendar
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayoffRoundCompleted, 3);
                    break;
            }
        }
    }

    private void SetupSuperBowl()
    {
        var playoffGames = CurrentSeason.Games.Where(g => g.IsPlayoff && g.IsCompleted).ToList();
        var afcChamp = ScheduleGenerator.FilterToWinners(AFCPlayoffSeeds, playoffGames);
        var nfcChamp = ScheduleGenerator.FilterToWinners(NFCPlayoffSeeds, playoffGames);

        if (afcChamp.Count > 0 && nfcChamp.Count > 0)
        {
            var sbGames = ScheduleGenerator.GeneratePlayoffRound(
                afcChamp, nfcChamp, PlayoffRound.SuperBowl, Calendar.CurrentYear, 1);
            CurrentSeason.Games.AddRange(sbGames);

            GD.Print($"Super Bowl set: {GetTeam(afcChamp[0].TeamId)?.Abbreviation} vs {GetTeam(nfcChamp[0].TeamId)?.Abbreviation}");
        }
    }

    // --- Lookups ---

    public Team? GetTeam(string teamId) => _teamLookup.GetValueOrDefault(teamId);
    public Player? GetPlayer(string playerId) => _playerLookup.GetValueOrDefault(playerId);
    public Coach? GetCoach(string coachId) => _coachLookup.GetValueOrDefault(coachId);
    public Team? GetPlayerTeam() => GetTeam(PlayerTeamId);

    public List<Player> GetTeamPlayers(string teamId) =>
        Players.Where(p => p.TeamId == teamId).ToList();

    public List<Player> GetTeamActivePlayers(string teamId) =>
        Players.Where(p => p.TeamId == teamId && p.RosterStatus == RosterStatus.Active53).ToList();

    // --- Private: Loading & Generation ---

    private void LoadTeams(string dataPath)
    {
        string teamsJson = File.ReadAllText(Path.Combine(dataPath, "teams.json"));
        var doc = JsonDocument.Parse(teamsJson);
        JsonElement root = doc.RootElement;

        // Support both flat array and {"teams": [...]} format
        JsonElement teamsArray = root.ValueKind == JsonValueKind.Array
            ? root
            : root.GetProperty("teams");

        var teamDataList = new List<JsonElement>();
        foreach (var item in teamsArray.EnumerateArray())
            teamDataList.Add(item);

        foreach (var td in teamDataList)
        {
            string id = td.GetProperty("id").GetString() ?? "";
            var team = new Team
            {
                Id = id,
                City = td.GetProperty("city").GetString() ?? "",
                Name = td.GetProperty("name").GetString() ?? "",
                Abbreviation = td.GetProperty("abbreviation").GetString() ?? "",
                Conference = Enum.Parse<Conference>(td.GetProperty("conference").GetString() ?? "AFC"),
                Division = Enum.Parse<Division>(td.GetProperty("division").GetString() ?? "North"),
                PrimaryColorHex = td.GetProperty("primaryColor").GetString() ?? "#000000",
                SecondaryColorHex = td.GetProperty("secondaryColor").GetString() ?? "#FFFFFF",
                LogoPath = td.GetProperty("logoPath").GetString() ?? "",
                SalaryCap = 25540000000, // $255.4M in cents
                CurrentRecord = new TeamRecord { Season = Calendar.CurrentYear },
                OffensiveScheme = (SchemeType)Rng.Next(6),     // random offensive scheme
                DefensiveScheme = (SchemeType)(6 + Rng.Next(7)), // random defensive scheme
            };

            Teams.Add(team);
            _teamLookup[id] = team;
        }
    }

    private void LoadHistoricalStandings(string dataPath)
    {
        string path = Path.Combine(dataPath, "2025_standings.json");
        if (!File.Exists(path))
        {
            GD.Print("No historical standings file found, skipping seed data.");
            return;
        }

        string json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int season = root.GetProperty("season").GetInt32();
        int loaded = 0;

        foreach (var entry in root.GetProperty("standings").EnumerateArray())
        {
            string teamId = entry.GetProperty("teamId").GetString() ?? "";
            var team = GetTeam(teamId);
            if (team == null)
            {
                GD.Print($"Historical standings: unknown team ID '{teamId}', skipping.");
                continue;
            }

            var record = new TeamRecord
            {
                Season = season,
                Wins = entry.GetProperty("wins").GetInt32(),
                Losses = entry.GetProperty("losses").GetInt32(),
                Ties = entry.GetProperty("ties").GetInt32(),
                PointsFor = entry.GetProperty("pointsFor").GetInt32(),
                PointsAgainst = entry.GetProperty("pointsAgainst").GetInt32(),
                DivisionRank = entry.GetProperty("divisionRank").GetInt32(),
                MadePlayoffs = entry.GetProperty("madePlayoffs").GetBoolean(),
                PlayoffResult = entry.TryGetProperty("playoffResult", out var pr)
                    && pr.ValueKind != JsonValueKind.Null
                    ? pr.GetString()
                    : null,
            };

            team.SeasonHistory.Add(record);
            loaded++;
        }

        GD.Print($"Loaded historical standings for {loaded} teams (season {season}).");
    }

    private void InitializePlayerGenerator(string dataPath)
    {
        _playerGenerator = new PlayerGenerator();
        _playerGenerator.LoadData(dataPath);

        _prospectGenerator = new ProspectGenerator();
        _prospectGenerator.LoadData(dataPath);
    }

    private void GenerateAllRosters()
    {
        if (_playerGenerator == null) return;

        foreach (var team in Teams)
        {
            var roster = _playerGenerator.GenerateRoster(team, Calendar.CurrentYear, Rng);
            Players.AddRange(roster);

            _playerGenerator.SetupDepthChart(team, roster);

            foreach (var player in roster)
                _playerLookup[player.Id] = player;
        }
    }

    private void GenerateCoachingStaffs()
    {
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        string[] coachFirstNames;
        string[] coachLastNames;

        try
        {
            string json = File.ReadAllText(Path.Combine(dataPath, "coach_names.json"));
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            coachFirstNames = data.GetProperty("firstNames").EnumerateArray()
                .Select(e => e.GetString() ?? "").ToArray();
            coachLastNames = data.GetProperty("lastNames").EnumerateArray()
                .Select(e => e.GetString() ?? "").ToArray();
        }
        catch
        {
            coachFirstNames = new[] { "Bill", "Andy", "Mike", "Sean", "John", "Matt", "Kyle", "Dan", "Ron", "Pete" };
            coachLastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Davis", "Wilson", "Moore", "Taylor" };
        }

        var roles = new[]
        {
            CoachRole.HeadCoach, CoachRole.OffensiveCoordinator, CoachRole.DefensiveCoordinator,
            CoachRole.SpecialTeamsCoordinator, CoachRole.QBCoach, CoachRole.RBCoach, CoachRole.WRCoach,
            CoachRole.OLineCoach, CoachRole.DLineCoach, CoachRole.LBCoach, CoachRole.DBCoach
        };

        foreach (var team in Teams)
        {
            foreach (var role in roles)
            {
                var coach = new Coach
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = coachFirstNames[Rng.Next(coachFirstNames.Length)],
                    LastName = coachLastNames[Rng.Next(coachLastNames.Length)],
                    Age = 35 + Rng.Next(30),
                    Role = role,
                    TeamId = team.Id,
                    OffenseRating = 40 + Rng.Next(50),
                    DefenseRating = 40 + Rng.Next(50),
                    SpecialTeamsRating = 40 + Rng.Next(50),
                    GameManagement = 40 + Rng.Next(50),
                    PlayerDevelopment = 40 + Rng.Next(50),
                    Motivation = 40 + Rng.Next(50),
                    Adaptability = 40 + Rng.Next(50),
                    Recruiting = 40 + Rng.Next(50),
                    PreferredOffense = (SchemeType)Rng.Next(6),
                    PreferredDefense = (SchemeType)(6 + Rng.Next(7)),
                    Personality = (CoachPersonality)Rng.Next(5),
                    Prestige = 20 + Rng.Next(60),
                    Experience = Rng.Next(25),
                };

                Coaches.Add(coach);
                _coachLookup[coach.Id] = coach;

                switch (role)
                {
                    case CoachRole.HeadCoach:
                        team.HeadCoachId = coach.Id;
                        break;
                    case CoachRole.OffensiveCoordinator:
                        team.OffensiveCoordinatorId = coach.Id;
                        break;
                    case CoachRole.DefensiveCoordinator:
                        team.DefensiveCoordinatorId = coach.Id;
                        break;
                    case CoachRole.SpecialTeamsCoordinator:
                        team.SpecialTeamsCoordId = coach.Id;
                        break;
                    default:
                        team.PositionCoachIds.Add(coach.Id);
                        break;
                }
            }
        }
    }

    private void GenerateDraftPicks()
    {
        // Generate 3 years of draft picks for all teams
        for (int yearOffset = 0; yearOffset < 3; yearOffset++)
        {
            int year = Calendar.CurrentYear + yearOffset;
            foreach (var team in Teams)
            {
                for (int round = 1; round <= 7; round++)
                {
                    var pick = new DraftPick
                    {
                        Id = Guid.NewGuid().ToString(),
                        Year = year,
                        Round = round,
                        OriginalTeamId = team.Id,
                        CurrentTeamId = team.Id,
                    };
                    AllDraftPicks.Add(pick);
                    team.DraftPicks.Add(pick);
                }
            }
        }
    }

    private void InitializeAIProfiles()
    {
        foreach (var team in Teams)
        {
            if (team.Id == PlayerTeamId) continue;

            AIProfiles[team.Id] = new AIGMProfile
            {
                TeamId = team.Id,
                Strategy = (AIStrategy)Rng.Next(4), // WinNow through Rebuild
                RiskTolerance = 0.3f + (float)Rng.NextDouble() * 0.5f,
                DraftPreference = 0.2f + (float)Rng.NextDouble() * 0.6f,
                FreeAgencyAggression = 0.3f + (float)Rng.NextDouble() * 0.5f,
                TradeFrequency = 0.2f + (float)Rng.NextDouble() * 0.5f,
                CompetitiveWindowYears = 2 + Rng.Next(4),
            };
        }
    }

    private void InitializeSystems(string dataPath)
    {
        SalaryCapManager = new SalaryCapManager();
        SalaryCapManager.LoadRules(dataPath);

        RosterManager = new RosterManager(
            () => Teams,
            () => Players,
            () => TransactionLog,
            SalaryCapManager,
            () => Calendar);

        InjurySystem = new InjurySystem(
            () => Players,
            GetPlayer,
            () => Rng);

        Staff = new StaffSystem(
            () => Teams,
            () => Players,
            () => Coaches,
            () => Rng,
            GetCoach,
            GetTeam,
            () => Calendar,
            () => PlayerTeamId,
            () => ActivePlayoffTeamIds);

        SimEngine = new SimulationEngine(
            () => Teams,
            () => Players,
            () => Coaches,
            () => Rng,
            GetPlayer,
            GetTeam,
            GetCoach,
            InjurySystem,
            Staff);

        FreeAgency = new FreeAgencySystem(
            () => Teams,
            () => Players,
            () => Coaches,
            () => AIProfiles,
            () => Rng,
            GetPlayer,
            GetTeam,
            RosterManager,
            SalaryCapManager,
            () => Calendar,
            () => PlayerTeamId);

        Scouting = new ScoutingSystem(
            () => CurrentDraftClass,
            () => Scouts,
            () => Rng,
            () => PlayerTeamId);

        Draft = new DraftSystem(
            () => Teams,
            () => Players,
            () => CurrentDraftClass,
            () => AllDraftPicks,
            () => AIProfiles,
            () => Rng,
            () => PlayerTeamId,
            RosterManager,
            SalaryCapManager,
            () => Calendar);

        Trading = new TradeSystem(
            () => Teams,
            () => Players,
            () => AllDraftPicks,
            () => AIProfiles,
            () => Rng,
            GetPlayer,
            GetTeam,
            RosterManager,
            SalaryCapManager,
            () => Calendar,
            () => PlayerTeamId,
            () => TransactionLog);

        Progression = new ProgressionSystem(
            () => Teams,
            () => Players,
            () => Rng,
            GetPlayer,
            GetTeam,
            () => Calendar,
            Staff);

        AIGM = new AIGMController(
            () => Teams,
            () => Players,
            () => Rng,
            GetPlayer,
            GetTeam,
            () => AIProfiles,
            RosterManager,
            SalaryCapManager,
            () => PlayerTeamId);
    }

    private void CalculateAllTeamCaps()
    {
        foreach (var team in Teams)
        {
            SalaryCapManager.RecalculateTeamCap(team, Players, Calendar.CurrentYear);
        }
    }

    private void RebuildLookups()
    {
        _playerLookup = Players.ToDictionary(p => p.Id);
        _teamLookup = Teams.ToDictionary(t => t.Id);
        _coachLookup = Coaches.ToDictionary(c => c.Id);
    }
}
