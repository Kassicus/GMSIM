using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

/// <summary>
/// Manages the free agency period: market generation, AI signings, player offers.
/// Plain C# class owned by GameManager (lambda DI).
/// </summary>
public class FreeAgencySystem
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<List<Coach>> _getCoaches;
    private readonly Func<Dictionary<string, AIGMProfile>> _getAIProfiles;
    private readonly Func<Random> _getRng;
    private readonly Func<string, Player?> _getPlayer;
    private readonly Func<string, Team?> _getTeam;
    private readonly RosterManager _rosterManager;
    private readonly SalaryCapManager _capManager;
    private readonly Func<CalendarSystem> _getCalendar;
    private readonly Func<string> _getPlayerTeamId;

    private List<string> _freeAgentPool = new();
    private List<FreeAgentOffer> _allOffers = new();
    private List<FreeAgentOffer> _playerPendingOffers = new();
    private Dictionary<string, List<string>> _playerInterest = new(); // playerId → interested teamIds

    public IReadOnlyList<string> FreeAgentPool => _freeAgentPool;
    public IReadOnlyList<FreeAgentOffer> AllOffers => _allOffers;
    public IReadOnlyList<FreeAgentOffer> PlayerPendingOffers => _playerPendingOffers;

    public FreeAgencySystem(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<Coach>> getCoaches,
        Func<Dictionary<string, AIGMProfile>> getAIProfiles,
        Func<Random> getRng,
        Func<string, Player?> getPlayer,
        Func<string, Team?> getTeam,
        RosterManager rosterManager,
        SalaryCapManager capManager,
        Func<CalendarSystem> getCalendar,
        Func<string> getPlayerTeamId)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getCoaches = getCoaches;
        _getAIProfiles = getAIProfiles;
        _getRng = getRng;
        _getPlayer = getPlayer;
        _getTeam = getTeam;
        _rosterManager = rosterManager;
        _capManager = capManager;
        _getCalendar = getCalendar;
        _getPlayerTeamId = getPlayerTeamId;
    }

    /// <summary>
    /// Called when FreeAgency phase begins. Identifies all FAs, generates interest.
    /// </summary>
    public void InitializeFreeAgency(int year)
    {
        _freeAgentPool.Clear();
        _allOffers.Clear();
        _playerPendingOffers.Clear();
        _playerInterest.Clear();

        var players = _getPlayers();
        var rng = _getRng();

        // Identify players whose contracts have expired
        foreach (var player in players)
        {
            if (player.RosterStatus == RosterStatus.Retired) continue;

            bool contractExpired = player.CurrentContract == null
                || !player.CurrentContract.Years.Any(y => y.Year >= year);

            // Already tagged players don't become FAs
            bool isTagged = false;
            if (player.TeamId != null)
            {
                var team = _getTeam(player.TeamId);
                if (team != null && (team.TaggedPlayerId == player.Id || team.TransitionTagPlayerId == player.Id))
                    isTagged = true;
            }

            if (contractExpired && !isTagged)
            {
                // Remove from team if still on one
                if (player.TeamId != null)
                {
                    var team = _getTeam(player.TeamId);
                    if (team != null)
                    {
                        team.PlayerIds.Remove(player.Id);
                        team.PracticeSquadIds.Remove(player.Id);
                        team.IRPlayerIds.Remove(player.Id);
                        RemoveFromDepthChart(team, player.Id);
                    }
                }

                player.TeamId = null;
                player.RosterStatus = RosterStatus.FreeAgent;
                player.CurrentContract = null;
                _freeAgentPool.Add(player.Id);
            }
        }

        // Sort pool by OVR descending
        _freeAgentPool.Sort((a, b) =>
        {
            var pa = _getPlayer(a);
            var pb = _getPlayer(b);
            return (pb?.Overall ?? 0).CompareTo(pa?.Overall ?? 0);
        });

        // Generate interest lists
        GenerateInterestLists(rng);
    }

    /// <summary>
    /// Process one week of free agency. Called by GameManager each FA week advance.
    /// </summary>
    public void ProcessFreeAgencyWeek(int weekNum)
    {
        var rng = _getRng();

        switch (weekNum)
        {
            case 1:
                // Legal tampering: generate AI offers for Elite tier, no signings yet
                GenerateAIOffersForTier(FASigningTier.Elite, weekNum, rng);
                break;
            case 2:
                // Elite signings + Starter offers
                ProcessPendingSignings(rng);
                GenerateAIOffersForTier(FASigningTier.Starter, weekNum, rng);
                break;
            case 3:
                // Starter signings + Depth offers
                ProcessPendingSignings(rng);
                GenerateAIOffersForTier(FASigningTier.Depth, weekNum, rng);
                break;
            case 4:
                // Process all remaining + bargain bin minimum signings
                ProcessPendingSignings(rng);
                ProcessBargainBin(rng);
                break;
        }

        // Process human player's pending offers
        ProcessPlayerOffers(rng);
    }

    /// <summary>
    /// Human player submits an offer to a free agent.
    /// </summary>
    public void MakePlayerOffer(FreeAgentOffer offer)
    {
        offer.IsPlayerOffer = true;
        offer.Status = FreeAgentOfferStatus.Pending;
        _playerPendingOffers.Add(offer);
        _allOffers.Add(offer);
    }

    /// <summary>
    /// Withdraw a pending player offer.
    /// </summary>
    public void WithdrawPlayerOffer(string offerId)
    {
        var offer = _playerPendingOffers.FirstOrDefault(o => o.Id == offerId);
        if (offer != null)
        {
            offer.Status = FreeAgentOfferStatus.Withdrawn;
            _playerPendingOffers.Remove(offer);
        }
    }

    /// <summary>
    /// Get filtered list of available free agents for UI.
    /// </summary>
    public List<Player> GetFreeAgents(Position? filterPos = null, FASigningTier? filterTier = null)
    {
        var result = new List<Player>();
        foreach (var id in _freeAgentPool)
        {
            var player = _getPlayer(id);
            if (player == null) continue;
            if (filterPos.HasValue && player.Position != filterPos.Value) continue;
            if (filterTier.HasValue && GetPlayerTier(player) != filterTier.Value) continue;
            result.Add(player);
        }
        return result;
    }

    /// <summary>
    /// Get all pending offers for a specific team.
    /// </summary>
    public List<FreeAgentOffer> GetTeamOffers(string teamId)
    {
        return _allOffers
            .Where(o => o.TeamId == teamId && o.Status == FreeAgentOfferStatus.Pending)
            .ToList();
    }

    /// <summary>
    /// Get the estimated market value for a free agent.
    /// </summary>
    public long GetEstimatedMarketValue(Player player)
    {
        return ContractGenerator.GetMarketValue(player);
    }

    // --- Internal ---

    private void GenerateInterestLists(Random rng)
    {
        var teams = _getTeams();
        var aiProfiles = _getAIProfiles();

        foreach (var playerId in _freeAgentPool)
        {
            var player = _getPlayer(playerId);
            if (player == null) continue;

            var interested = new List<string>();
            var tier = GetPlayerTier(player);

            foreach (var team in teams)
            {
                if (team.Id == _getPlayerTeamId()) continue; // Skip human team (they make their own offers)

                // Base interest: does this team need this position?
                bool hasNeed = team.TeamNeeds.Contains(player.Position);
                float needBonus = hasNeed ? 0.3f : 0f;

                // Can the team afford it roughly?
                long estimatedAPY = ContractGenerator.GetMarketValue(player);
                if (!_capManager.CanAffordContract(team, estimatedAPY)) continue;

                // AI aggression
                float aggression = 0.5f;
                if (aiProfiles.TryGetValue(team.Id, out var profile))
                    aggression = profile.FreeAgencyAggression;

                // Interest probability based on tier
                float baseProb = tier switch
                {
                    FASigningTier.Elite => 0.4f,
                    FASigningTier.Starter => 0.25f,
                    FASigningTier.Depth => 0.15f,
                    _ => 0.05f,
                };

                float totalProb = baseProb + needBonus + (aggression - 0.5f) * 0.2f;
                if (rng.NextDouble() < totalProb)
                    interested.Add(team.Id);
            }

            // Ensure at least 1 team is interested in starters and above
            if (interested.Count == 0 && (tier == FASigningTier.Elite || tier == FASigningTier.Starter))
            {
                var randomTeam = teams
                    .Where(t => t.Id != _getPlayerTeamId() && _capManager.CanAffordContract(t, ContractGenerator.GetMarketValue(player)))
                    .OrderBy(_ => rng.Next())
                    .FirstOrDefault();
                if (randomTeam != null)
                    interested.Add(randomTeam.Id);
            }

            _playerInterest[playerId] = interested;
        }
    }

    private void GenerateAIOffersForTier(FASigningTier targetTier, int weekNum, Random rng)
    {
        var aiProfiles = _getAIProfiles();
        var calendar = _getCalendar();

        foreach (var playerId in _freeAgentPool.ToList())
        {
            var player = _getPlayer(playerId);
            if (player == null) continue;
            if (GetPlayerTier(player) != targetTier) continue;

            if (!_playerInterest.TryGetValue(playerId, out var interestedTeams)) continue;

            foreach (var teamId in interestedTeams)
            {
                // Check if this team already has a pending offer for this player
                if (_allOffers.Any(o => o.TeamId == teamId && o.PlayerId == playerId && o.Status == FreeAgentOfferStatus.Pending))
                    continue;

                var team = _getTeam(teamId);
                if (team == null) continue;

                var offer = GenerateAIOffer(team, player, aiProfiles, weekNum, rng);
                if (offer != null)
                {
                    _allOffers.Add(offer);
                }
            }
        }
    }

    private FreeAgentOffer? GenerateAIOffer(Team team, Player player,
        Dictionary<string, AIGMProfile> aiProfiles, int weekNum, Random rng)
    {
        long baseAPY = ContractGenerator.GetMarketValue(player);

        // Aggression modifier (0.85 - 1.15x)
        float aggression = 0.5f;
        if (aiProfiles.TryGetValue(team.Id, out var profile))
            aggression = profile.FreeAgencyAggression;
        float aggressionMod = 0.85f + aggression * 0.6f; // 0.3 agg = 1.03x, 0.8 agg = 1.33x

        // Need modifier (1.0 - 1.2x)
        float needMod = team.TeamNeeds.Contains(player.Position) ? 1.15f : 1.0f;

        // Age discount (30+ gets -5%/yr over 30)
        float ageMod = player.Age >= 30 ? 1.0f - (player.Age - 30) * 0.05f : 1.0f;
        ageMod = Math.Max(ageMod, 0.5f);

        long adjustedAPY = (long)(baseAPY * aggressionMod * needMod * ageMod);

        // Add variance ±10%
        float variance = 0.9f + (float)rng.NextDouble() * 0.2f;
        adjustedAPY = (long)(adjustedAPY * variance);

        // Can the team afford it?
        if (!_capManager.CanAffordContract(team, adjustedAPY)) return null;

        // Determine years
        var tier = GetPlayerTier(player);
        int years = tier switch
        {
            FASigningTier.Elite => rng.Next(3, 6),
            FASigningTier.Starter => rng.Next(2, 5),
            FASigningTier.Depth => rng.Next(1, 3),
            _ => 1,
        };

        // Reduce years for old players
        if (player.Age >= 33) years = Math.Min(years, 2);
        else if (player.Age >= 30) years = Math.Min(years, 3);

        long totalValue = adjustedAPY * years;
        float guaranteedPct = tier switch
        {
            FASigningTier.Elite => 0.50f + (float)rng.NextDouble() * 0.15f,
            FASigningTier.Starter => 0.40f + (float)rng.NextDouble() * 0.10f,
            _ => 0.25f + (float)rng.NextDouble() * 0.10f,
        };
        long guaranteed = (long)(totalValue * guaranteedPct);
        long signingBonus = (long)(totalValue * 0.12);

        return new FreeAgentOffer
        {
            PlayerId = player.Id,
            TeamId = team.Id,
            Years = years,
            TotalValue = totalValue,
            GuaranteedMoney = guaranteed,
            AnnualAverage = adjustedAPY,
            SigningBonus = signingBonus,
            OfferWeek = weekNum,
            IsPlayerOffer = false,
            Status = FreeAgentOfferStatus.Pending,
        };
    }

    private void ProcessPendingSignings(Random rng)
    {
        var calendar = _getCalendar();

        // Group pending offers by player
        var offersByPlayer = _allOffers
            .Where(o => o.Status == FreeAgentOfferStatus.Pending && !o.IsPlayerOffer)
            .GroupBy(o => o.PlayerId)
            .ToList();

        foreach (var group in offersByPlayer)
        {
            var player = _getPlayer(group.Key);
            if (player == null) continue;
            if (!_freeAgentPool.Contains(player.Id)) continue; // already signed

            var offers = group.OrderByDescending(o => o.TotalValue).ToList();
            if (offers.Count == 0) continue;

            // Best offer wins with some randomness for team prestige
            var bestOffer = SelectBestOffer(offers, player, rng);
            if (bestOffer == null) continue;

            // Sign the player
            var contract = ContractGenerator.GenerateFromOffer(bestOffer, calendar.CurrentYear);
            var result = _rosterManager.SignFreeAgent(player.Id, bestOffer.TeamId, contract);

            if (result.Success)
            {
                bestOffer.Status = FreeAgentOfferStatus.Accepted;
                _freeAgentPool.Remove(player.Id);

                // Reject all other offers for this player
                foreach (var other in offers.Where(o => o.Id != bestOffer.Id))
                    other.Status = FreeAgentOfferStatus.Rejected;
            }
        }
    }

    private FreeAgentOffer? SelectBestOffer(List<FreeAgentOffer> offers, Player player, Random rng)
    {
        if (offers.Count == 0) return null;
        if (offers.Count == 1) return offers[0];

        // Score each offer
        var scored = new List<(FreeAgentOffer Offer, double Score)>();
        foreach (var offer in offers)
        {
            double score = offer.TotalValue / 100_000_000.0; // Base: money in millions

            // Team prestige bonus (coach prestige 0-99 → 0-5 bonus)
            var team = _getTeam(offer.TeamId);
            if (team?.HeadCoachId != null)
            {
                var coaches = _getCoaches();
                var hc = coaches.FirstOrDefault(c => c.Id == team.HeadCoachId);
                if (hc != null)
                    score += hc.Prestige * 0.05;
            }

            // Win record bonus (winning teams attract FAs)
            if (team != null)
            {
                float winPct = (team.CurrentRecord.Wins + team.CurrentRecord.Losses + team.CurrentRecord.Ties) > 0
                    ? (float)team.CurrentRecord.Wins / (team.CurrentRecord.Wins + team.CurrentRecord.Losses + team.CurrentRecord.Ties)
                    : 0.5f;
                score += winPct * 3.0; // 0-3 bonus for winning teams
            }

            // TeamPlayer trait: slight loyalty/stability preference (small bonus)
            if (player.Traits.TeamPlayer)
                score += rng.NextDouble() * 2.0;

            // Add randomness
            score += rng.NextDouble() * 3.0;

            scored.Add((offer, score));
        }

        return scored.OrderByDescending(s => s.Score).First().Offer;
    }

    private void ProcessPlayerOffers(Random rng)
    {
        var calendar = _getCalendar();

        foreach (var offer in _playerPendingOffers.ToList())
        {
            if (offer.Status != FreeAgentOfferStatus.Pending) continue;

            var player = _getPlayer(offer.PlayerId);
            if (player == null || !_freeAgentPool.Contains(offer.PlayerId))
            {
                offer.Status = FreeAgentOfferStatus.Expired;
                _playerPendingOffers.Remove(offer);
                continue;
            }

            // Get best AI offer for comparison
            var aiOffers = _allOffers
                .Where(o => o.PlayerId == offer.PlayerId && !o.IsPlayerOffer && o.Status == FreeAgentOfferStatus.Pending)
                .ToList();

            long bestAIValue = aiOffers.Count > 0 ? aiOffers.Max(o => o.TotalValue) : 0;

            // Evaluate player offer
            double acceptProb = EvaluatePlayerOffer(offer, bestAIValue, player, rng);

            if (rng.NextDouble() < acceptProb)
            {
                // Accept
                var contract = ContractGenerator.GenerateFromOffer(offer, calendar.CurrentYear);
                var result = _rosterManager.SignFreeAgent(player.Id, offer.TeamId, contract);

                if (result.Success)
                {
                    offer.Status = FreeAgentOfferStatus.Accepted;
                    _freeAgentPool.Remove(player.Id);

                    // Reject all other offers
                    foreach (var other in _allOffers.Where(o => o.PlayerId == player.Id && o.Id != offer.Id))
                        other.Status = FreeAgentOfferStatus.Rejected;
                }
            }
            else
            {
                offer.Status = FreeAgentOfferStatus.Rejected;
            }

            _playerPendingOffers.Remove(offer);
        }
    }

    private double EvaluatePlayerOffer(FreeAgentOffer offer, long bestAIValue, Player player, Random rng)
    {
        if (bestAIValue <= 0)
            return 0.90; // No competition — very likely to accept

        double ratio = (double)offer.TotalValue / bestAIValue;

        // Base acceptance probability from money ratio
        double prob = ratio switch
        {
            >= 1.10 => 0.95,
            >= 1.0 => 0.85,
            >= 0.95 => 0.75,
            >= 0.90 => 0.55,
            >= 0.80 => 0.30,
            >= 0.70 => 0.15,
            _ => 0.05,
        };

        // Team competitiveness bonus
        var team = _getTeam(offer.TeamId);
        if (team != null)
        {
            float winPct = (team.CurrentRecord.Wins + team.CurrentRecord.Losses + team.CurrentRecord.Ties) > 0
                ? (float)team.CurrentRecord.Wins / (team.CurrentRecord.Wins + team.CurrentRecord.Losses + team.CurrentRecord.Ties)
                : 0.5f;
            if (winPct >= 0.6f) prob += 0.05;
        }

        // Coach prestige bonus
        if (team?.HeadCoachId != null)
        {
            var coaches = _getCoaches();
            var hc = coaches.FirstOrDefault(c => c.Id == team.HeadCoachId);
            if (hc != null && hc.Prestige >= 70) prob += 0.05;
        }

        return Math.Clamp(prob, 0.0, 0.98);
    }

    private void ProcessBargainBin(Random rng)
    {
        var calendar = _getCalendar();
        var teams = _getTeams();

        // Any remaining unsigned FAs with OVR >= 65 get minimum contract offers from needy teams
        foreach (var playerId in _freeAgentPool.ToList())
        {
            var player = _getPlayer(playerId);
            if (player == null || player.Overall < 65) continue;

            // Find a team that needs this position and has roster space
            var needyTeam = teams
                .Where(t => t.Id != _getPlayerTeamId()
                           && t.PlayerIds.Count < _capManager.ActiveRosterSize
                           && (t.TeamNeeds.Contains(player.Position) || t.PlayerIds.Count < 48))
                .OrderBy(_ => rng.Next())
                .FirstOrDefault();

            if (needyTeam == null) continue;

            var contract = ContractGenerator.GenerateMinimumContract(
                player.YearsInLeague, calendar.CurrentYear, player.Id, needyTeam.Id);

            var result = _rosterManager.SignFreeAgent(player.Id, needyTeam.Id, contract);
            if (result.Success)
                _freeAgentPool.Remove(playerId);
        }
    }

    // --- Helpers ---

    public static FASigningTier GetPlayerTier(Player player)
    {
        return player.Overall switch
        {
            >= 90 => FASigningTier.Elite,
            >= 80 => FASigningTier.Starter,
            >= 70 => FASigningTier.Depth,
            >= 60 => FASigningTier.Minimum,
            _ => FASigningTier.PracticeSquad,
        };
    }

    private static void RemoveFromDepthChart(Team team, string playerId)
    {
        foreach (var kvp in team.DepthChart.Chart)
            kvp.Value.Remove(playerId);
    }

    // --- State for Save/Load ---

    public void SetState(List<string> pool, List<FreeAgentOffer> offers, int week)
    {
        _freeAgentPool = new List<string>(pool);
        _allOffers = new List<FreeAgentOffer>(offers);
        _playerPendingOffers = offers.Where(o => o.IsPlayerOffer && o.Status == FreeAgentOfferStatus.Pending).ToList();
    }

    public (List<string> Pool, List<FreeAgentOffer> Offers) GetState()
    {
        return (new List<string>(_freeAgentPool), new List<FreeAgentOffer>(_allOffers));
    }
}
