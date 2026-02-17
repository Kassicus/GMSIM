using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class StaffSystem
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<List<Coach>> _getCoaches;
    private readonly Func<Random> _getRng;
    private readonly Func<string, Coach?> _getCoach;
    private readonly Func<string, Team?> _getTeam;
    private readonly Func<CalendarSystem> _getCalendar;
    private readonly Func<string> _getPlayerTeamId;

    private List<Coach> _coachingMarket = new();

    public IReadOnlyList<Coach> CoachingMarket => _coachingMarket;

    public StaffSystem(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<Coach>> getCoaches,
        Func<Random> getRng,
        Func<string, Coach?> getCoach,
        Func<string, Team?> getTeam,
        Func<CalendarSystem> getCalendar,
        Func<string> getPlayerTeamId)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getCoaches = getCoaches;
        _getRng = getRng;
        _getCoach = getCoach;
        _getTeam = getTeam;
        _getCalendar = getCalendar;
        _getPlayerTeamId = getPlayerTeamId;
    }

    // --- Scheme Fit ---

    public float CalculateSchemeFit(Team team, Coach? hc = null)
    {
        hc ??= team.HeadCoachId != null ? _getCoach(team.HeadCoachId) : null;
        if (hc == null) return 0f;

        var players = _getPlayers().Where(p => p.TeamId == team.Id).ToList();
        float offFit = CalculateOffenseSchemeFit(hc.PreferredOffense, players);
        float defFit = CalculateDefenseSchemeFit(hc.PreferredDefense, players);

        return Math.Clamp((offFit + defFit) / 2f, -1f, 1f);
    }

    private float CalculateOffenseSchemeFit(SchemeType scheme, List<Player> players)
    {
        float avgOvr = GetAvgOvr(players, Position.QB, Position.HB, Position.WR, Position.TE,
            Position.LT, Position.LG, Position.C, Position.RG, Position.RT);
        if (avgOvr == 0) return 0f;

        float targetOvr;
        switch (scheme)
        {
            case SchemeType.WestCoast:
            case SchemeType.ProStyle:
                targetOvr = GetAvgOvr(players, Position.QB, Position.WR, Position.TE);
                break;
            case SchemeType.AirRaid:
            case SchemeType.SpreadOption:
                targetOvr = GetAvgOvr(players, Position.QB, Position.WR);
                break;
            case SchemeType.RunHeavy:
                targetOvr = GetAvgOvr(players, Position.HB, Position.FB,
                    Position.LT, Position.LG, Position.C, Position.RG, Position.RT);
                break;
            case SchemeType.RPO:
                targetOvr = GetAvgOvr(players, Position.QB, Position.HB, Position.WR);
                break;
            default:
                return 0f;
        }

        // Compare scheme-critical positions to overall offense average
        // If scheme positions are stronger than average → good fit
        float diff = targetOvr - avgOvr;
        return Math.Clamp(diff / 10f, -1f, 1f);
    }

    private float CalculateDefenseSchemeFit(SchemeType scheme, List<Player> players)
    {
        float avgOvr = GetAvgOvr(players, Position.EDGE, Position.DT, Position.MLB,
            Position.OLB, Position.CB, Position.FS, Position.SS);
        if (avgOvr == 0) return 0f;

        float targetOvr;
        switch (scheme)
        {
            case SchemeType.Cover1ManPress:
                targetOvr = GetAvgOvr(players, Position.CB);
                break;
            case SchemeType.Cover3:
            case SchemeType.Cover2Tampa:
                targetOvr = GetAvgOvr(players, Position.FS, Position.SS, Position.MLB, Position.OLB);
                break;
            case SchemeType.ThreeFour:
                targetOvr = GetAvgOvr(players, Position.OLB, Position.MLB, Position.DT);
                break;
            case SchemeType.FourThree:
                targetOvr = GetAvgOvr(players, Position.EDGE, Position.DT);
                break;
            case SchemeType.Hybrid:
            case SchemeType.MultipleDefense:
                return 0.1f; // versatile scheme, slight positive (less mismatch)
            default:
                return 0f;
        }

        float diff = targetOvr - avgOvr;
        return Math.Clamp(diff / 10f, -1f, 1f);
    }

    private float GetAvgOvr(List<Player> players, params Position[] positions)
    {
        var matching = players.Where(p => positions.Contains(p.Position) && p.CurrentInjury == null).ToList();
        return matching.Count > 0 ? (float)matching.Average(p => p.Overall) : 0f;
    }

    // --- Coaching Sim Modifier (called by SimulationEngine) ---

    public float GetCoachingSimModifier(Team team)
    {
        float total = 0f;

        // HC GameManagement: ±5
        if (team.HeadCoachId != null)
        {
            var hc = _getCoach(team.HeadCoachId);
            if (hc != null)
            {
                total += (hc.GameManagement - 65f) / 5f;

                // Scheme fit: ±2.5
                float schemeFit = CalculateSchemeFit(team, hc);
                total += schemeFit * 2.5f;
            }
        }

        // OC OffenseRating: ±2.4
        if (team.OffensiveCoordinatorId != null)
        {
            var oc = _getCoach(team.OffensiveCoordinatorId);
            if (oc != null)
                total += (oc.OffenseRating - 65f) / 10f;
        }

        // DC DefenseRating: ±2.4
        if (team.DefensiveCoordinatorId != null)
        {
            var dc = _getCoach(team.DefensiveCoordinatorId);
            if (dc != null)
                total += (dc.DefenseRating - 65f) / 10f;
        }

        return total;
    }

    // --- Position Coach Development Bonus ---

    public float GetPositionCoachDevBonus(string teamId, Position pos)
    {
        var team = _getTeam(teamId);
        if (team == null) return 0f;

        CoachRole targetRole = MapPositionToCoachRole(pos);
        Coach? coach = FindCoachByRole(team, targetRole);
        if (coach == null) return 0f;

        // 90 dev → +12.5%, 65 dev → 0%, 50 dev → -7.5%
        return (coach.PlayerDevelopment - 65f) / 200f;
    }

    private static CoachRole MapPositionToCoachRole(Position pos)
    {
        return pos switch
        {
            Position.QB => CoachRole.QBCoach,
            Position.HB or Position.FB => CoachRole.RBCoach,
            Position.WR or Position.TE => CoachRole.WRCoach,
            Position.LT or Position.LG or Position.C or Position.RG or Position.RT => CoachRole.OLineCoach,
            Position.EDGE or Position.DT => CoachRole.DLineCoach,
            Position.MLB or Position.OLB => CoachRole.LBCoach,
            Position.CB or Position.FS or Position.SS => CoachRole.DBCoach,
            _ => CoachRole.HeadCoach,
        };
    }

    private Coach? FindCoachByRole(Team team, CoachRole role)
    {
        switch (role)
        {
            case CoachRole.HeadCoach:
                return team.HeadCoachId != null ? _getCoach(team.HeadCoachId) : null;
            case CoachRole.OffensiveCoordinator:
                return team.OffensiveCoordinatorId != null ? _getCoach(team.OffensiveCoordinatorId) : null;
            case CoachRole.DefensiveCoordinator:
                return team.DefensiveCoordinatorId != null ? _getCoach(team.DefensiveCoordinatorId) : null;
            case CoachRole.SpecialTeamsCoordinator:
                return team.SpecialTeamsCoordId != null ? _getCoach(team.SpecialTeamsCoordId) : null;
            default:
                foreach (var coachId in team.PositionCoachIds)
                {
                    var c = _getCoach(coachId);
                    if (c != null && c.Role == role) return c;
                }
                return null;
        }
    }

    // --- Coaching Carousel ---

    public List<string> RunCoachingCarousel()
    {
        var changes = new List<string>();
        var rng = _getRng();
        var teams = _getTeams();
        var playerTeamId = _getPlayerTeamId();

        // Phase 1: Evaluate and fire underperforming HCs (AI teams only)
        foreach (var team in teams)
        {
            if (team.Id == playerTeamId) continue;
            if (team.HeadCoachId == null) continue;

            var hc = _getCoach(team.HeadCoachId);
            if (hc == null) continue;

            bool shouldFire = ShouldFireHC(team, hc, rng);
            if (shouldFire)
            {
                changes.Add($"{team.FullName} fired HC {hc.FullName}");
                FireCoachInternal(team, hc);
                _coachingMarket.Add(hc);
                EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachFired, hc.Id, team.Id);
            }
        }

        // Phase 2: Generate new coaches for the market
        int marketSize = Math.Max(8, _coachingMarket.Count(c => c.Role == CoachRole.HeadCoach) + 5);
        var newCoaches = GenerateCoachingMarket(marketSize);
        _coachingMarket.AddRange(newCoaches);
        _getCoaches().AddRange(newCoaches);

        // Phase 3: AI teams hire head coaches
        foreach (var team in teams)
        {
            if (team.Id == playerTeamId) continue;
            if (team.HeadCoachId != null) continue;

            var bestHC = _coachingMarket
                .Where(c => c.TeamId == null)
                .OrderByDescending(c => c.Prestige * 0.4f + c.GameManagement * 0.3f
                    + c.OffenseRating * 0.15f + c.DefenseRating * 0.15f)
                .FirstOrDefault();

            if (bestHC != null)
            {
                HireCoachInternal(team, bestHC, CoachRole.HeadCoach);
                _coachingMarket.Remove(bestHC);
                changes.Add($"{team.FullName} hired HC {bestHC.FullName}");
                EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, bestHC.Id, team.Id, (int)CoachRole.HeadCoach);
            }
        }

        // Phase 4: Fill coordinator/position coach vacancies for AI teams
        foreach (var team in teams)
        {
            if (team.Id == playerTeamId) continue;
            FillVacancies(team, changes, rng);
        }

        return changes;
    }

    private bool ShouldFireHC(Team team, Coach hc, Random rng)
    {
        int winThreshold = team.OwnerPatience / 10; // patience 50 → need 5 wins
        int wins = team.CurrentRecord.Wins;

        if (wins < winThreshold)
        {
            // Below threshold: high chance to fire, reduced by HC prestige
            float fireChance = 0.7f - (hc.Prestige / 200f);
            return rng.NextDouble() < fireChance;
        }

        // Teams with low patience and mediocre record: small fire chance
        if (team.OwnerPatience < 30 && wins < 9)
            return rng.NextDouble() < 0.15f;

        return false;
    }

    // --- Player-Facing Methods ---

    public (bool Success, string Message) FireCoach(string coachId)
    {
        var playerTeamId = _getPlayerTeamId();
        var team = _getTeam(playerTeamId);
        if (team == null) return (false, "Team not found.");

        var coach = _getCoach(coachId);
        if (coach == null) return (false, "Coach not found.");
        if (coach.TeamId != playerTeamId) return (false, "Coach is not on your team.");

        FireCoachInternal(team, coach);
        _coachingMarket.Add(coach);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachFired, coachId, playerTeamId);
        return (true, $"Fired {coach.FullName}.");
    }

    public (bool Success, string Message) HireCoach(string coachId, CoachRole role)
    {
        var playerTeamId = _getPlayerTeamId();
        var team = _getTeam(playerTeamId);
        if (team == null) return (false, "Team not found.");

        var coach = _coachingMarket.FirstOrDefault(c => c.Id == coachId);
        if (coach == null) return (false, "Coach not available in market.");

        // Check if role is already filled
        if (IsRoleFilled(team, role))
            return (false, $"The {role} position is already filled. Fire the current coach first.");

        HireCoachInternal(team, coach, role);
        _coachingMarket.Remove(coach);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, coachId, playerTeamId, (int)role);
        return (true, $"Hired {coach.FullName} as {role}.");
    }

    public (bool Success, string Message) PromoteCoach(string coachId, CoachRole newRole)
    {
        var playerTeamId = _getPlayerTeamId();
        var team = _getTeam(playerTeamId);
        if (team == null) return (false, "Team not found.");

        var coach = _getCoach(coachId);
        if (coach == null) return (false, "Coach not found.");
        if (coach.TeamId != playerTeamId) return (false, "Coach is not on your team.");

        if (newRole != CoachRole.HeadCoach)
            return (false, "Can only promote to Head Coach.");
        if (IsRoleFilled(team, CoachRole.HeadCoach))
            return (false, "Head Coach position is already filled.");

        // Remove from old role
        RemoveCoachFromRole(team, coach);

        // Assign to new role
        coach.Role = newRole;
        team.HeadCoachId = coach.Id;

        // Boost GameManagement slightly for promotion
        coach.GameManagement = Math.Min(99, coach.GameManagement + 3);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, coachId, playerTeamId, (int)newRole);
        return (true, $"Promoted {coach.FullName} to Head Coach.");
    }

    // --- Internal Helpers ---

    private void FireCoachInternal(Team team, Coach coach)
    {
        RemoveCoachFromRole(team, coach);
        coach.TeamId = null;
    }

    private void HireCoachInternal(Team team, Coach coach, CoachRole role)
    {
        coach.TeamId = team.Id;
        coach.Role = role;

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

    private void RemoveCoachFromRole(Team team, Coach coach)
    {
        if (team.HeadCoachId == coach.Id) team.HeadCoachId = null;
        if (team.OffensiveCoordinatorId == coach.Id) team.OffensiveCoordinatorId = null;
        if (team.DefensiveCoordinatorId == coach.Id) team.DefensiveCoordinatorId = null;
        if (team.SpecialTeamsCoordId == coach.Id) team.SpecialTeamsCoordId = null;
        team.PositionCoachIds.Remove(coach.Id);
    }

    private bool IsRoleFilled(Team team, CoachRole role)
    {
        return role switch
        {
            CoachRole.HeadCoach => team.HeadCoachId != null,
            CoachRole.OffensiveCoordinator => team.OffensiveCoordinatorId != null,
            CoachRole.DefensiveCoordinator => team.DefensiveCoordinatorId != null,
            CoachRole.SpecialTeamsCoordinator => team.SpecialTeamsCoordId != null,
            _ => team.PositionCoachIds.Any(id =>
            {
                var c = _getCoach(id);
                return c != null && c.Role == role;
            }),
        };
    }

    private void FillVacancies(Team team, List<string> changes, Random rng)
    {
        var requiredRoles = new[]
        {
            CoachRole.OffensiveCoordinator, CoachRole.DefensiveCoordinator,
            CoachRole.SpecialTeamsCoordinator, CoachRole.QBCoach, CoachRole.RBCoach,
            CoachRole.WRCoach, CoachRole.OLineCoach, CoachRole.DLineCoach,
            CoachRole.LBCoach, CoachRole.DBCoach
        };

        foreach (var role in requiredRoles)
        {
            if (!IsRoleFilled(team, role))
            {
                // Try to hire from market first
                var candidate = _coachingMarket.FirstOrDefault(c => c.TeamId == null);
                if (candidate != null)
                {
                    HireCoachInternal(team, candidate, role);
                    _coachingMarket.Remove(candidate);
                }
                else
                {
                    // Generate a new coach
                    var newCoach = GenerateSingleCoach(role, rng);
                    _getCoaches().Add(newCoach);
                    HireCoachInternal(team, newCoach, role);
                }
            }
        }
    }

    // --- Market Generation ---

    public List<Coach> GenerateCoachingMarket(int count)
    {
        var rng = _getRng();
        var coaches = new List<Coach>();

        string[] firstNames = { "Bill", "Andy", "Mike", "Sean", "John", "Matt", "Kyle", "Dan",
            "Ron", "Pete", "Brian", "Kevin", "Todd", "Doug", "Jim", "Steve", "Tom", "Eric",
            "Dave", "Chris", "Rob", "Joe", "Nick", "Ben", "Frank" };
        string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Davis", "Wilson",
            "Moore", "Taylor", "Anderson", "Thomas", "Martinez", "Garcia", "Clark", "Lewis",
            "Robinson", "Walker", "Young", "Allen", "King", "Wright" };

        for (int i = 0; i < count; i++)
        {
            // First few are HC-caliber (higher ratings)
            bool isHCCaliberr = i < count / 3;
            int minRating = isHCCaliberr ? 55 : 40;
            int ratingRange = isHCCaliberr ? 35 : 45;

            var coach = new Coach
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstNames[rng.Next(firstNames.Length)],
                LastName = lastNames[rng.Next(lastNames.Length)],
                Age = 35 + rng.Next(25),
                Role = isHCCaliberr ? CoachRole.HeadCoach : (CoachRole)(1 + rng.Next(10)),
                TeamId = null,
                OffenseRating = minRating + rng.Next(ratingRange),
                DefenseRating = minRating + rng.Next(ratingRange),
                SpecialTeamsRating = 40 + rng.Next(45),
                GameManagement = minRating + rng.Next(ratingRange),
                PlayerDevelopment = minRating + rng.Next(ratingRange),
                Motivation = 40 + rng.Next(50),
                Adaptability = 40 + rng.Next(50),
                Recruiting = 40 + rng.Next(50),
                PreferredOffense = (SchemeType)rng.Next(6),
                PreferredDefense = (SchemeType)(6 + rng.Next(7)),
                Personality = (CoachPersonality)rng.Next(5),
                Prestige = isHCCaliberr ? 30 + rng.Next(50) : 10 + rng.Next(40),
                Experience = rng.Next(25),
            };
            coaches.Add(coach);
        }

        return coaches;
    }

    private Coach GenerateSingleCoach(CoachRole role, Random rng)
    {
        string[] firstNames = { "Bill", "Andy", "Mike", "Sean", "John", "Matt", "Kyle", "Dan" };
        string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Davis", "Wilson" };

        return new Coach
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = firstNames[rng.Next(firstNames.Length)],
            LastName = lastNames[rng.Next(lastNames.Length)],
            Age = 35 + rng.Next(25),
            Role = role,
            TeamId = null,
            OffenseRating = 40 + rng.Next(45),
            DefenseRating = 40 + rng.Next(45),
            SpecialTeamsRating = 40 + rng.Next(45),
            GameManagement = 40 + rng.Next(45),
            PlayerDevelopment = 40 + rng.Next(45),
            Motivation = 40 + rng.Next(50),
            Adaptability = 40 + rng.Next(50),
            Recruiting = 40 + rng.Next(50),
            PreferredOffense = (SchemeType)rng.Next(6),
            PreferredDefense = (SchemeType)(6 + rng.Next(7)),
            Personality = (CoachPersonality)rng.Next(5),
            Prestige = 10 + rng.Next(40),
            Experience = rng.Next(15),
        };
    }

    // --- Save/Load ---

    public (List<string> MarketIds, object _) GetState()
    {
        return (_coachingMarket.Select(c => c.Id).ToList(), null!);
    }

    public void SetState(List<string> marketIds)
    {
        _coachingMarket.Clear();
        var allCoaches = _getCoaches();
        foreach (var id in marketIds)
        {
            var coach = allCoaches.FirstOrDefault(c => c.Id == id);
            if (coach != null)
                _coachingMarket.Add(coach);
        }
    }
}
