using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class AIGMController
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<Random> _getRng;
    private readonly Func<string, Player?> _getPlayer;
    private readonly Func<string, Team?> _getTeam;
    private readonly Func<Dictionary<string, AIGMProfile>> _getProfiles;
    private readonly RosterManager _rosterManager;
    private readonly SalaryCapManager _capManager;
    private readonly Func<string> _getPlayerTeamId;

    // Positions considered premium for team needs analysis
    private static readonly HashSet<Position> PremiumPositions = new()
    {
        Position.QB, Position.EDGE, Position.CB, Position.LT, Position.RT
    };

    public AIGMController(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<Random> getRng,
        Func<string, Player?> getPlayer,
        Func<string, Team?> getTeam,
        Func<Dictionary<string, AIGMProfile>> getProfiles,
        RosterManager rosterManager,
        SalaryCapManager capManager,
        Func<string> getPlayerTeamId)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getRng = getRng;
        _getPlayer = getPlayer;
        _getTeam = getTeam;
        _getProfiles = getProfiles;
        _rosterManager = rosterManager;
        _capManager = capManager;
        _getPlayerTeamId = getPlayerTeamId;
    }

    // --- Team Needs Analysis ---

    public void AnalyzeAllTeamNeeds()
    {
        foreach (var team in _getTeams())
            AnalyzeTeamNeeds(team);
    }

    public List<Position> AnalyzeTeamNeeds(Team team)
    {
        var needs = new List<Position>();
        var players = _getPlayers();
        var teamPlayers = players.Where(p => p.TeamId == team.Id && p.RosterStatus == RosterStatus.Active53).ToList();

        // Check each position
        var positions = Enum.GetValues<Position>().Where(p => p != Position.LS && p != Position.FB).ToList();

        foreach (var pos in positions)
        {
            var posPlayers = teamPlayers.Where(p => p.Position == pos).ToList();

            // Depth need: fewer than 2 players
            if (posPlayers.Count < 2)
            {
                needs.Add(pos);
                continue;
            }

            // Starter quality need
            string? starterId = null;
            if (team.DepthChart.Chart.TryGetValue(pos, out var depthList) && depthList.Count > 0)
                starterId = depthList[0];

            var starter = starterId != null ? _getPlayer(starterId) : posPlayers.OrderByDescending(p => p.Overall).FirstOrDefault();

            if (starter != null)
            {
                int threshold = PremiumPositions.Contains(pos) ? 70 : 60;
                if (starter.Overall < threshold)
                    needs.Add(pos);
            }
        }

        // Sort by priority: QB first, then premium positions, then others
        needs = needs
            .Distinct()
            .OrderBy(p => p == Position.QB ? 0 : PremiumPositions.Contains(p) ? 1 : 2)
            .ThenBy(p => p)
            .ToList();

        team.TeamNeeds = needs;
        return needs;
    }

    // --- AI Cuts ---

    public void RunAICuts()
    {
        var playerTeamId = _getPlayerTeamId();
        var players = _getPlayers();

        foreach (var team in _getTeams())
        {
            if (team.Id == playerTeamId) continue;

            var profile = GetProfile(team.Id);
            var teamPlayers = players
                .Where(p => p.TeamId == team.Id && p.RosterStatus == RosterStatus.Active53)
                .ToList();

            // Target roster size (53 is max)
            int targetSize = 53;
            int currentSize = teamPlayers.Count;
            if (currentSize <= targetSize) continue;

            // Score each player: value vs cost
            var cutCandidates = teamPlayers
                .Where(p => p.CurrentContract != null)
                .Select(p => new
                {
                    Player = p,
                    Value = CalculatePlayerValue(p, profile),
                    CapSavings = _capManager.CalculateCutCapSavings(p, GetCurrentYear(), false),
                })
                .OrderBy(x => x.Value) // Worst value first
                .ToList();

            int cutCount = currentSize - targetSize;
            int cutsMade = 0;

            foreach (var candidate in cutCandidates)
            {
                if (cutsMade >= cutCount) break;

                // Don't cut very valuable players even if over roster limit
                if (candidate.Value > 80) continue;

                var result = _rosterManager.CutPlayer(candidate.Player.Id, team.Id);
                if (result.Success)
                {
                    cutsMade++;
                    GD.Print($"[AI] {team.Abbreviation} cut {candidate.Player.FullName} (OVR {candidate.Player.Overall})");
                }
            }

            // Strategy-based extra cuts for cap space
            if (profile.Strategy is AIStrategy.Rebuild or AIStrategy.TankMode)
            {
                // Rebuild teams cut overpaid declining veterans
                var veteranCuts = teamPlayers
                    .Where(p => p.CurrentContract != null && p.Age >= 30 && p.Overall < 70
                        && p.TeamId == team.Id && p.RosterStatus == RosterStatus.Active53)
                    .Select(p => new
                    {
                        Player = p,
                        CapHit = p.CurrentContract!.GetCapHit(GetCurrentYear()),
                        CapSavings = _capManager.CalculateCutCapSavings(p, GetCurrentYear(), false),
                    })
                    .Where(x => x.CapSavings > 0 && x.CapHit > 500_000_00L) // $5M+ cap hit
                    .OrderByDescending(x => x.CapSavings)
                    .Take(3)
                    .ToList();

                foreach (var vet in veteranCuts)
                {
                    var result = _rosterManager.CutPlayer(vet.Player.Id, team.Id);
                    if (result.Success)
                        GD.Print($"[AI] {team.Abbreviation} cut veteran {vet.Player.FullName} (cap savings: {vet.CapSavings / 100m:C0})");
                }
            }
        }
    }

    // --- AI Extensions ---

    public void RunAIExtensions()
    {
        var playerTeamId = _getPlayerTeamId();
        var players = _getPlayers();
        int currentYear = GetCurrentYear();

        foreach (var team in _getTeams())
        {
            if (team.Id == playerTeamId) continue;

            var profile = GetProfile(team.Id);
            var teamPlayers = players
                .Where(p => p.TeamId == team.Id && p.RosterStatus == RosterStatus.Active53 && p.CurrentContract != null)
                .ToList();

            // Find players whose contracts expire next year
            var expiringPlayers = teamPlayers
                .Where(p =>
                {
                    var contract = p.CurrentContract!;
                    var lastYear = contract.Years.MaxBy(y => y.Year);
                    return lastYear != null && lastYear.Year == currentYear;
                })
                .ToList();

            foreach (var player in expiringPlayers)
            {
                bool shouldExtend = profile.Strategy switch
                {
                    AIStrategy.WinNow or AIStrategy.Contend => player.Overall >= 75,
                    AIStrategy.Retool => player.Overall >= 78 && player.Age < 30,
                    AIStrategy.Rebuild => player.Age < 27 && player.Overall >= 70,
                    AIStrategy.TankMode => false,
                    _ => false,
                };

                if (!shouldExtend) continue;

                // Generate a reasonable extension
                var extension = GenerateExtension(player, currentYear, profile);
                if (extension == null) continue;

                // Check if team can afford it
                if (!_capManager.CanAffordContract(team, extension.AveragePerYear))
                    continue;

                var result = _rosterManager.ExtendContract(player.Id, extension);
                if (result.Success)
                    GD.Print($"[AI] {team.Abbreviation} extended {player.FullName} — {extension.TotalYears}yr/{extension.TotalValue / 100m:C0}");
            }
        }
    }

    // --- AI Depth Charts ---

    public void SetAIDepthCharts()
    {
        var playerTeamId = _getPlayerTeamId();
        var players = _getPlayers();

        foreach (var team in _getTeams())
        {
            if (team.Id == playerTeamId) continue;

            var teamPlayers = players
                .Where(p => p.TeamId == team.Id && p.RosterStatus == RosterStatus.Active53)
                .ToList();

            foreach (var pos in Enum.GetValues<Position>())
            {
                var posPlayers = teamPlayers
                    .Where(p => p.Position == pos)
                    .OrderByDescending(p => p.Overall)
                    .Select(p => p.Id)
                    .ToList();

                if (posPlayers.Count > 0)
                    team.DepthChart.Chart[pos] = posPlayers;
            }
        }
    }

    // --- AI Strategy Updates ---

    public void UpdateAIStrategies()
    {
        var playerTeamId = _getPlayerTeamId();
        var profiles = _getProfiles();
        var players = _getPlayers();

        foreach (var team in _getTeams())
        {
            if (team.Id == playerTeamId) continue;
            if (!profiles.ContainsKey(team.Id)) continue;

            var profile = profiles[team.Id];
            var teamPlayers = players
                .Where(p => p.TeamId == team.Id && p.RosterStatus == RosterStatus.Active53)
                .ToList();

            if (teamPlayers.Count == 0) continue;

            // Calculate average starter OVR
            float avgOvr = 0;
            int starterCount = 0;

            foreach (var pos in Enum.GetValues<Position>())
            {
                if (team.DepthChart.Chart.TryGetValue(pos, out var depthList) && depthList.Count > 0)
                {
                    var starter = _getPlayer(depthList[0]);
                    if (starter != null)
                    {
                        avgOvr += starter.Overall;
                        starterCount++;
                    }
                }
            }

            if (starterCount > 0)
                avgOvr /= starterCount;

            // Count young players
            int youngCount = teamPlayers.Count(p => p.Age <= 25);
            float youngRatio = (float)youngCount / teamPlayers.Count;

            // Determine strategy
            AIStrategy newStrategy;
            if (avgOvr > 78 && team.CapSpace > 0)
                newStrategy = AIStrategy.WinNow;
            else if (avgOvr > 72)
                newStrategy = AIStrategy.Contend;
            else if (avgOvr > 65)
                newStrategy = AIStrategy.Retool;
            else if (youngRatio > 0.4f)
                newStrategy = AIStrategy.Rebuild;
            else
                newStrategy = AIStrategy.Retool;

            profile.Strategy = newStrategy;
        }
    }

    // --- Helpers ---

    private float CalculatePlayerValue(Player player, AIGMProfile profile)
    {
        float value = player.Overall;

        // Young players are more valuable
        if (player.Age <= 25) value += 5;
        else if (player.Age >= 30) value -= 5;
        else if (player.Age >= 33) value -= 10;

        // Premium positions
        if (PremiumPositions.Contains(player.Position)) value += 3;

        // Dev trait bonus
        value += player.DevTrait switch
        {
            DevelopmentTrait.XFactor => 8,
            DevelopmentTrait.Superstar => 5,
            DevelopmentTrait.Star => 2,
            _ => 0,
        };

        // Rebuild teams value young players more
        if (profile.Strategy == AIStrategy.Rebuild && player.Age <= 25)
            value += 5;

        return value;
    }

    private Contract? GenerateExtension(Player player, int currentYear, AIGMProfile profile)
    {
        // Calculate market value based on OVR and position
        long baseAnnual = EstimateMarketValue(player);
        int years = player.Age switch
        {
            <= 25 => 4,
            <= 28 => 3,
            <= 31 => 2,
            _ => 1,
        };

        // Strategy discount/premium
        float multiplier = profile.Strategy switch
        {
            AIStrategy.WinNow => 1.1f,
            AIStrategy.Contend => 1.0f,
            AIStrategy.Retool => 0.9f,
            AIStrategy.Rebuild => 0.85f,
            _ => 1.0f,
        };

        long annualValue = (long)(baseAnnual * multiplier);
        long totalValue = annualValue * years;
        long guaranteed = (long)(totalValue * 0.5f);

        var contract = new Contract
        {
            PlayerId = player.Id,
            TeamId = player.TeamId ?? string.Empty,
            TotalYears = years,
            TotalValue = totalValue,
            TotalGuaranteed = guaranteed,
            Type = ContractType.Veteran,
        };

        // Generate yearly breakdowns
        for (int i = 0; i < years; i++)
        {
            int year = currentYear + 1 + i;
            long yearSalary = annualValue + (annualValue * i / 10); // Slight escalation
            long signingBonusPerYear = guaranteed / (years * 2) / years; // Prorated

            contract.Years.Add(new ContractYear
            {
                Year = year,
                YearNumber = i + 1,
                BaseSalary = yearSalary,
                SigningBonus = i == 0 ? guaranteed / 2 : 0,
                CapHit = yearSalary + signingBonusPerYear,
                DeadCap = signingBonusPerYear * (years - i),
                Guaranteed = i < years / 2 + 1 ? yearSalary : 0,
            });
        }

        return contract;
    }

    private long EstimateMarketValue(Player player)
    {
        // Base market value in cents based on position and OVR
        // OVR 80+ QB gets ~$40M+, OVR 60 gets ~$5M
        float posMultiplier = player.Position switch
        {
            Position.QB => 2.0f,
            Position.EDGE or Position.LT or Position.RT => 1.5f,
            Position.CB or Position.WR => 1.3f,
            Position.DT or Position.FS or Position.SS => 1.1f,
            _ => 1.0f,
        };

        // Scale from ~$2M (OVR 50) to ~$25M (OVR 90) base
        float ovrFactor = Math.Max(0, player.Overall - 50) / 40f; // 0 to 1
        long baseValue = (long)(200_000_00L + ovrFactor * ovrFactor * 2_300_000_00L); // exponential curve

        return (long)(baseValue * posMultiplier);
    }

    private AIGMProfile GetProfile(string teamId)
    {
        var profiles = _getProfiles();
        if (profiles.TryGetValue(teamId, out var profile))
            return profile;

        // Default profile
        return new AIGMProfile
        {
            TeamId = teamId,
            Strategy = AIStrategy.Contend,
            RiskTolerance = 0.5f,
            DraftPreference = 0.5f,
            FreeAgencyAggression = 0.5f,
            TradeFrequency = 0.5f,
            CompetitiveWindowYears = 3,
        };
    }

    private int GetCurrentYear()
    {
        // Infer from teams or use a safe default
        var teams = _getTeams();
        if (teams.Count > 0)
        {
            var player = _getPlayers().FirstOrDefault(p => p.CurrentContract?.Years.Count > 0);
            if (player?.CurrentContract != null)
                return player.CurrentContract.Years.Max(y => y.Year);
        }
        return DateTime.Now.Year;
    }
}
