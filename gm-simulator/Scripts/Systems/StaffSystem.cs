using Godot;
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
    private readonly Func<HashSet<string>> _getActivePlayoffTeamIds;

    private List<Coach> _coachingMarket = new();

    // Coaching Market state
    private List<InterviewRequest> _interviewRequests = new();
    private HashSet<string> _promotionIntentCoachIds = new();      // Player's protection list
    private Dictionary<string, string> _aiPromotionIntents = new(); // AI teamId -> coachId

    public IReadOnlyList<Coach> CoachingMarket => _coachingMarket;
    public IReadOnlyList<InterviewRequest> InterviewRequests => _interviewRequests;

    public StaffSystem(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<Coach>> getCoaches,
        Func<Random> getRng,
        Func<string, Coach?> getCoach,
        Func<string, Team?> getTeam,
        Func<CalendarSystem> getCalendar,
        Func<string> getPlayerTeamId,
        Func<HashSet<string>>? getActivePlayoffTeamIds = null)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getCoaches = getCoaches;
        _getRng = getRng;
        _getCoach = getCoach;
        _getTeam = getTeam;
        _getCalendar = getCalendar;
        _getPlayerTeamId = getPlayerTeamId;
        _getActivePlayoffTeamIds = getActivePlayoffTeamIds ?? (() => new HashSet<string>());
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
                return 0.1f;
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

        if (team.HeadCoachId != null)
        {
            var hc = _getCoach(team.HeadCoachId);
            if (hc != null)
            {
                total += (hc.GameManagement - 65f) / 5f;
                float schemeFit = CalculateSchemeFit(team, hc);
                total += schemeFit * 2.5f;
            }
        }

        if (team.OffensiveCoordinatorId != null)
        {
            var oc = _getCoach(team.OffensiveCoordinatorId);
            if (oc != null)
                total += (oc.OffenseRating - 65f) / 10f;
        }

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

    // =====================================================
    // COACHING MARKET — Market Lifecycle Methods
    // =====================================================

    /// <summary>
    /// Phase 1 of market opening: Fire underperforming AI HCs based on owner patience.
    /// </summary>
    public List<string> FireUnderperformingHCs()
    {
        var changes = new List<string>();
        var rng = _getRng();
        var teams = _getTeams();
        var playerTeamId = _getPlayerTeamId();

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
                CleanupInterviewsForCoach(hc.Id);
                EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachFired, hc.Id, team.Id);
            }
        }

        return changes;
    }

    /// <summary>
    /// Phase 2 of market opening: Generate 8-13 free agent coaches for the market.
    /// </summary>
    public void GenerateAndAddMarketCoaches()
    {
        int marketSize = Math.Max(8, _coachingMarket.Count(c => c.Role == CoachRole.HeadCoach) + 5);
        marketSize = Math.Min(marketSize, 13);
        var newCoaches = GenerateCoachingMarket(marketSize);
        _coachingMarket.AddRange(newCoaches);
        _getCoaches().AddRange(newCoaches);
        GD.Print($"Coaching market: Generated {newCoaches.Count} free agent coaches. Market total: {_coachingMarket.Count}");
    }

    /// <summary>
    /// Determine which AI teams declare promotion intent for their OC/DC when HC is fired.
    /// ~30% chance per AI team with vacant HC slot.
    /// </summary>
    public void DetermineAIPromotionIntents()
    {
        _aiPromotionIntents.Clear();
        var rng = _getRng();
        var teams = _getTeams();
        var playerTeamId = _getPlayerTeamId();

        foreach (var team in teams)
        {
            if (team.Id == playerTeamId) continue;
            if (team.HeadCoachId != null) continue; // Only teams with HC vacancy

            if (rng.NextDouble() < 0.3)
            {
                // Pick best OC or DC as internal candidate
                Coach? bestCandidate = null;
                float bestScore = -1;

                if (team.OffensiveCoordinatorId != null)
                {
                    var oc = _getCoach(team.OffensiveCoordinatorId);
                    if (oc != null)
                    {
                        float score = oc.GameManagement * 0.4f + oc.OffenseRating * 0.3f + oc.Prestige * 0.3f;
                        if (score > bestScore) { bestScore = score; bestCandidate = oc; }
                    }
                }

                if (team.DefensiveCoordinatorId != null)
                {
                    var dc = _getCoach(team.DefensiveCoordinatorId);
                    if (dc != null)
                    {
                        float score = dc.GameManagement * 0.4f + dc.DefenseRating * 0.3f + dc.Prestige * 0.3f;
                        if (score > bestScore) { bestScore = score; bestCandidate = dc; }
                    }
                }

                if (bestCandidate != null)
                {
                    _aiPromotionIntents[team.Id] = bestCandidate.Id;
                    GD.Print($"Coaching market: {team.FullName} declares promotion intent for {bestCandidate.FullName}");
                }
            }
        }
    }

    /// <summary>
    /// Weekly AI coaching market processing. Called each week during market window.
    /// </summary>
    public void ProcessCoachingMarketWeek(int week)
    {
        var rng = _getRng();
        var teams = _getTeams();
        var playerTeamId = _getPlayerTeamId();
        var activePlayoffTeamIds = _getActivePlayoffTeamIds();

        GD.Print($"Coaching market week {week}: Processing AI activity...");

        // Weeks 1-2: AI fires underperforming non-HC staff, sends HC interview requests
        if (week <= 2)
        {
            foreach (var team in teams)
            {
                if (team.Id == playerTeamId) continue;
                if (activePlayoffTeamIds.Contains(team.Id)) continue; // Playoff teams can't act
                if (team.HeadCoachId != null) continue; // Only teams needing HC

                // Send interview requests for HC candidates from other teams
                AIRequestHCInterviews(team, rng);
            }
        }

        // Weeks 3-4: AI hires HCs from market + approved interviews
        if (week >= 3 && week <= 4)
        {
            foreach (var team in teams)
            {
                if (team.Id == playerTeamId) continue;
                if (activePlayoffTeamIds.Contains(team.Id)) continue;
                if (team.HeadCoachId != null) continue;

                // First try approved interviews
                var approvedHC = _interviewRequests
                    .Where(r => r.RequestingTeamId == team.Id
                        && r.Status == InterviewStatus.Approved
                        && r.TargetRole == CoachRole.HeadCoach)
                    .OrderByDescending(r =>
                    {
                        var c = _getCoach(r.CoachId);
                        return c != null ? c.Prestige * 0.4f + c.GameManagement * 0.3f
                            + c.OffenseRating * 0.15f + c.DefenseRating * 0.15f : 0f;
                    })
                    .FirstOrDefault();

                if (approvedHC != null)
                {
                    var coach = _getCoach(approvedHC.CoachId);
                    if (coach != null)
                    {
                        // Poach from old team
                        var oldTeam = _getTeam(approvedHC.CurrentTeamId);
                        if (oldTeam != null)
                            RemoveCoachFromRole(oldTeam, coach);

                        HireCoachInternal(team, coach, CoachRole.HeadCoach);
                        approvedHC.Status = InterviewStatus.Hired;
                        ExpireOtherRequestsForCoach(coach.Id, approvedHC.Id);
                        _coachingMarket.Remove(coach);
                        GD.Print($"Coaching market: {team.FullName} hired HC {coach.FullName} (from interview)");
                        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, coach.Id, team.Id, (int)CoachRole.HeadCoach);
                        continue;
                    }
                }

                // Check if team declared promotion intent — promote that coach
                if (_aiPromotionIntents.TryGetValue(team.Id, out var promoCoachId))
                {
                    var promoCoach = _getCoach(promoCoachId);
                    if (promoCoach != null && promoCoach.TeamId == team.Id)
                    {
                        RemoveCoachFromRole(team, promoCoach);
                        promoCoach.Role = CoachRole.HeadCoach;
                        team.HeadCoachId = promoCoach.Id;
                        promoCoach.GameManagement = Math.Min(99, promoCoach.GameManagement + 3);
                        _aiPromotionIntents.Remove(team.Id);
                        GD.Print($"Coaching market: {team.FullName} promoted {promoCoach.FullName} to HC");
                        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, promoCoach.Id, team.Id, (int)CoachRole.HeadCoach);
                        continue;
                    }
                }

                // Fall back to market free agents
                var bestHC = _coachingMarket
                    .Where(c => c.TeamId == null)
                    .OrderByDescending(c => c.Prestige * 0.4f + c.GameManagement * 0.3f
                        + c.OffenseRating * 0.15f + c.DefenseRating * 0.15f)
                    .FirstOrDefault();

                if (bestHC != null)
                {
                    HireCoachInternal(team, bestHC, CoachRole.HeadCoach);
                    _coachingMarket.Remove(bestHC);
                    ExpireOtherRequestsForCoach(bestHC.Id, null);
                    GD.Print($"Coaching market: {team.FullName} hired HC {bestHC.FullName} (from market)");
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, bestHC.Id, team.Id, (int)CoachRole.HeadCoach);
                }
            }
        }

        // Weeks 4-5: AI fills coordinator and position coach vacancies
        if (week >= 4)
        {
            foreach (var team in teams)
            {
                if (team.Id == playerTeamId) continue;
                if (activePlayoffTeamIds.Contains(team.Id)) continue;

                var changes = new List<string>();
                FillVacancies(team, changes, rng);
                foreach (var change in changes)
                    GD.Print($"Coaching market: {change}");
            }
        }
    }

    private void AIRequestHCInterviews(Team requestingTeam, Random rng)
    {
        var activePlayoffTeamIds = _getActivePlayoffTeamIds();
        var candidates = GetInterviewCandidatesForTeam(requestingTeam.Id);

        // Pick up to 2 candidates to interview
        var topCandidates = candidates
            .OrderByDescending(c => c.GameManagement * 0.3f + c.OffenseRating * 0.2f
                + c.DefenseRating * 0.2f + c.Prestige * 0.3f)
            .Take(2);

        foreach (var candidate in topCandidates)
        {
            if (rng.NextDouble() > 0.6) continue; // 60% chance to actually request

            // Already have a pending/approved request for this coach?
            if (_interviewRequests.Any(r => r.RequestingTeamId == requestingTeam.Id
                && r.CoachId == candidate.Id
                && (r.Status == InterviewStatus.Pending || r.Status == InterviewStatus.Approved)))
                continue;

            var result = CreateInterviewRequest(requestingTeam.Id, candidate.Id,
                candidate.TeamId ?? "", CoachRole.HeadCoach);

            // If targeting player's coach, emit signal
            if (candidate.TeamId == _getPlayerTeamId() && result.request != null)
            {
                EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerCoachTargeted,
                    candidate.Id, requestingTeam.Id, (int)CoachRole.HeadCoach);
            }
        }
    }

    /// <summary>
    /// End of market window: auto-fill all remaining vacancies, expire pending requests.
    /// </summary>
    public void CloseMarketFillVacancies()
    {
        var rng = _getRng();
        var teams = _getTeams();
        var playerTeamId = _getPlayerTeamId();

        // Expire all pending/approved requests
        foreach (var req in _interviewRequests.Where(r =>
            r.Status == InterviewStatus.Pending || r.Status == InterviewStatus.Approved))
        {
            req.Status = InterviewStatus.Expired;
        }

        // Fill all AI vacancies (including HC)
        foreach (var team in teams)
        {
            if (team.Id == playerTeamId) continue;

            // Fill HC if still vacant
            if (team.HeadCoachId == null)
            {
                var bestHC = _coachingMarket
                    .Where(c => c.TeamId == null)
                    .OrderByDescending(c => c.Prestige * 0.4f + c.GameManagement * 0.3f
                        + c.OffenseRating * 0.15f + c.DefenseRating * 0.15f)
                    .FirstOrDefault();

                if (bestHC != null)
                {
                    HireCoachInternal(team, bestHC, CoachRole.HeadCoach);
                    _coachingMarket.Remove(bestHC);
                    GD.Print($"Market close: {team.FullName} hired HC {bestHC.FullName}");
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, bestHC.Id, team.Id, (int)CoachRole.HeadCoach);
                }
                else
                {
                    var newHC = GenerateSingleCoach(CoachRole.HeadCoach, rng);
                    _getCoaches().Add(newHC);
                    HireCoachInternal(team, newHC, CoachRole.HeadCoach);
                    GD.Print($"Market close: {team.FullName} hired generated HC {newHC.FullName}");
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, newHC.Id, team.Id, (int)CoachRole.HeadCoach);
                }
            }

            // Fill remaining vacancies
            var changes = new List<string>();
            FillVacancies(team, changes, rng);
        }

        // Clear AI promotion intents
        _aiPromotionIntents.Clear();

        GD.Print("Coaching market closed. All vacancies filled.");
    }

    // =====================================================
    // COACHING MARKET — Interview System
    // =====================================================

    /// <summary>
    /// Player requests an interview with an employed coach for a target role.
    /// Returns (success, message, request).
    /// </summary>
    public (bool Success, string Message, InterviewRequest? Request) RequestInterview(string coachId, CoachRole targetRole)
    {
        var playerTeamId = _getPlayerTeamId();
        var coach = _getCoach(coachId);
        if (coach == null)
            return (false, "Coach not found.", null);

        if (coach.TeamId == null)
            return (false, "Coach is a free agent — hire directly from the market.", null);

        if (coach.TeamId == playerTeamId)
            return (false, "Coach is already on your team.", null);

        if (coach.Role == CoachRole.HeadCoach)
            return (false, "Cannot interview another team's head coach.", null);

        var activePlayoffTeamIds = _getActivePlayoffTeamIds();
        if (activePlayoffTeamIds.Contains(coach.TeamId))
            return (false, "Coach's team is still in the playoffs.", null);

        // Check for existing request
        if (_interviewRequests.Any(r => r.RequestingTeamId == playerTeamId
            && r.CoachId == coachId
            && (r.Status == InterviewStatus.Pending || r.Status == InterviewStatus.Approved)))
            return (false, "You already have an active interview request for this coach.", null);

        var result = CreateInterviewRequest(playerTeamId, coachId, coach.TeamId, targetRole);
        return result;
    }

    private (bool Success, string Message, InterviewRequest? request) CreateInterviewRequest(
        string requestingTeamId, string coachId, string currentTeamId, CoachRole targetRole)
    {
        var coach = _getCoach(coachId);
        if (coach == null)
            return (false, "Coach not found.", null);

        var calendar = _getCalendar();
        var request = new InterviewRequest
        {
            Id = Guid.NewGuid().ToString(),
            RequestingTeamId = requestingTeamId,
            CoachId = coachId,
            CurrentTeamId = currentTeamId,
            TargetRole = targetRole,
            RequestWeek = calendar.CurrentWeek,
        };

        // Evaluate blocking
        var (blocked, reason) = EvaluateBlocking(coach, currentTeamId, targetRole);

        if (blocked)
        {
            request.Status = InterviewStatus.Blocked;
            request.BlockReason = reason;
            request.Notes = reason == BlockReason.LateralMove
                ? "Blocked: lateral move (same role)"
                : "Blocked: team has declared promotion intent";
            _interviewRequests.Add(request);

            EventBus.Instance?.EmitSignal(EventBus.SignalName.InterviewBlocked,
                request.Id, coachId, reason.ToString());

            GD.Print($"Interview BLOCKED: {coach.FullName} to {_getTeam(requestingTeamId)?.FullName} as {targetRole} — {reason}");
            return (true, $"Interview blocked: {request.Notes}", request);
        }

        request.Status = InterviewStatus.Approved;
        request.BlockReason = BlockReason.None;
        _interviewRequests.Add(request);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.InterviewRequested,
            request.Id, coachId, requestingTeamId);

        GD.Print($"Interview APPROVED: {coach.FullName} to {_getTeam(requestingTeamId)?.FullName} as {targetRole}");
        return (true, $"Interview approved for {coach.FullName} as {targetRole}.", request);
    }

    private (bool Blocked, BlockReason Reason) EvaluateBlocking(Coach coach, string currentTeamId, CoachRole targetRole)
    {
        // Rule 1: Lateral move (same role) → BLOCK
        if (coach.Role == targetRole)
            return (true, BlockReason.LateralMove);

        // Rule 2: Target is HC, current team has no HC, and coach is declared promotion intent → BLOCK
        if (targetRole == CoachRole.HeadCoach)
        {
            var currentTeam = _getTeam(currentTeamId);
            if (currentTeam != null && currentTeam.HeadCoachId == null)
            {
                // Check player's protection list
                if (currentTeamId == _getPlayerTeamId() && _promotionIntentCoachIds.Contains(coach.Id))
                    return (true, BlockReason.PlannedPromotion);

                // Check AI promotion intents
                if (_aiPromotionIntents.TryGetValue(currentTeamId, out var intentCoachId) && intentCoachId == coach.Id)
                    return (true, BlockReason.PlannedPromotion);
            }
        }

        // Rule 3: All other cases → APPROVE
        return (false, BlockReason.None);
    }

    /// <summary>
    /// Hire a coach from an approved interview. Removes from old team, assigns to requesting team.
    /// </summary>
    public (bool Success, string Message) HireFromInterview(string requestId)
    {
        var request = _interviewRequests.FirstOrDefault(r => r.Id == requestId);
        if (request == null)
            return (false, "Interview request not found.");

        if (request.Status != InterviewStatus.Approved)
            return (false, $"Interview is not approved (status: {request.Status}).");

        var playerTeamId = _getPlayerTeamId();
        if (request.RequestingTeamId != playerTeamId)
            return (false, "This interview is not for your team.");

        var coach = _getCoach(request.CoachId);
        if (coach == null)
            return (false, "Coach no longer exists.");

        var playerTeam = _getTeam(playerTeamId);
        if (playerTeam == null)
            return (false, "Team not found.");

        if (IsRoleFilled(playerTeam, request.TargetRole))
            return (false, $"The {request.TargetRole} position is already filled.");

        // Remove from old team
        if (coach.TeamId != null)
        {
            var oldTeam = _getTeam(coach.TeamId);
            if (oldTeam != null)
                RemoveCoachFromRole(oldTeam, coach);
        }

        // Hire to new team
        HireCoachInternal(playerTeam, coach, request.TargetRole);
        _coachingMarket.Remove(coach);

        // Update request status
        request.Status = InterviewStatus.Hired;
        ExpireOtherRequestsForCoach(coach.Id, request.Id);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.CoachHired, coach.Id, playerTeamId, (int)request.TargetRole);
        return (true, $"Hired {coach.FullName} as {request.TargetRole}.");
    }

    /// <summary>
    /// Player declares a coach as "HC candidate" to enable blocking interview requests.
    /// </summary>
    public (bool Success, string Message) DeclarePromotionIntent(string coachId)
    {
        var playerTeamId = _getPlayerTeamId();
        var coach = _getCoach(coachId);
        if (coach == null)
            return (false, "Coach not found.");
        if (coach.TeamId != playerTeamId)
            return (false, "Coach is not on your team.");
        if (coach.Role == CoachRole.HeadCoach)
            return (false, "Coach is already the head coach.");

        var team = _getTeam(playerTeamId);
        if (team?.HeadCoachId != null)
            return (false, "Your HC position is already filled — protection not needed.");

        _promotionIntentCoachIds.Add(coachId);
        GD.Print($"Promotion intent declared for {coach.FullName}");
        return (true, $"Declared promotion intent for {coach.FullName}. Interview requests for this coach can now be blocked.");
    }

    /// <summary>
    /// Remove promotion protection from a coach.
    /// </summary>
    public (bool Success, string Message) RemovePromotionIntent(string coachId)
    {
        if (!_promotionIntentCoachIds.Remove(coachId))
            return (false, "Coach was not on the protection list.");

        var coach = _getCoach(coachId);
        return (true, $"Removed promotion intent for {coach?.FullName ?? coachId}.");
    }

    /// <summary>
    /// Returns employed coaches on non-active-playoff teams, excluding HCs.
    /// These are the coaches available for interview requests.
    /// </summary>
    public List<Coach> GetInterviewCandidates()
    {
        var activePlayoffTeamIds = _getActivePlayoffTeamIds();
        var playerTeamId = _getPlayerTeamId();

        return _getCoaches()
            .Where(c => c.TeamId != null
                && c.TeamId != playerTeamId
                && c.Role != CoachRole.HeadCoach
                && !activePlayoffTeamIds.Contains(c.TeamId))
            .ToList();
    }

    private List<Coach> GetInterviewCandidatesForTeam(string requestingTeamId)
    {
        var activePlayoffTeamIds = _getActivePlayoffTeamIds();

        return _getCoaches()
            .Where(c => c.TeamId != null
                && c.TeamId != requestingTeamId
                && c.Role != CoachRole.HeadCoach
                && !activePlayoffTeamIds.Contains(c.TeamId))
            .ToList();
    }

    /// <summary>
    /// Returns interview requests targeting the player's coaches.
    /// </summary>
    public List<InterviewRequest> GetIncomingInterviewRequests()
    {
        var playerTeamId = _getPlayerTeamId();
        return _interviewRequests
            .Where(r => r.CurrentTeamId == playerTeamId
                && (r.Status == InterviewStatus.Pending || r.Status == InterviewStatus.Approved))
            .ToList();
    }

    /// <summary>
    /// Returns interview requests made by the player's team.
    /// </summary>
    public List<InterviewRequest> GetOutgoingInterviewRequests()
    {
        var playerTeamId = _getPlayerTeamId();
        return _interviewRequests
            .Where(r => r.RequestingTeamId == playerTeamId)
            .ToList();
    }

    // =====================================================
    // COACHING MARKET — Helper Methods
    // =====================================================

    private void CleanupInterviewsForCoach(string coachId)
    {
        foreach (var req in _interviewRequests.Where(r => r.CoachId == coachId
            && (r.Status == InterviewStatus.Pending || r.Status == InterviewStatus.Approved)))
        {
            req.Status = InterviewStatus.Expired;
            req.Notes = "Coach was fired/released";
        }
        _promotionIntentCoachIds.Remove(coachId);

        // Remove from AI promotion intents if present
        var keysToRemove = _aiPromotionIntents.Where(kv => kv.Value == coachId).Select(kv => kv.Key).ToList();
        foreach (var key in keysToRemove)
            _aiPromotionIntents.Remove(key);
    }

    private void ExpireOtherRequestsForCoach(string coachId, string? exceptRequestId)
    {
        foreach (var req in _interviewRequests.Where(r => r.CoachId == coachId
            && r.Id != exceptRequestId
            && (r.Status == InterviewStatus.Pending || r.Status == InterviewStatus.Approved)))
        {
            req.Status = InterviewStatus.Expired;
            req.Notes = "Coach was hired elsewhere";
        }
    }

    // =====================================================
    // LEGACY CAROUSEL (kept for backward compat, now called only by CloseMarket)
    // =====================================================

    private bool ShouldFireHC(Team team, Coach hc, Random rng)
    {
        int winThreshold = team.OwnerPatience / 10;
        int wins = team.CurrentRecord.Wins;

        if (wins < winThreshold)
        {
            float fireChance = 0.7f - (hc.Prestige / 200f);
            return rng.NextDouble() < fireChance;
        }

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
        CleanupInterviewsForCoach(coachId);
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

        if (IsRoleFilled(team, role))
            return (false, $"The {role} position is already filled. Fire the current coach first.");

        HireCoachInternal(team, coach, role);
        _coachingMarket.Remove(coach);
        ExpireOtherRequestsForCoach(coachId, null);
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

        RemoveCoachFromRole(team, coach);
        coach.Role = newRole;
        team.HeadCoachId = coach.Id;
        coach.GameManagement = Math.Min(99, coach.GameManagement + 3);

        // Remove from promotion intent if was protected
        _promotionIntentCoachIds.Remove(coachId);

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
                var candidate = _coachingMarket.FirstOrDefault(c => c.TeamId == null);
                if (candidate != null)
                {
                    HireCoachInternal(team, candidate, role);
                    _coachingMarket.Remove(candidate);
                }
                else
                {
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
            bool isHCCaliber = i < count / 3;
            int minRating = isHCCaliber ? 55 : 40;
            int ratingRange = isHCCaliber ? 35 : 45;

            var coach = new Coach
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstNames[rng.Next(firstNames.Length)],
                LastName = lastNames[rng.Next(lastNames.Length)],
                Age = 35 + rng.Next(25),
                Role = isHCCaliber ? CoachRole.HeadCoach : (CoachRole)(1 + rng.Next(10)),
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
                Prestige = isHCCaliber ? 30 + rng.Next(50) : 10 + rng.Next(40),
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

    public (List<string> MarketIds, List<InterviewRequest> Requests,
        List<string> PromotionIntents, Dictionary<string, string> AIIntents) GetState()
    {
        return (
            _coachingMarket.Select(c => c.Id).ToList(),
            _interviewRequests,
            _promotionIntentCoachIds.ToList(),
            new Dictionary<string, string>(_aiPromotionIntents)
        );
    }

    public void SetState(List<string> marketIds,
        List<InterviewRequest>? requests = null,
        List<string>? promotionIntents = null,
        Dictionary<string, string>? aiIntents = null)
    {
        _coachingMarket.Clear();
        var allCoaches = _getCoaches();
        foreach (var id in marketIds)
        {
            var coach = allCoaches.FirstOrDefault(c => c.Id == id);
            if (coach != null)
                _coachingMarket.Add(coach);
        }

        _interviewRequests = requests ?? new List<InterviewRequest>();
        _promotionIntentCoachIds = promotionIntents != null
            ? new HashSet<string>(promotionIntents) : new HashSet<string>();
        _aiPromotionIntents = aiIntents ?? new Dictionary<string, string>();
    }
}
