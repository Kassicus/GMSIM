using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class DraftSystem
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<List<Prospect>> _getProspects;
    private readonly Func<List<DraftPick>> _getAllPicks;
    private readonly Func<Dictionary<string, AIGMProfile>> _getAIProfiles;
    private readonly Func<Random> _getRng;
    private readonly Func<string> _getPlayerTeamId;
    private readonly RosterManager _rosterManager;
    private readonly SalaryCapManager _capManager;
    private readonly Func<CalendarSystem> _getCalendar;

    private List<DraftPick> _pickOrder = new();
    private HashSet<string> _availableProspectIds = new();
    private List<DraftResult> _draftResults = new();
    private int _currentPickIndex;

    public bool IsDraftActive { get; private set; }
    public IReadOnlyList<DraftResult> DraftResults => _draftResults;

    public DraftSystem(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<Prospect>> getProspects,
        Func<List<DraftPick>> getAllPicks,
        Func<Dictionary<string, AIGMProfile>> getAIProfiles,
        Func<Random> getRng,
        Func<string> getPlayerTeamId,
        RosterManager rosterManager,
        SalaryCapManager capManager,
        Func<CalendarSystem> getCalendar)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getProspects = getProspects;
        _getAllPicks = getAllPicks;
        _getAIProfiles = getAIProfiles;
        _getRng = getRng;
        _getPlayerTeamId = getPlayerTeamId;
        _rosterManager = rosterManager;
        _capManager = capManager;
        _getCalendar = getCalendar;
    }

    public void InitializeDraft(int year)
    {
        var allPicks = _getAllPicks();
        var teams = _getTeams();
        var prospects = _getProspects();

        // Get all picks for this year
        var yearPicks = allPicks.Where(p => p.Year == year && !p.IsUsed).ToList();

        // Assign overall pick numbers based on team records (reverse order of wins)
        var teamOrder = teams
            .OrderBy(t => t.CurrentRecord.Wins)
            .ThenByDescending(t => t.CurrentRecord.Losses)
            .ThenBy(t => t.CurrentRecord.PointsFor) // tiebreaker: fewer points scored = higher pick
            .Select(t => t.Id)
            .ToList();

        // Build ordered pick list: round by round
        _pickOrder.Clear();
        for (int round = 1; round <= 7; round++)
        {
            // Regular picks: ordered by team record (worst first)
            var roundPicks = yearPicks
                .Where(p => p.Round == round && !p.IsCompensatory)
                .OrderBy(p => teamOrder.IndexOf(p.CurrentTeamId))
                .ToList();

            int overallNum = (round - 1) * 32 + 1;
            foreach (var pick in roundPicks)
            {
                pick.OverallNumber = overallNum++;
                _pickOrder.Add(pick);
            }

            // Compensatory picks at end of round (rounds 3-7)
            var compPicks = yearPicks
                .Where(p => p.Round == round && p.IsCompensatory)
                .OrderBy(p => teamOrder.IndexOf(p.CurrentTeamId))
                .ToList();

            foreach (var pick in compPicks)
            {
                pick.OverallNumber = overallNum++;
                _pickOrder.Add(pick);
            }
        }

        // Track available prospects
        _availableProspectIds = new HashSet<string>(prospects.Select(p => p.Id));

        _draftResults.Clear();
        _currentPickIndex = 0;
        IsDraftActive = true;

        GD.Print($"Draft initialized: {_pickOrder.Count} picks, {_availableProspectIds.Count} prospects available.");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.DraftStarted, year);
    }

    public DraftPick? GetCurrentPick()
    {
        if (_currentPickIndex >= _pickOrder.Count) return null;
        return _pickOrder[_currentPickIndex];
    }

    public bool IsPlayerPick()
    {
        var pick = GetCurrentPick();
        return pick?.CurrentTeamId == _getPlayerTeamId();
    }

    public bool IsDraftComplete() => _currentPickIndex >= _pickOrder.Count;

    public DraftResult? MakeSelection(string prospectId)
    {
        var pick = GetCurrentPick();
        if (pick == null) return null;
        if (!_availableProspectIds.Contains(prospectId)) return null;

        return ExecutePick(pick, prospectId);
    }

    public DraftResult? SimulateAIPick()
    {
        var pick = GetCurrentPick();
        if (pick == null) return null;

        string? selectedId = SelectAIProspect(pick);
        if (selectedId == null) return null;

        return ExecutePick(pick, selectedId);
    }

    public List<DraftResult> SimulateToNextPlayerPick()
    {
        var results = new List<DraftResult>();
        string playerTeamId = _getPlayerTeamId();

        while (!IsDraftComplete())
        {
            var pick = GetCurrentPick();
            if (pick == null) break;

            // Stop if it's the player's pick
            if (pick.CurrentTeamId == playerTeamId) break;

            var result = SimulateAIPick();
            if (result != null)
                results.Add(result);
            else
                break;
        }

        return results;
    }

    public DraftResult? AutoPick()
    {
        var pick = GetCurrentPick();
        if (pick == null) return null;

        string? selectedId = SelectAIProspect(pick);
        if (selectedId == null) return null;

        return ExecutePick(pick, selectedId);
    }

    private DraftResult ExecutePick(DraftPick pick, string prospectId)
    {
        var prospects = _getProspects();
        var prospect = prospects.FirstOrDefault(p => p.Id == prospectId);
        if (prospect == null) return null!;

        var calendar = _getCalendar();

        // Mark pick as used
        pick.IsUsed = true;
        pick.SelectedPlayerId = prospectId;

        // Update prospect
        prospect.IsDrafted = true;
        prospect.DraftedRound = pick.Round;
        prospect.DraftedPick = pick.OverallNumber;
        prospect.DraftedByTeamId = pick.CurrentTeamId;

        // Remove from available pool
        _availableProspectIds.Remove(prospectId);

        // Convert to player and add to team
        var player = ConvertProspectToPlayer(prospect, pick.CurrentTeamId, pick.Round,
            pick.OverallNumber ?? _currentPickIndex + 1, calendar.CurrentYear);

        var result = new DraftResult
        {
            Round = pick.Round,
            OverallPick = pick.OverallNumber ?? _currentPickIndex + 1,
            ProspectId = prospectId,
            ProspectName = prospect.FullName,
            Position = prospect.Position,
            College = prospect.College,
            TeamId = pick.CurrentTeamId,
            PlayerId = player.Id,
        };

        _draftResults.Add(result);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.DraftPickMade,
            pick.Round, pick.OverallNumber ?? 0, prospectId, pick.CurrentTeamId);

        // Advance to next pick
        _currentPickIndex++;

        // Check if draft is complete
        if (IsDraftComplete())
        {
            IsDraftActive = false;
            EventBus.Instance?.EmitSignal(EventBus.SignalName.DraftCompleted, calendar.CurrentYear);
        }

        return result;
    }

    private Player ConvertProspectToPlayer(Prospect prospect, string teamId, int round, int overallPick, int year)
    {
        var players = _getPlayers();
        var teams = _getTeams();
        var team = teams.FirstOrDefault(t => t.Id == teamId);

        var player = new Player
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = prospect.FirstName,
            LastName = prospect.LastName,
            Age = prospect.Age,
            YearsInLeague = 0,
            College = prospect.College,
            DraftYear = year,
            DraftRound = round,
            DraftPick = overallPick,
            IsUndrafted = false,
            HeightInches = prospect.HeightInches,
            WeightLbs = prospect.WeightLbs,
            Position = prospect.Position,
            Archetype = prospect.Archetype,
            Overall = OverallCalculator.Calculate(prospect.Position, prospect.TrueAttributes),
            PotentialCeiling = prospect.TruePotential,
            Attributes = prospect.TrueAttributes,
            Traits = prospect.TrueTraits,
            DevTrait = prospect.TrueDevTrait,
            RosterStatus = RosterStatus.Active53,
            Morale = 80,
            Fatigue = 0,
            TrajectoryModifier = new Random().Next(-2, 4),
            CareerStats = new Dictionary<int, SeasonStats>(),
            TeamId = teamId,
        };

        // Generate rookie contract
        player.CurrentContract = ContractGenerator.GenerateRookieContract(
            round, overallPick, year, player.Id, teamId);

        // Add to game state
        players.Add(player);

        if (team != null)
        {
            team.PlayerIds.Add(player.Id);

            // Add to depth chart
            if (team.DepthChart.Chart.TryGetValue(player.Position, out var depthList))
                depthList.Add(player.Id);
            else
                team.DepthChart.Chart[player.Position] = new List<string> { player.Id };

            // Recalculate cap
            _capManager.RecalculateTeamCap(team, players, year);
        }

        return player;
    }

    // --- AI Pick Logic ---

    private string? SelectAIProspect(DraftPick pick)
    {
        var prospects = _getProspects();
        var teams = _getTeams();
        var rng = _getRng();
        var aiProfiles = _getAIProfiles();

        var team = teams.FirstOrDefault(t => t.Id == pick.CurrentTeamId);
        if (team == null) return null;

        var available = prospects.Where(p => _availableProspectIds.Contains(p.Id)).ToList();
        if (available.Count == 0) return null;

        // Get AI profile (fallback to balanced defaults for player team on auto-pick)
        aiProfiles.TryGetValue(team.Id, out var profile);

        // Score each prospect
        var scored = new List<(Prospect Prospect, float Score)>();
        foreach (var prospect in available)
        {
            float score = ScoreProspectForTeam(prospect, team, pick, profile, rng);
            scored.Add((prospect, score));
        }

        // Sort by score descending, add small randomness for top picks
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Pick from top 3 with weighted random
        int topN = Math.Min(3, scored.Count);
        float totalWeight = 0;
        for (int i = 0; i < topN; i++)
            totalWeight += scored[i].Score;

        if (totalWeight <= 0)
            return scored[0].Prospect.Id;

        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0;
        for (int i = 0; i < topN; i++)
        {
            cumulative += scored[i].Score;
            if (roll <= cumulative)
                return scored[i].Prospect.Id;
        }

        return scored[0].Prospect.Id;
    }

    private float ScoreProspectForTeam(Prospect prospect, Team team, DraftPick pick,
        AIGMProfile? profile, Random rng)
    {
        float needScore = CalculateNeedScore(prospect.Position, team);
        float bpaScore = prospect.DraftValue;
        float valueScore = CalculateValueScore(prospect, pick);
        float schemeFitScore = CalculateSchemeFit(prospect, team);

        // Weights: Need 40%, BPA 35%, Value 15%, Scheme 10%
        float total = needScore * 0.40f + bpaScore * 0.35f + valueScore * 0.15f + schemeFitScore * 0.10f;

        // AI profile adjustments
        if (profile != null)
        {
            // Win-now teams value BPA more, rebuilding teams value potential
            if (profile.Strategy == AIStrategy.WinNow)
                total += bpaScore * 0.1f;
            else if (profile.Strategy == AIStrategy.Rebuild)
                total += (prospect.TruePotential - 70) * 0.3f;

            // Draft preference: higher = prefers drafting (takes more chances)
            total += profile.DraftPreference * 5f;
        }

        return Math.Max(0, total);
    }

    private float CalculateNeedScore(Position position, Team team)
    {
        // Count players at position on depth chart
        int depth = 0;
        if (team.DepthChart.Chart.TryGetValue(position, out var depthList))
            depth = depthList.Count;

        // Also check overall quality at position
        var players = _getPlayers();
        var teamPlayers = players.Where(p => p.TeamId == team.Id && p.Position == position).ToList();
        float avgOverall = teamPlayers.Count > 0 ? teamPlayers.Average(p => (float)p.Overall) : 0;

        // Need is high when depth is low or quality is low
        float depthNeed = depth switch
        {
            0 => 30f,
            1 => 20f,
            2 => 10f,
            _ => 0f,
        };

        float qualityNeed = avgOverall switch
        {
            < 65 => 15f,
            < 72 => 10f,
            < 78 => 5f,
            _ => 0f,
        };

        // Premium positions get bonus need
        float posBonus = position switch
        {
            Position.QB => 10f,
            Position.EDGE => 5f,
            Position.LT => 4f,
            Position.CB => 4f,
            _ => 0f,
        };

        return depthNeed + qualityNeed + posBonus;
    }

    private float CalculateValueScore(Prospect prospect, DraftPick pick)
    {
        // Expected DraftValue at this pick position
        int overall = pick.OverallNumber ?? 100;
        float expectedValue = overall switch
        {
            <= 5 => 140f,
            <= 15 => 120f,
            <= 32 => 105f,
            <= 64 => 90f,
            <= 100 => 75f,
            <= 150 => 60f,
            _ => 45f,
        };

        // Good value = prospect's value exceeds what's expected at this pick
        float diff = prospect.DraftValue - expectedValue;
        return Math.Clamp(diff, -20f, 30f);
    }

    private float CalculateSchemeFit(Prospect prospect, Team team)
    {
        // Simplified scheme fit: check if archetype aligns with team scheme
        var archetype = prospect.Archetype;
        float score = 0;

        // Offensive scheme fit
        if (prospect.Position is Position.QB or Position.HB or Position.WR or Position.TE
            or Position.LT or Position.LG or Position.C or Position.RG or Position.RT or Position.FB)
        {
            score = (team.OffensiveScheme, archetype) switch
            {
                (SchemeType.AirRaid, Archetype.DeepThreat) => 10f,
                (SchemeType.AirRaid, Archetype.RouteRunner) => 8f,
                (SchemeType.AirRaid, Archetype.PocketPasser) => 8f,
                (SchemeType.RunHeavy, Archetype.PowerBack) => 10f,
                (SchemeType.RunHeavy, Archetype.RunBlocker) => 8f,
                (SchemeType.SpreadOption, Archetype.Scrambler) => 10f,
                (SchemeType.SpreadOption, Archetype.SpeedBack) => 8f,
                (SchemeType.WestCoast, Archetype.PossessionReceiver) => 8f,
                (SchemeType.WestCoast, Archetype.ReceivingBack) => 8f,
                (SchemeType.RPO, Archetype.Scrambler) => 8f,
                _ => 3f, // baseline
            };
        }
        // Defensive scheme fit
        else
        {
            score = (team.DefensiveScheme, archetype) switch
            {
                (SchemeType.Cover1ManPress, Archetype.ManCoverage) => 10f,
                (SchemeType.Cover1ManPress, Archetype.SpeedRusher) => 8f,
                (SchemeType.Cover3, Archetype.ZoneCoverage) => 10f,
                (SchemeType.Cover3, Archetype.CenterFielder) => 8f,
                (SchemeType.Cover2Tampa, Archetype.ZoneCoverage) => 8f,
                (SchemeType.ThreeFour, Archetype.PowerRusher) => 8f,
                (SchemeType.ThreeFour, Archetype.NoseTackle) => 8f,
                (SchemeType.FourThree, Archetype.SpeedRusher) => 8f,
                (SchemeType.FourThree, Archetype.PassRushDT) => 8f,
                _ => 3f,
            };
        }

        return score;
    }

    // --- UDFA ---

    public List<UDFAResult> ProcessUDFA()
    {
        var prospects = _getProspects();
        var teams = _getTeams();
        var rng = _getRng();
        var calendar = _getCalendar();
        var results = new List<UDFAResult>();

        var undrafted = prospects.Where(p => !p.IsDrafted).ToList();
        if (undrafted.Count == 0) return results;

        // AI teams sign ~60% of undrafted prospects
        var aiTeams = teams.Where(t => t.Id != _getPlayerTeamId()).ToList();

        foreach (var prospect in undrafted)
        {
            if (rng.NextDouble() > 0.60) continue; // 40% go unsigned

            var team = aiTeams[rng.Next(aiTeams.Count)];
            if (team.PracticeSquadIds.Count >= 16) continue; // PS full

            var player = ConvertUDFAToPlayer(prospect, team.Id, calendar.CurrentYear);

            results.Add(new UDFAResult
            {
                ProspectId = prospect.Id,
                ProspectName = prospect.FullName,
                Position = prospect.Position,
                TeamId = team.Id,
                PlayerId = player.Id,
            });
        }

        return results;
    }

    public (bool Success, string Message) SignUDFA(string prospectId, string teamId)
    {
        var prospects = _getProspects();
        var teams = _getTeams();
        var calendar = _getCalendar();

        var prospect = prospects.FirstOrDefault(p => p.Id == prospectId);
        if (prospect == null) return (false, "Prospect not found.");
        if (prospect.IsDrafted) return (false, "Prospect was already drafted.");

        var team = teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null) return (false, "Team not found.");
        if (team.PracticeSquadIds.Count >= 16)
            return (false, "Practice squad is full.");

        var player = ConvertUDFAToPlayer(prospect, teamId, calendar.CurrentYear);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.UDFASigned, prospectId, teamId);
        return (true, $"Signed {prospect.FullName} as UDFA to practice squad.");
    }

    private Player ConvertUDFAToPlayer(Prospect prospect, string teamId, int year)
    {
        var players = _getPlayers();
        var teams = _getTeams();
        var team = teams.FirstOrDefault(t => t.Id == teamId);

        prospect.IsDrafted = false; // technically not drafted
        prospect.DraftedByTeamId = teamId;

        var player = new Player
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = prospect.FirstName,
            LastName = prospect.LastName,
            Age = prospect.Age,
            YearsInLeague = 0,
            College = prospect.College,
            DraftYear = year,
            DraftRound = 0,
            DraftPick = 0,
            IsUndrafted = true,
            HeightInches = prospect.HeightInches,
            WeightLbs = prospect.WeightLbs,
            Position = prospect.Position,
            Archetype = prospect.Archetype,
            Overall = OverallCalculator.Calculate(prospect.Position, prospect.TrueAttributes),
            PotentialCeiling = prospect.TruePotential,
            Attributes = prospect.TrueAttributes,
            Traits = prospect.TrueTraits,
            DevTrait = prospect.TrueDevTrait,
            RosterStatus = RosterStatus.PracticeSquad,
            Morale = 70,
            Fatigue = 0,
            TrajectoryModifier = new Random().Next(-2, 3),
            CareerStats = new Dictionary<int, SeasonStats>(),
            TeamId = teamId,
        };

        // UDFA contract: 3 years, minimal value
        player.CurrentContract = ContractGenerator.GenerateRookieContract(
            0, 0, year, player.Id, teamId);

        players.Add(player);

        if (team != null)
        {
            team.PracticeSquadIds.Add(player.Id);
            _capManager.RecalculateTeamCap(team, players, year);
        }

        return player;
    }

    // --- Available Prospects ---

    public List<Prospect> GetAvailableProspects()
    {
        var prospects = _getProspects();
        return prospects.Where(p => _availableProspectIds.Contains(p.Id)).ToList();
    }

    public List<Prospect> GetUndraftedFreeAgents()
    {
        var prospects = _getProspects();
        return prospects.Where(p => !p.IsDrafted && p.DraftedByTeamId == null).ToList();
    }

    // --- Save/Load ---

    public (int PickIndex, List<DraftResult> Results) GetState()
    {
        return (_currentPickIndex, _draftResults);
    }

    public void SetState(int pickIndex, int currentRound, int currentPick)
    {
        _currentPickIndex = pickIndex;
    }
}

// --- Result models ---

public class DraftResult
{
    public int Round { get; set; }
    public int OverallPick { get; set; }
    public string ProspectId { get; set; } = string.Empty;
    public string ProspectName { get; set; } = string.Empty;
    public Position Position { get; set; }
    public string College { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
}

public class UDFAResult
{
    public string ProspectId { get; set; } = string.Empty;
    public string ProspectName { get; set; } = string.Empty;
    public Position Position { get; set; }
    public string TeamId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
}
