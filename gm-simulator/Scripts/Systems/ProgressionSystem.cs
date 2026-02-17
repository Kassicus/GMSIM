using System.Reflection;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class ProgressionSystem
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<Random> _getRng;
    private readonly Func<string, Player?> _getPlayer;
    private readonly Func<string, Team?> _getTeam;
    private readonly Func<CalendarSystem> _getCalendar;
    private readonly StaffSystem _staff;

    private static readonly HashSet<string> MentalAttributes = new()
    {
        "Awareness", "Clutch", "Consistency", "Leadership", "PlayRecognition"
    };

    private static readonly PropertyInfo[] AttributeProperties =
        typeof(PlayerAttributes).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(int) && p.CanRead && p.CanWrite)
            .ToArray();

    public ProgressionSystem(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<Random> getRng,
        Func<string, Player?> getPlayer,
        Func<string, Team?> getTeam,
        Func<CalendarSystem> getCalendar,
        StaffSystem staff)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getRng = getRng;
        _getPlayer = getPlayer;
        _getTeam = getTeam;
        _getCalendar = getCalendar;
        _staff = staff;
    }

    // --- Age Brackets ---

    private enum AgeBracket { Growth, Peak, Decline, SharpDecline }

    private static AgeBracket GetAgeBracket(Position pos, int age)
    {
        var (growthEnd, peakEnd, declineEnd) = GetAgeCurve(pos);

        if (age <= growthEnd) return AgeBracket.Growth;
        if (age <= peakEnd) return AgeBracket.Peak;
        if (age <= declineEnd) return AgeBracket.Decline;
        return AgeBracket.SharpDecline;
    }

    private static (int growthEnd, int peakEnd, int declineEnd) GetAgeCurve(Position pos)
    {
        return pos switch
        {
            Position.QB => (26, 34, 37),
            Position.HB => (23, 27, 29),
            Position.FB => (24, 30, 33),
            Position.WR or Position.TE or Position.DT
                or Position.FS or Position.SS => (24, 30, 33),
            Position.LT or Position.LG or Position.C
                or Position.RG or Position.RT => (24, 32, 35),
            Position.EDGE or Position.MLB or Position.OLB => (24, 29, 32),
            Position.CB => (24, 28, 31),
            Position.K or Position.P => (24, 36, 40),
            Position.LS => (24, 34, 38),
            _ => (24, 30, 33),
        };
    }

    // --- Offseason Progression ---

    public ProgressionReport RunOffseasonProgression()
    {
        var report = new ProgressionReport();
        var rng = _getRng();
        var players = _getPlayers();

        foreach (var player in players)
        {
            if (player.RosterStatus == RosterStatus.Retired) continue;

            int oldOverall = player.Overall;
            var bracket = GetAgeBracket(player.Position, player.Age);

            // Dev trait modifier (flat, applied once)
            int devMod = player.DevTrait switch
            {
                DevelopmentTrait.XFactor => 2,
                DevelopmentTrait.Superstar => 1,
                DevelopmentTrait.Star => 0,
                DevelopmentTrait.Normal => -1,
                _ => 0,
            };

            // Coach dev bonus (scaled to int)
            int coachMod = 0;
            if (player.TeamId != null)
            {
                float coachBonus = _staff.GetPositionCoachDevBonus(player.TeamId, player.Position);
                coachMod = (int)Math.Round(coachBonus * 20f); // -1.5 to +2.5 → roughly -2 to +3
            }

            // Starter bonus
            int starterBonus = IsStarter(player) ? 1 : 0;

            // Trajectory modifier
            int trajectory = player.TrajectoryModifier;

            // Apply to each attribute individually
            foreach (var prop in AttributeProperties)
            {
                int current = (int)prop.GetValue(player.Attributes)!;

                // Base age curve roll (independent per attribute)
                int ageChange = bracket switch
                {
                    AgeBracket.Growth => rng.Next(1, 5),
                    AgeBracket.Peak => rng.Next(-1, 3),
                    AgeBracket.Decline => rng.Next(-3, 1),
                    AgeBracket.SharpDecline => rng.Next(-5, 0),
                    _ => 0,
                };

                // Mental attributes decline slower
                if (MentalAttributes.Contains(prop.Name) && ageChange < 0)
                    ageChange = Math.Min(ageChange + 1, 0);

                int totalChange = ageChange + devMod + coachMod + starterBonus + trajectory;
                int newValue = Math.Clamp(current + totalChange, 1, 99);
                prop.SetValue(player.Attributes, newValue);
            }

            // Recalculate overall
            player.Overall = OverallCalculator.Calculate(player.Position, player.Attributes);

            // Age the player
            player.Age++;
            player.YearsInLeague++;

            // Track changes
            int change = player.Overall - oldOverall;
            if (change > 0)
                report.Improved.Add((player.Id, player.FullName, oldOverall, player.Overall));
            else if (change < 0)
                report.Declined.Add((player.Id, player.FullName, oldOverall, player.Overall));
        }

        return report;
    }

    // --- Retirement ---

    public List<(string playerId, string reason)> ProcessRetirements()
    {
        var retirements = new List<(string, string)>();
        var rng = _getRng();
        var players = _getPlayers();

        foreach (var player in players)
        {
            if (player.RosterStatus == RosterStatus.Retired) continue;

            var bracket = GetAgeBracket(player.Position, player.Age);
            var (_, _, declineEnd) = GetAgeCurve(player.Position);

            // Base probability
            float prob = bracket switch
            {
                AgeBracket.Growth => 0f,
                AgeBracket.Peak => 0.01f,
                AgeBracket.Decline => 0.05f + 0.03f * (player.Age - (declineEnd - 2)),
                AgeBracket.SharpDecline => 0.15f + 0.05f * (player.Age - declineEnd),
                _ => 0f,
            };

            // OVR modifiers
            if (player.Overall < 45) prob += 0.25f;
            else if (player.Overall < 55) prob += 0.10f;

            // Free agent more likely to retire
            if (player.RosterStatus == RosterStatus.FreeAgent) prob += 0.08f;

            prob = Math.Clamp(prob, 0f, 0.95f);

            if (prob > 0 && rng.NextDouble() < prob)
            {
                string reason = bracket switch
                {
                    AgeBracket.SharpDecline => "age",
                    _ when player.Overall < 55 => "declining performance",
                    _ when player.RosterStatus == RosterStatus.FreeAgent => "unsigned",
                    _ => "personal decision",
                };

                RetirePlayer(player);
                retirements.Add((player.Id, reason));
            }
        }

        return retirements;
    }

    private void RetirePlayer(Player player)
    {
        player.RosterStatus = RosterStatus.Retired;

        // Remove from team roster
        if (player.TeamId != null)
        {
            var team = _getTeam(player.TeamId);
            if (team != null)
            {
                team.PlayerIds.Remove(player.Id);
                if (team.DepthChart.Chart.TryGetValue(player.Position, out var depthList))
                    depthList.Remove(player.Id);
            }
            player.TeamId = null;
        }

        player.CurrentContract = null;

        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerRetired, player.Id);
    }

    // --- Dev Trait Changes ---

    public void ProcessDevTraitChanges(SeasonAwards awards)
    {
        var rng = _getRng();
        var players = _getPlayers();

        foreach (var player in players)
        {
            if (player.RosterStatus == RosterStatus.Retired) continue;

            bool isProBowl = awards.ProBowlIds.Contains(player.Id);
            bool isAllPro = awards.FirstTeamAllPro.Contains(player.Id)
                || awards.SecondTeamAllPro.Contains(player.Id);

            if (isProBowl || isAllPro)
            {
                float upgradeChance = isAllPro ? 0.25f : 0.20f;

                if (player.DevTrait == DevelopmentTrait.Superstar && rng.NextDouble() < 0.02f)
                    player.DevTrait = DevelopmentTrait.XFactor;
                else if (player.DevTrait == DevelopmentTrait.Star && rng.NextDouble() < upgradeChance)
                    player.DevTrait = DevelopmentTrait.Superstar;
                else if (player.DevTrait == DevelopmentTrait.Normal && rng.NextDouble() < upgradeChance)
                    player.DevTrait = DevelopmentTrait.Star;
            }
        }
    }

    // --- Owner Patience & Fan Satisfaction ---

    public void UpdateOwnerPatience(Team team, int wins, int losses, bool madePlayoffs, bool madeSuperBowl)
    {
        int change = 0;

        if (wins >= 9) change += 5;
        if (madePlayoffs) change += 8;
        if (madeSuperBowl) change += 15;
        if (wins < 6) change -= 8;

        // Consecutive losing: check FanSatisfaction as proxy (low satisfaction = sustained losing)
        if (wins < 6 && team.FanSatisfaction < 40) change -= 5;

        team.OwnerPatience = Math.Clamp(team.OwnerPatience + change, 0, 100);

        if (team.OwnerPatience < 10)
        {
            EventBus.Instance?.EmitSignal(
                EventBus.SignalName.OwnerPatienceLow, team.Id, team.OwnerPatience);
        }
    }

    public void UpdateFanSatisfaction(Team team, int wins, bool madePlayoffs, bool wonSuperBowl)
    {
        int change = 0;

        if (wins >= 9) change += 10;
        if (madePlayoffs) change += 12;
        if (wonSuperBowl) change += 25;
        if (wins < 6) change -= 10;

        team.FanSatisfaction = Math.Clamp(team.FanSatisfaction + change, 0, 100);
    }

    // --- Helpers ---

    private bool IsStarter(Player player)
    {
        if (player.TeamId == null) return false;

        var team = _getTeam(player.TeamId);
        if (team == null) return false;

        if (team.DepthChart.Chart.TryGetValue(player.Position, out var depthList))
            return depthList.Count > 0 && depthList[0] == player.Id;

        return false;
    }
}

// --- Report Models ---

public class ProgressionReport
{
    public List<(string PlayerId, string Name, int OldOvr, int NewOvr)> Improved { get; set; } = new();
    public List<(string PlayerId, string Name, int OldOvr, int NewOvr)> Declined { get; set; } = new();
}
