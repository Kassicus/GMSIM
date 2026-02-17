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

    // Systems (Phase 2)
    public SalaryCapManager SalaryCapManager { get; private set; } = new();
    public RosterManager RosterManager { get; private set; } = null!;

    // Systems (Phase 3)
    public SimulationEngine SimEngine { get; private set; } = null!;
    public InjurySystem InjurySystem { get; private set; } = null!;

    // Systems (Phase 4)
    public FreeAgencySystem FreeAgency { get; private set; } = null!;
    public int FreeAgencyWeekNumber { get; private set; }

    // Season State (Phase 3)
    public Season CurrentSeason { get; private set; } = new();
    public List<GameResult> RecentGameResults { get; private set; } = new();
    public List<PlayoffSeed> AFCPlayoffSeeds { get; private set; } = new();
    public List<PlayoffSeed> NFCPlayoffSeeds { get; private set; } = new();

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
            CurrentYear = 2025,
            CurrentPhase = GamePhase.PostSeason,
            CurrentWeek = 1
        };

        // Clear everything
        Teams.Clear();
        Players.Clear();
        Coaches.Clear();
        Scouts.Clear();
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

        // Load data and generate league
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        LoadTeams(dataPath);
        InitializePlayerGenerator(dataPath);
        InitializeSystems(dataPath);
        GenerateAllRosters();
        GenerateCoachingStaffs();
        GenerateDraftPicks();
        InitializeAIProfiles();
        CalculateAllTeamCaps();

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

        IsGameActive = true;
    }

    public SaveData CreateSaveData(string saveName)
    {
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

        // Process free agency week
        if (Calendar.CurrentPhase == GamePhase.FreeAgency)
        {
            FreeAgencyWeekNumber++;
            FreeAgency.ProcessFreeAgencyWeek(FreeAgencyWeekNumber);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.FreeAgencyWeekProcessed, FreeAgencyWeekNumber);
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
        switch (newPhase)
        {
            case GamePhase.FreeAgency:
                StartFreeAgency();
                break;
            case GamePhase.PreDraft:
                EndFreeAgency();
                break;
            case GamePhase.RegularSeason:
                GenerateSeasonSchedule();
                break;
            case GamePhase.Playoffs:
                SetupPlayoffBracket();
                break;
            case GamePhase.SuperBowl:
                SetupSuperBowl();
                break;
        }
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

    // --- Phase 3: Season Schedule ---

    private void GenerateSeasonSchedule()
    {
        CurrentSeason = new Season { Year = Calendar.CurrentYear };
        var games = ScheduleGenerator.GenerateRegularSeason(Teams, Calendar.CurrentYear, Rng);
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
            InjurySystem.ApplyInjuries(result, Calendar.CurrentYear, Calendar.CurrentWeek);

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
            CheckAndAdvancePlayoffRound();
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

    private void InitializePlayerGenerator(string dataPath)
    {
        _playerGenerator = new PlayerGenerator();
        _playerGenerator.LoadData(dataPath);
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

        SimEngine = new SimulationEngine(
            () => Teams,
            () => Players,
            () => Coaches,
            () => Rng,
            GetPlayer,
            GetTeam,
            GetCoach,
            InjurySystem);

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
