using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class TradeSystem
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<List<DraftPick>> _getAllPicks;
    private readonly Func<Dictionary<string, AIGMProfile>> _getAIProfiles;
    private readonly Func<Random> _getRng;
    private readonly Func<string, Player?> _getPlayer;
    private readonly Func<string, Team?> _getTeam;
    private readonly RosterManager _rosterManager;
    private readonly SalaryCapManager _capManager;
    private readonly Func<CalendarSystem> _getCalendar;
    private readonly Func<string> _getPlayerTeamId;
    private readonly Func<List<TransactionRecord>> _getTransactionLog;

    private List<TradeProposal> _pendingProposals = new();
    private List<TradeRecord> _tradeHistory = new();
    private List<string> _tradeBlockPlayerIds = new();
    private Dictionary<string, float> _tradeRelationships = new();

    public IReadOnlyList<TradeProposal> PendingProposals => _pendingProposals;
    public IReadOnlyList<TradeRecord> TradeHistory => _tradeHistory;
    public IReadOnlyList<string> TradeBlock => _tradeBlockPlayerIds;

    public TradeSystem(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<DraftPick>> getAllPicks,
        Func<Dictionary<string, AIGMProfile>> getAIProfiles,
        Func<Random> getRng,
        Func<string, Player?> getPlayer,
        Func<string, Team?> getTeam,
        RosterManager rosterManager,
        SalaryCapManager capManager,
        Func<CalendarSystem> getCalendar,
        Func<string> getPlayerTeamId,
        Func<List<TransactionRecord>> getTransactionLog)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getAllPicks = getAllPicks;
        _getAIProfiles = getAIProfiles;
        _getRng = getRng;
        _getPlayer = getPlayer;
        _getTeam = getTeam;
        _rosterManager = rosterManager;
        _capManager = capManager;
        _getCalendar = getCalendar;
        _getPlayerTeamId = getPlayerTeamId;
        _getTransactionLog = getTransactionLog;
    }

    // =========================================================================
    // Jimmy Johnson Draft Value Chart — anchor points with linear interpolation
    // =========================================================================

    private static readonly (int Pick, int Value)[] ValueAnchors =
    {
        (1, 3000), (5, 1700), (10, 1300), (15, 1050), (20, 850),
        (32, 590), (33, 580), (64, 270), (100, 120), (135, 55),
        (170, 27), (210, 10), (224, 1),
    };

    public static int GetPickValueByOverall(int overallPickNumber)
    {
        if (overallPickNumber <= 0) return 0;
        if (overallPickNumber >= 224) return 1;

        for (int i = 0; i < ValueAnchors.Length - 1; i++)
        {
            var (p1, v1) = ValueAnchors[i];
            var (p2, v2) = ValueAnchors[i + 1];

            if (overallPickNumber >= p1 && overallPickNumber <= p2)
            {
                if (p2 == p1) return v1;
                float t = (float)(overallPickNumber - p1) / (p2 - p1);
                return (int)(v1 + t * (v2 - v1));
            }
        }

        return 1;
    }

    public int GetPickTradeValue(DraftPick pick, int currentYear)
    {
        int overallEstimate = pick.OverallNumber ?? EstimatePickOverall(pick);
        int baseValue = GetPickValueByOverall(overallEstimate);

        // Future pick discount: 15% per year out
        int yearsOut = pick.Year - currentYear;
        if (yearsOut > 0)
        {
            float discount = MathF.Pow(0.85f, yearsOut);
            baseValue = (int)(baseValue * discount);
        }

        return Math.Max(1, baseValue);
    }

    private int EstimatePickOverall(DraftPick pick)
    {
        // For future picks without a known overall, estimate based on round
        // Assume mid-round slot
        return pick.Round switch
        {
            1 => 16,
            2 => 48,
            3 => 80,
            4 => 112,
            5 => 152,
            6 => 185,
            7 => 217,
            _ => 200,
        };
    }

    // =========================================================================
    // Player Trade Value Calculation
    // =========================================================================

    public int CalculatePlayerTradeValue(Player player, int currentYear)
    {
        int baseValue = player.Overall switch
        {
            >= 95 => 2000,
            >= 90 => 1500,
            >= 85 => 1000,
            >= 80 => 700,
            >= 75 => 400,
            >= 70 => 200,
            >= 65 => 80,
            _ => 20,
        };

        // Age modifier: peaks at 24-27, declines after
        float ageMod = player.Age switch
        {
            <= 23 => 1.1f,
            <= 27 => 1.0f,
            28 => 0.9f,
            29 => 0.8f,
            30 => 0.65f,
            31 => 0.5f,
            32 => 0.35f,
            _ => 0.2f,
        };

        // Contract favorability
        float contractMod = 1.0f;
        if (player.CurrentContract != null)
        {
            int yearsLeft = player.CurrentContract.Years
                .Count(y => y.Year >= currentYear && !y.IsVoidYear);
            long apy = player.CurrentContract.AveragePerYear;
            long marketValue = ContractGenerator.GetMarketValue(player);
            float ratio = marketValue > 0 ? (float)apy / marketValue : 1.0f;

            if (ratio < 0.8f) contractMod = 1.25f;
            else if (ratio < 0.95f) contractMod = 1.1f;
            else if (ratio > 1.2f) contractMod = 0.75f;

            // Expiring (rental) discount
            if (yearsLeft <= 1) contractMod *= 0.6f;
        }
        else
        {
            contractMod = 0.5f;
        }

        // Position multiplier
        float posMod = player.Position switch
        {
            Position.QB => 1.5f,
            Position.EDGE => 1.15f,
            Position.CB => 1.1f,
            Position.WR or Position.LT => 1.05f,
            Position.DT or Position.RT => 1.0f,
            Position.FS or Position.SS or Position.MLB => 0.95f,
            Position.HB or Position.TE or Position.LG or Position.RG or Position.C => 0.9f,
            Position.OLB => 0.9f,
            Position.K or Position.P => 0.5f,
            _ => 0.8f,
        };

        // Development trait bonus
        float devMod = player.DevTrait switch
        {
            DevelopmentTrait.XFactor => 1.3f,
            DevelopmentTrait.Superstar => 1.15f,
            DevelopmentTrait.Star => 1.05f,
            _ => 1.0f,
        };

        return Math.Max(1, (int)(baseValue * ageMod * contractMod * posMod * devMod));
    }

    // =========================================================================
    // Trade Window / Deadline
    // =========================================================================

    public bool IsTradeWindowOpen()
    {
        var calendar = _getCalendar();
        return calendar.CurrentPhase switch
        {
            GamePhase.Draft => true,
            GamePhase.PostDraft => true,
            GamePhase.Preseason => true,
            GamePhase.RegularSeason => calendar.CurrentWeek <= 8,
            _ => false,
        };
    }

    public bool IsNearDeadline()
    {
        var calendar = _getCalendar();
        return calendar.CurrentPhase == GamePhase.RegularSeason
            && calendar.CurrentWeek >= 6 && calendar.CurrentWeek <= 8;
    }

    // =========================================================================
    // Trade Validation
    // =========================================================================

    public (bool Valid, string Message) ValidateTrade(TradeProposal proposal)
    {
        if (!IsTradeWindowOpen())
            return (false, "The trade window is closed.");

        var proposingTeam = _getTeam(proposal.ProposingTeamId);
        var receivingTeam = _getTeam(proposal.ReceivingTeamId);
        if (proposingTeam == null || receivingTeam == null)
            return (false, "Invalid team.");

        // Must have at least one asset on each side
        if (proposal.ProposingPlayerIds.Count == 0 && proposal.ProposingPickIds.Count == 0)
            return (false, "Proposing team must offer at least one asset.");
        if (proposal.ReceivingPlayerIds.Count == 0 && proposal.ReceivingPickIds.Count == 0)
            return (false, "Receiving team must offer at least one asset.");

        // Validate all players belong to correct teams
        foreach (var pid in proposal.ProposingPlayerIds)
        {
            var p = _getPlayer(pid);
            if (p == null) return (false, $"Player {pid} not found.");
            if (p.TeamId != proposal.ProposingTeamId) return (false, $"{p.FullName} is not on the proposing team.");
            if (p.CurrentContract?.HasNoTradeClause == true) return (false, $"{p.FullName} has a no-trade clause.");
        }
        foreach (var pid in proposal.ReceivingPlayerIds)
        {
            var p = _getPlayer(pid);
            if (p == null) return (false, $"Player {pid} not found.");
            if (p.TeamId != proposal.ReceivingTeamId) return (false, $"{p.FullName} is not on the receiving team.");
            if (p.CurrentContract?.HasNoTradeClause == true) return (false, $"{p.FullName} has a no-trade clause.");
        }

        // Validate picks belong to correct teams and aren't used
        var allPicks = _getAllPicks();
        foreach (var pickId in proposal.ProposingPickIds)
        {
            var pick = allPicks.FirstOrDefault(p => p.Id == pickId);
            if (pick == null) return (false, "Draft pick not found.");
            if (pick.CurrentTeamId != proposal.ProposingTeamId) return (false, "Draft pick doesn't belong to proposing team.");
            if (pick.IsUsed) return (false, "Draft pick has already been used.");
        }
        foreach (var pickId in proposal.ReceivingPickIds)
        {
            var pick = allPicks.FirstOrDefault(p => p.Id == pickId);
            if (pick == null) return (false, "Draft pick not found.");
            if (pick.CurrentTeamId != proposal.ReceivingTeamId) return (false, "Draft pick doesn't belong to receiving team.");
            if (pick.IsUsed) return (false, "Draft pick has already been used.");
        }

        // Check roster size limits (53 active)
        int proposingNetPlayers = proposal.ReceivingPlayerIds.Count - proposal.ProposingPlayerIds.Count;
        int receivingNetPlayers = proposal.ProposingPlayerIds.Count - proposal.ReceivingPlayerIds.Count;

        int proposingActive = proposingTeam.PlayerIds.Count + proposingNetPlayers;
        int receivingActive = receivingTeam.PlayerIds.Count + receivingNetPlayers;

        if (proposingActive > 53)
            return (false, $"{proposingTeam.FullName} would exceed 53-man roster limit.");
        if (receivingActive > 53)
            return (false, $"{receivingTeam.FullName} would exceed 53-man roster limit.");

        return (true, "Trade is valid.");
    }

    // =========================================================================
    // Trade Execution
    // =========================================================================

    public (bool Success, string Message) ExecuteTrade(TradeProposal proposal)
    {
        var (valid, msg) = ValidateTrade(proposal);
        if (!valid) return (false, msg);

        var proposingTeam = _getTeam(proposal.ProposingTeamId)!;
        var receivingTeam = _getTeam(proposal.ReceivingTeamId)!;
        var calendar = _getCalendar();
        var allPicks = _getAllPicks();
        int currentYear = calendar.CurrentYear;

        // Move players: proposing → receiving
        foreach (var pid in proposal.ProposingPlayerIds)
            MovePlayer(pid, proposingTeam, receivingTeam);

        // Move players: receiving → proposing
        foreach (var pid in proposal.ReceivingPlayerIds)
            MovePlayer(pid, receivingTeam, proposingTeam);

        // Move picks: proposing → receiving
        foreach (var pickId in proposal.ProposingPickIds)
            MovePick(pickId, proposingTeam, receivingTeam, allPicks);

        // Move picks: receiving → proposing
        foreach (var pickId in proposal.ReceivingPickIds)
            MovePick(pickId, receivingTeam, proposingTeam, allPicks);

        // Recalculate cap for both teams
        _capManager.RecalculateTeamCap(proposingTeam, _getPlayers(), currentYear);
        _capManager.RecalculateTeamCap(receivingTeam, _getPlayers(), currentYear);

        // Log transactions
        foreach (var pid in proposal.ProposingPlayerIds)
        {
            var p = _getPlayer(pid);
            LogTransaction(TransactionType.Traded, pid, proposal.ReceivingTeamId,
                proposal.ProposingTeamId, $"{p?.FullName} traded to {receivingTeam.FullName}");
        }
        foreach (var pid in proposal.ReceivingPlayerIds)
        {
            var p = _getPlayer(pid);
            LogTransaction(TransactionType.Traded, pid, proposal.ProposingTeamId,
                proposal.ReceivingTeamId, $"{p?.FullName} traded to {proposingTeam.FullName}");
        }

        // Create trade record with snapshot names
        var record = new TradeRecord
        {
            Team1Id = proposal.ProposingTeamId,
            Team2Id = proposal.ReceivingTeamId,
            Team1SentPlayerIds = new List<string>(proposal.ProposingPlayerIds),
            Team1SentPickIds = new List<string>(proposal.ProposingPickIds),
            Team2SentPlayerIds = new List<string>(proposal.ReceivingPlayerIds),
            Team2SentPickIds = new List<string>(proposal.ReceivingPickIds),
            Team1SentPlayerNames = proposal.ProposingPlayerIds
                .Select(id => _getPlayer(id)?.FullName ?? "Unknown").ToList(),
            Team2SentPlayerNames = proposal.ReceivingPlayerIds
                .Select(id => _getPlayer(id)?.FullName ?? "Unknown").ToList(),
            Team1SentPickDescriptions = proposal.ProposingPickIds
                .Select(id => DescribePick(id, allPicks)).ToList(),
            Team2SentPickDescriptions = proposal.ReceivingPickIds
                .Select(id => DescribePick(id, allPicks)).ToList(),
            Team1ValueGiven = proposal.ProposingValuePoints,
            Team2ValueGiven = proposal.ReceivingValuePoints,
            Year = calendar.CurrentYear,
            Week = calendar.CurrentWeek,
            Phase = calendar.CurrentPhase,
        };
        _tradeHistory.Add(record);

        // Update relationship
        UpdateRelationship(proposal.ProposingTeamId, proposal.ReceivingTeamId, 0.1f);

        // Remove from pending if present
        _pendingProposals.RemoveAll(p => p.Id == proposal.Id);
        proposal.Status = TradeStatus.Accepted;

        // Emit signals
        EventBus.Instance?.EmitSignal(EventBus.SignalName.TradeAccepted, proposal.Id);
        foreach (var pid in proposal.ProposingPlayerIds)
            EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerTraded,
                pid, proposal.ProposingTeamId, proposal.ReceivingTeamId);
        foreach (var pid in proposal.ReceivingPlayerIds)
            EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerTraded,
                pid, proposal.ReceivingTeamId, proposal.ProposingTeamId);

        return (true, "Trade completed successfully.");
    }

    private void MovePlayer(string playerId, Team fromTeam, Team toTeam)
    {
        var player = _getPlayer(playerId);
        if (player == null) return;

        // Remove from source team roster
        fromTeam.PlayerIds.Remove(playerId);
        fromTeam.PracticeSquadIds.Remove(playerId);
        fromTeam.IRPlayerIds.Remove(playerId);

        // Remove from source depth chart
        foreach (var kvp in fromTeam.DepthChart.Chart)
            kvp.Value.Remove(playerId);

        // Add to destination team (active roster)
        if (!toTeam.PlayerIds.Contains(playerId))
            toTeam.PlayerIds.Add(playerId);

        // Add to end of depth chart at position
        if (toTeam.DepthChart.Chart.TryGetValue(player.Position, out var list))
            list.Add(playerId);
        else
            toTeam.DepthChart.Chart[player.Position] = new List<string> { playerId };

        // Update player fields
        player.TeamId = toTeam.Id;
        player.RosterStatus = RosterStatus.Active53;
        if (player.CurrentContract != null)
            player.CurrentContract.TeamId = toTeam.Id;
    }

    private void MovePick(string pickId, Team fromTeam, Team toTeam, List<DraftPick> allPicks)
    {
        var pick = allPicks.FirstOrDefault(p => p.Id == pickId);
        if (pick == null) return;

        fromTeam.DraftPicks.RemoveAll(p => p.Id == pickId);
        pick.CurrentTeamId = toTeam.Id;
        if (!toTeam.DraftPicks.Any(p => p.Id == pickId))
            toTeam.DraftPicks.Add(pick);
    }

    private string DescribePick(string pickId, List<DraftPick> allPicks)
    {
        var pick = allPicks.FirstOrDefault(p => p.Id == pickId);
        if (pick == null) return "Unknown Pick";
        string overall = pick.OverallNumber.HasValue ? $" #{pick.OverallNumber}" : "";
        return $"{pick.Year} Round {pick.Round}{overall}";
    }

    // =========================================================================
    // AI Trade Evaluation
    // =========================================================================

    public (bool Accept, string Reason) EvaluateTradeForAI(TradeProposal proposal, string evaluatingTeamId)
    {
        var profiles = _getAIProfiles();
        if (!profiles.TryGetValue(evaluatingTeamId, out var profile))
            return (false, "No AI profile for this team.");

        var calendar = _getCalendar();
        int currentYear = calendar.CurrentYear;

        // Calculate what the evaluating team gives vs receives
        bool isReceiver = evaluatingTeamId == proposal.ReceivingTeamId;
        var myGivenPlayerIds = isReceiver ? proposal.ReceivingPlayerIds : proposal.ProposingPlayerIds;
        var myGivenPickIds = isReceiver ? proposal.ReceivingPickIds : proposal.ProposingPickIds;
        var myReceivedPlayerIds = isReceiver ? proposal.ProposingPlayerIds : proposal.ReceivingPlayerIds;
        var myReceivedPickIds = isReceiver ? proposal.ProposingPickIds : proposal.ReceivingPickIds;

        int givenValue = CalculateAssetValue(myGivenPlayerIds, myGivenPickIds, currentYear);
        int receivedValue = CalculateAssetValue(myReceivedPlayerIds, myReceivedPickIds, currentYear);

        // Apply competitive window adjustments
        float windowPlayerMod = 1.0f;
        float windowPickMod = 1.0f;
        switch (profile.Strategy)
        {
            case AIStrategy.WinNow:
                windowPlayerMod = 1.2f;
                windowPickMod = 0.85f;
                break;
            case AIStrategy.Contend:
                windowPlayerMod = 1.1f;
                windowPickMod = 0.9f;
                break;
            case AIStrategy.Rebuild:
            case AIStrategy.TankMode:
                windowPlayerMod = 0.8f;
                windowPickMod = 1.2f;
                break;
        }

        // Recalculate with window modifiers
        int adjustedGiven = CalculateWindowAdjustedValue(myGivenPlayerIds, myGivenPickIds, currentYear, windowPlayerMod, windowPickMod);
        int adjustedReceived = CalculateWindowAdjustedValue(myReceivedPlayerIds, myReceivedPickIds, currentYear, windowPlayerMod, windowPickMod);

        float valueDiff = adjustedReceived - adjustedGiven;
        float diffPct = adjustedGiven > 0 ? valueDiff / adjustedGiven : 0;

        // Threshold adjusted by RiskTolerance (0.0-1.0 scale)
        float threshold = -0.10f - (profile.RiskTolerance - 0.5f) * 0.1f;

        // Penalty for trading away starters at need positions
        var team = _getTeam(evaluatingTeamId);
        if (team != null)
        {
            foreach (var pid in myGivenPlayerIds)
            {
                var p = _getPlayer(pid);
                if (p != null && team.TeamNeeds.Contains(p.Position) && p.Overall >= 75)
                    valueDiff -= 200;
            }
        }

        // Relationship modifier
        string otherTeamId = isReceiver ? proposal.ProposingTeamId : proposal.ReceivingTeamId;
        float relMod = GetRelationship(evaluatingTeamId, otherTeamId);
        valueDiff += relMod * 50; // ±50 points per ±1.0 relationship

        // Near-deadline aggression
        if (IsNearDeadline())
        {
            if (profile.Strategy is AIStrategy.WinNow or AIStrategy.Contend)
                valueDiff += 50; // More willing to buy
            else if (profile.Strategy is AIStrategy.Rebuild or AIStrategy.TankMode)
                valueDiff += 30; // More willing to sell
        }

        // Final check with threshold
        float finalDiffPct = adjustedGiven > 0 ? valueDiff / adjustedGiven : diffPct;
        if (finalDiffPct < threshold)
            return (false, "Insufficient value offered.");

        // Add some randomness — even "fair" trades can be rejected
        var rng = _getRng();
        if (finalDiffPct < 0 && rng.NextDouble() > 0.6)
            return (false, "Team decided to pass on the trade.");

        return (true, "Trade accepted.");
    }

    private int CalculateAssetValue(List<string> playerIds, List<string> pickIds, int currentYear)
    {
        int total = 0;
        var allPicks = _getAllPicks();

        foreach (var pid in playerIds)
        {
            var p = _getPlayer(pid);
            if (p != null) total += CalculatePlayerTradeValue(p, currentYear);
        }
        foreach (var pickId in pickIds)
        {
            var pick = allPicks.FirstOrDefault(p => p.Id == pickId);
            if (pick != null) total += GetPickTradeValue(pick, currentYear);
        }

        return total;
    }

    private int CalculateWindowAdjustedValue(List<string> playerIds, List<string> pickIds,
        int currentYear, float playerMod, float pickMod)
    {
        int total = 0;
        var allPicks = _getAllPicks();

        foreach (var pid in playerIds)
        {
            var p = _getPlayer(pid);
            if (p != null) total += (int)(CalculatePlayerTradeValue(p, currentYear) * playerMod);
        }
        foreach (var pickId in pickIds)
        {
            var pick = allPicks.FirstOrDefault(p => p.Id == pickId);
            if (pick != null) total += (int)(GetPickTradeValue(pick, currentYear) * pickMod);
        }

        return total;
    }

    // =========================================================================
    // AI-Initiated Trade Proposals
    // =========================================================================

    public void GenerateAITradeProposals()
    {
        if (!IsTradeWindowOpen()) return;

        var rng = _getRng();
        var profiles = _getAIProfiles();
        string playerTeamId = _getPlayerTeamId();
        var calendar = _getCalendar();
        int currentYear = calendar.CurrentYear;
        float deadlineMod = IsNearDeadline() ? 2.0f : 1.0f;

        foreach (var (teamId, profile) in profiles)
        {
            // Roll against trade frequency
            if (rng.NextDouble() > profile.TradeFrequency * 0.3 * deadlineMod)
                continue;

            var proposal = BuildAIProposal(teamId, profile, currentYear, rng);
            if (proposal == null) continue;

            if (proposal.ReceivingTeamId == playerTeamId)
            {
                // Proposal to player → queue for UI
                _pendingProposals.Add(proposal);
                EventBus.Instance?.EmitSignal(EventBus.SignalName.TradeProposed,
                    teamId, playerTeamId);
            }
            else
            {
                // AI-to-AI → evaluate and execute immediately
                var (accept, _) = EvaluateTradeForAI(proposal, proposal.ReceivingTeamId);
                if (accept)
                    ExecuteTrade(proposal);
            }
        }

        // Expire old proposals (from more than 2 weeks ago)
        _pendingProposals.RemoveAll(p =>
            p.Status == TradeStatus.Pending &&
            (calendar.CurrentWeek - p.ProposedWeek > 2 ||
             calendar.CurrentPhase != p.ProposedPhase));
    }

    private TradeProposal? BuildAIProposal(string aiTeamId, AIGMProfile profile,
        int currentYear, Random rng)
    {
        var aiTeam = _getTeam(aiTeamId);
        if (aiTeam == null) return null;

        var players = _getPlayers();
        string playerTeamId = _getPlayerTeamId();
        var teams = _getTeams();

        // Determine what the AI wants to do based on strategy
        bool wantsToBuy = profile.Strategy is AIStrategy.WinNow or AIStrategy.Contend;

        if (wantsToBuy)
        {
            // Look for a player on another team at a need position
            if (aiTeam.TeamNeeds.Count == 0) return null;
            var needPos = aiTeam.TeamNeeds[rng.Next(aiTeam.TeamNeeds.Count)];

            // Find a target player on another team (prefer trade block players)
            Player? target = null;
            string? targetTeamId = null;

            var candidates = players
                .Where(p => p.Position == needPos && p.TeamId != null && p.TeamId != aiTeamId
                    && p.Overall >= 70 && p.CurrentContract?.HasNoTradeClause != true)
                .OrderByDescending(p => _tradeBlockPlayerIds.Contains(p.Id) ? 1 : 0)
                .ThenByDescending(p => p.Overall)
                .Take(10)
                .ToList();

            if (candidates.Count == 0) return null;
            target = candidates[rng.Next(Math.Min(3, candidates.Count))];
            targetTeamId = target.TeamId!;

            // Offer a draft pick of appropriate value
            int targetValue = CalculatePlayerTradeValue(target, currentYear);
            var bestPick = aiTeam.DraftPicks
                .Where(p => !p.IsUsed && !p.IsCompensatory)
                .OrderByDescending(p => GetPickTradeValue(p, currentYear))
                .FirstOrDefault(p => GetPickTradeValue(p, currentYear) >= targetValue * 0.7);

            if (bestPick == null) return null;

            return CreateProposal(aiTeamId, targetTeamId,
                new List<string>(), new List<string> { bestPick.Id },
                new List<string> { target.Id }, new List<string>(),
                currentYear, true);
        }
        else
        {
            // Rebuild: look to sell a veteran for picks
            var veterans = players
                .Where(p => p.TeamId == aiTeamId && p.Age >= 28 && p.Overall >= 72
                    && p.CurrentContract?.HasNoTradeClause != true)
                .OrderByDescending(p => p.Overall)
                .Take(5)
                .ToList();

            if (veterans.Count == 0) return null;
            var veteran = veterans[rng.Next(Math.Min(3, veterans.Count))];

            int veteranValue = CalculatePlayerTradeValue(veteran, currentYear);

            // Find a team willing to trade a pick
            var potentialBuyers = teams
                .Where(t => t.Id != aiTeamId && t.DraftPicks.Any(p => !p.IsUsed && !p.IsCompensatory))
                .ToList();

            if (potentialBuyers.Count == 0) return null;

            // Prefer teams that need this position, or the player's team
            var buyerCandidates = potentialBuyers
                .Where(t => t.TeamNeeds.Contains(veteran.Position) || t.Id == playerTeamId)
                .ToList();

            var buyer = buyerCandidates.Count > 0
                ? buyerCandidates[rng.Next(buyerCandidates.Count)]
                : potentialBuyers[rng.Next(potentialBuyers.Count)];

            var buyerPick = buyer.DraftPicks
                .Where(p => !p.IsUsed && !p.IsCompensatory)
                .OrderByDescending(p => GetPickTradeValue(p, currentYear))
                .FirstOrDefault(p => GetPickTradeValue(p, currentYear) >= veteranValue * 0.5);

            if (buyerPick == null) return null;

            return CreateProposal(aiTeamId, buyer.Id,
                new List<string> { veteran.Id }, new List<string>(),
                new List<string>(), new List<string> { buyerPick.Id },
                currentYear, true);
        }
    }

    // =========================================================================
    // Player-Facing Methods
    // =========================================================================

    public TradeProposal CreatePlayerProposal(
        string targetTeamId,
        List<string> offeredPlayerIds,
        List<string> offeredPickIds,
        List<string> requestedPlayerIds,
        List<string> requestedPickIds)
    {
        string playerTeamId = _getPlayerTeamId();
        return CreateProposal(playerTeamId, targetTeamId,
            offeredPlayerIds, offeredPickIds,
            requestedPlayerIds, requestedPickIds,
            _getCalendar().CurrentYear, false);
    }

    public (bool Success, string Message) SubmitPlayerProposal(TradeProposal proposal)
    {
        var (valid, validMsg) = ValidateTrade(proposal);
        if (!valid) return (false, validMsg);

        var (accept, reason) = EvaluateTradeForAI(proposal, proposal.ReceivingTeamId);
        if (accept)
        {
            proposal.Status = TradeStatus.Accepted;
            return ExecuteTrade(proposal);
        }
        else
        {
            proposal.Status = TradeStatus.Rejected;
            proposal.RejectionReason = reason;
            UpdateRelationship(proposal.ProposingTeamId, proposal.ReceivingTeamId, -0.05f);
            EventBus.Instance?.EmitSignal(EventBus.SignalName.TradeRejected, proposal.Id);
            return (false, reason);
        }
    }

    public (bool Success, string Message) AcceptAIProposal(string proposalId)
    {
        var proposal = _pendingProposals.FirstOrDefault(p => p.Id == proposalId);
        if (proposal == null) return (false, "Proposal not found.");
        if (proposal.Status != TradeStatus.Pending) return (false, "Proposal is no longer pending.");

        return ExecuteTrade(proposal);
    }

    public void RejectAIProposal(string proposalId)
    {
        var proposal = _pendingProposals.FirstOrDefault(p => p.Id == proposalId);
        if (proposal == null) return;

        proposal.Status = TradeStatus.Rejected;
        UpdateRelationship(proposal.ProposingTeamId, proposal.ReceivingTeamId, -0.05f);
        _pendingProposals.Remove(proposal);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.TradeRejected, proposal.Id);
    }

    // =========================================================================
    // Trade Block
    // =========================================================================

    public void AddToTradeBlock(string playerId)
    {
        if (!_tradeBlockPlayerIds.Contains(playerId))
            _tradeBlockPlayerIds.Add(playerId);
    }

    public void RemoveFromTradeBlock(string playerId)
    {
        _tradeBlockPlayerIds.Remove(playerId);
    }

    public bool IsOnTradeBlock(string playerId) => _tradeBlockPlayerIds.Contains(playerId);

    // =========================================================================
    // Relationship Tracking
    // =========================================================================

    private string GetRelationshipKey(string team1Id, string team2Id)
    {
        return string.Compare(team1Id, team2Id, StringComparison.Ordinal) < 0
            ? $"{team1Id}_{team2Id}"
            : $"{team2Id}_{team1Id}";
    }

    public float GetRelationship(string team1Id, string team2Id)
    {
        string key = GetRelationshipKey(team1Id, team2Id);
        return _tradeRelationships.GetValueOrDefault(key, 0f);
    }

    private void UpdateRelationship(string team1Id, string team2Id, float delta)
    {
        string key = GetRelationshipKey(team1Id, team2Id);
        float current = _tradeRelationships.GetValueOrDefault(key, 0f);
        _tradeRelationships[key] = Math.Clamp(current + delta, -1.0f, 1.0f);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private TradeProposal CreateProposal(
        string proposingTeamId, string receivingTeamId,
        List<string> propPlayerIds, List<string> propPickIds,
        List<string> recvPlayerIds, List<string> recvPickIds,
        int currentYear, bool isAI)
    {
        var calendar = _getCalendar();
        int propValue = CalculateAssetValue(propPlayerIds, propPickIds, currentYear);
        int recvValue = CalculateAssetValue(recvPlayerIds, recvPickIds, currentYear);

        return new TradeProposal
        {
            ProposingTeamId = proposingTeamId,
            ReceivingTeamId = receivingTeamId,
            ProposingPlayerIds = new List<string>(propPlayerIds),
            ProposingPickIds = new List<string>(propPickIds),
            ReceivingPlayerIds = new List<string>(recvPlayerIds),
            ReceivingPickIds = new List<string>(recvPickIds),
            ProposingValuePoints = propValue,
            ReceivingValuePoints = recvValue,
            ProposedYear = calendar.CurrentYear,
            ProposedWeek = calendar.CurrentWeek,
            ProposedPhase = calendar.CurrentPhase,
            IsAIInitiated = isAI,
        };
    }

    private void LogTransaction(TransactionType type, string playerId, string teamId,
        string otherTeamId, string description)
    {
        var calendar = _getCalendar();
        _getTransactionLog().Add(new TransactionRecord
        {
            Type = type,
            PlayerId = playerId,
            TeamId = teamId,
            OtherTeamId = otherTeamId,
            Description = description,
            Year = calendar.CurrentYear,
            Week = calendar.CurrentWeek,
            Phase = calendar.CurrentPhase,
        });
    }

    // =========================================================================
    // Save/Load State
    // =========================================================================

    public (List<TradeRecord> History, List<string> Block,
        List<TradeProposal> Pending, Dictionary<string, float> Relationships) GetState()
    {
        return (_tradeHistory, _tradeBlockPlayerIds, _pendingProposals, _tradeRelationships);
    }

    public void SetState(List<TradeRecord> history, List<string> block,
        List<TradeProposal> pending, Dictionary<string, float> relationships)
    {
        _tradeHistory = history;
        _tradeBlockPlayerIds = block;
        _pendingProposals = pending;
        _tradeRelationships = relationships;
    }
}
