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

        // Load data and generate league
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        LoadTeams(dataPath);
        InitializePlayerGenerator(dataPath);
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

        RebuildLookups();
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
        };
    }

    // --- Advancement ---

    public void AdvanceWeek()
    {
        if (!Calendar.CanAdvance()) return;

        var result = Calendar.AdvanceWeek();
        if (!result.Success) return;

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
        var result = Calendar.AdvanceToNextPhase();
        if (!result.Success) return;

        if (result.YearChanged)
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.SeasonEnded, Calendar.CurrentYear - 1);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.SeasonStarted, Calendar.CurrentYear);
        }

        EventBus.Instance?.EmitSignal(EventBus.SignalName.PhaseChanged, (int)Calendar.CurrentPhase);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.WeekAdvanced, Calendar.CurrentYear, Calendar.CurrentWeek);
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

    private void CalculateAllTeamCaps()
    {
        if (_playerGenerator == null) return;
        foreach (var team in Teams)
        {
            _playerGenerator.RecalculateTeamCap(team, Players, Calendar.CurrentYear);
        }
    }

    private void RebuildLookups()
    {
        _playerLookup = Players.ToDictionary(p => p.Id);
        _teamLookup = Teams.ToDictionary(t => t.Id);
        _coachLookup = Coaches.ToDictionary(c => c.Id);
    }
}
