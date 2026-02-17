using System.Text.Json;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

/// <summary>
/// Manages all salary cap operations. Plain C# class owned by GameManager.
/// All monetary values are in cents (long).
/// </summary>
public class SalaryCapManager
{
    private Dictionary<int, long> _capByYear = new();
    private Dictionary<Position, long> _franchiseTagValues = new();
    private int _maxProrationYears = 5;
    private int _activeRosterSize = 53;
    private int _practiceSquadSize = 16;
    private int _practiceSquadVeteranSlots = 6;
    private int _irMinimumGames = 4;
    private float _capGrowthRateMin = 0.04f;
    private float _capGrowthRateMax = 0.07f;

    public int ActiveRosterSize => _activeRosterSize;
    public int PracticeSquadSize => _practiceSquadSize;
    public int PracticeSquadVeteranSlots => _practiceSquadVeteranSlots;
    public int IRMinimumGames => _irMinimumGames;

    public void LoadRules(string dataPath)
    {
        string json = File.ReadAllText(Path.Combine(dataPath, "salary_cap_rules.json"));
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("capByYear", out var capByYear))
        {
            foreach (var prop in capByYear.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out int year))
                    _capByYear[year] = prop.Value.GetInt64();
            }
        }

        if (root.TryGetProperty("maxSigningBonusProrationYears", out var maxPro))
            _maxProrationYears = maxPro.GetInt32();
        if (root.TryGetProperty("activeRosterSize", out var ars))
            _activeRosterSize = ars.GetInt32();
        if (root.TryGetProperty("practiceSquadSize", out var pss))
            _practiceSquadSize = pss.GetInt32();
        if (root.TryGetProperty("practiceSquadVeteranSlots", out var psvs))
            _practiceSquadVeteranSlots = psvs.GetInt32();
        if (root.TryGetProperty("irMinimumGames", out var irMin))
            _irMinimumGames = irMin.GetInt32();
        if (root.TryGetProperty("capGrowthRateMin", out var cgMin))
            _capGrowthRateMin = (float)cgMin.GetDouble();
        if (root.TryGetProperty("capGrowthRateMax", out var cgMax))
            _capGrowthRateMax = (float)cgMax.GetDouble();

        if (root.TryGetProperty("franchiseTagEstimatesByPosition", out var tagEst))
        {
            foreach (var prop in tagEst.EnumerateObject())
            {
                if (Enum.TryParse<Position>(prop.Name, out var pos))
                    _franchiseTagValues[pos] = prop.Value.GetInt64();
            }
        }
    }

    public void RecalculateTeamCap(Team team, List<Player> allPlayers, int currentYear)
    {
        long totalCapUsed = 0;
        var allRosterIds = team.PlayerIds
            .Concat(team.PracticeSquadIds)
            .Concat(team.IRPlayerIds);

        foreach (var playerId in allRosterIds)
        {
            var player = allPlayers.FirstOrDefault(p => p.Id == playerId);
            if (player?.CurrentContract != null)
                totalCapUsed += player.CurrentContract.GetCapHit(currentYear);
        }

        team.CurrentCapUsed = totalCapUsed;
    }

    public long GetCapForYear(int year)
    {
        if (_capByYear.TryGetValue(year, out long cap))
            return cap;

        // Project future cap using average growth rate
        int maxKnownYear = _capByYear.Keys.DefaultIfEmpty(2025).Max();
        long maxKnownCap = _capByYear.GetValueOrDefault(maxKnownYear, 25540000000L);
        float avgGrowth = (_capGrowthRateMin + _capGrowthRateMax) / 2f;

        int yearsAhead = year - maxKnownYear;
        if (yearsAhead <= 0) return maxKnownCap;

        return (long)(maxKnownCap * Math.Pow(1 + avgGrowth, yearsAhead));
    }

    public long GetAdjustedCap(Team team, int year)
    {
        return GetCapForYear(year) + team.CarryoverCap;
    }

    public (long ThisYear, long NextYear) CalculateCutDeadCap(Player player, int currentYear, bool postJune1)
    {
        if (player.CurrentContract == null)
            return (0, 0);

        long totalDeadCap = player.CurrentContract.CalculateDeadCap(currentYear);

        if (!postJune1 || totalDeadCap == 0)
            return (totalDeadCap, 0);

        // Post-June 1: current year gets only this year's prorated bonus portion,
        // next year gets the rest
        var currentContractYear = player.CurrentContract.Years
            .FirstOrDefault(y => y.Year == currentYear);
        long thisYearPortion = currentContractYear?.DeadCap ?? 0;
        // Clamp: this year can't exceed total
        if (thisYearPortion > totalDeadCap) thisYearPortion = totalDeadCap;
        long nextYearPortion = totalDeadCap - thisYearPortion;

        return (thisYearPortion, nextYearPortion);
    }

    public long CalculateCutCapSavings(Player player, int currentYear, bool postJune1)
    {
        if (player.CurrentContract == null) return 0;

        long currentCapHit = player.CurrentContract.GetCapHit(currentYear);
        var (thisYearDead, _) = CalculateCutDeadCap(player, currentYear, postJune1);

        // Savings = current cap hit minus the dead cap that hits this year
        long savings = currentCapHit - thisYearDead;
        return Math.Max(savings, -currentCapHit); // Can be negative if dead cap > cap hit
    }

    public List<(Player Player, long CapHit)> GetTopCapHits(
        Team team, List<Player> allPlayers, int currentYear, int count = 5)
    {
        var allRosterIds = team.PlayerIds
            .Concat(team.PracticeSquadIds)
            .Concat(team.IRPlayerIds)
            .ToHashSet();

        return allPlayers
            .Where(p => allRosterIds.Contains(p.Id) && p.CurrentContract != null)
            .Select(p => (Player: p, CapHit: p.CurrentContract!.GetCapHit(currentYear)))
            .OrderByDescending(x => x.CapHit)
            .Take(count)
            .ToList();
    }

    public Dictionary<int, long> GetCapProjections(
        Team team, List<Player> allPlayers, int currentYear, int yearsAhead = 3)
    {
        var projections = new Dictionary<int, long>();
        var allRosterIds = team.PlayerIds
            .Concat(team.PracticeSquadIds)
            .Concat(team.IRPlayerIds)
            .ToHashSet();

        for (int offset = 0; offset <= yearsAhead; offset++)
        {
            int year = currentYear + offset;
            long committed = 0;

            foreach (var player in allPlayers.Where(p => allRosterIds.Contains(p.Id)))
            {
                if (player.CurrentContract != null)
                    committed += player.CurrentContract.GetCapHit(year);
            }

            projections[year] = committed;
        }

        return projections;
    }

    public bool HasCapSpace(Team team, long additionalCapHit)
    {
        return team.CapSpace >= additionalCapHit;
    }

    public bool IsPostJune1(GamePhase phase)
    {
        return phase >= GamePhase.Preseason;
    }

    public bool RestructureContract(Player player, int currentYear, long amountToConvert)
    {
        if (player.CurrentContract == null) return false;

        var contract = player.CurrentContract;
        var currentContractYear = contract.Years.FirstOrDefault(y => y.Year == currentYear);
        if (currentContractYear == null) return false;
        if (amountToConvert > currentContractYear.BaseSalary) return false;

        // Count remaining years for proration
        var remainingYears = contract.Years.Where(y => y.Year >= currentYear).ToList();
        int prorationYears = Math.Min(remainingYears.Count, _maxProrationYears);
        if (prorationYears <= 1) return false; // No benefit to restructure 1-year deal

        long proratedAmount = amountToConvert / prorationYears;

        // Reduce current year base salary, add prorated bonus to all remaining years
        currentContractYear.BaseSalary -= amountToConvert;
        currentContractYear.SigningBonus += amountToConvert;

        foreach (var year in remainingYears.Take(prorationYears))
        {
            year.CapHit = year.BaseSalary + year.SigningBonus / prorationYears + proratedAmount;
            year.DeadCap += proratedAmount * (prorationYears - remainingYears.IndexOf(year));
        }

        // Recalculate cap hit for current year
        currentContractYear.CapHit = currentContractYear.BaseSalary + proratedAmount;

        return true;
    }

    public (long Savings, long FutureHitPerYear) CalculateRestructureImpact(
        Player player, int currentYear, long amountToConvert)
    {
        if (player.CurrentContract == null) return (0, 0);

        var contract = player.CurrentContract;
        var remainingYears = contract.Years.Where(y => y.Year >= currentYear).ToList();
        int prorationYears = Math.Min(remainingYears.Count, _maxProrationYears);
        if (prorationYears <= 1) return (0, 0);

        long proratedAmount = amountToConvert / prorationYears;
        long savings = amountToConvert - proratedAmount; // This year saves (amount - prorated portion)

        return (savings, proratedAmount);
    }

    public long CalculateFranchiseTagValue(Position pos)
    {
        return _franchiseTagValues.GetValueOrDefault(pos, 1500000000L); // default ~$15M
    }

    public long CalculateTransitionTagValue(Position pos)
    {
        return (long)(CalculateFranchiseTagValue(pos) * 0.8);
    }

    public bool CanAffordContract(Team team, long annualCapHit)
    {
        return team.CapSpace >= annualCapHit;
    }
}
