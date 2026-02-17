using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class SimulationEngine
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<List<Coach>> _getCoaches;
    private readonly Func<Random> _getRng;
    private readonly Func<string, Player?> _getPlayer;
    private readonly Func<string, Team?> _getTeam;
    private readonly Func<string, Coach?> _getCoach;

    private readonly InjurySystem _injurySystem;

    private static readonly Dictionary<Position, float> PositionWeights = new()
    {
        { Position.QB, 0.18f }, { Position.EDGE, 0.08f }, { Position.CB, 0.07f },
        { Position.WR, 0.07f }, { Position.LT, 0.06f }, { Position.RT, 0.05f },
        { Position.DT, 0.05f }, { Position.HB, 0.05f }, { Position.TE, 0.04f },
        { Position.FS, 0.04f }, { Position.SS, 0.04f }, { Position.MLB, 0.04f },
        { Position.LG, 0.03f }, { Position.RG, 0.03f }, { Position.C, 0.03f },
        { Position.OLB, 0.03f }, { Position.K, 0.02f }, { Position.P, 0.015f },
        { Position.FB, 0.01f }, { Position.LS, 0.005f },
    };

    public SimulationEngine(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<Coach>> getCoaches,
        Func<Random> getRng,
        Func<string, Player?> getPlayer,
        Func<string, Team?> getTeam,
        Func<string, Coach?> getCoach,
        InjurySystem injurySystem)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getCoaches = getCoaches;
        _getRng = getRng;
        _getPlayer = getPlayer;
        _getTeam = getTeam;
        _getCoach = getCoach;
        _injurySystem = injurySystem;
    }

    /// <summary>
    /// Simulates a single game and returns a full GameResult.
    /// Does NOT mutate any state -- caller applies results.
    /// </summary>
    public GameResult SimulateGame(Game game)
    {
        var rng = _getRng();
        var homeTeam = _getTeam(game.HomeTeamId)!;
        var awayTeam = _getTeam(game.AwayTeamId)!;

        float homePower = CalculateTeamPower(homeTeam);
        float awayPower = CalculateTeamPower(awayTeam);

        // Generate scores
        var (homeScore, awayScore) = GenerateScore(homePower, awayPower, game.IsPlayoff, rng);

        // Generate quarter scores
        var homeQuarters = SplitIntoQuarters(homeScore, rng);
        var awayQuarters = SplitIntoQuarters(awayScore, rng);

        // Determine game script
        bool homeWinning = homeScore >= awayScore;

        // Generate team stats
        var homeTeamStats = GenerateTeamStats(homeScore, homePower, true, homeWinning, rng);
        var awayTeamStats = GenerateTeamStats(awayScore, awayPower, false, !homeWinning, rng);

        // Generate player stats
        var playerStats = new Dictionary<string, PlayerGameStats>();
        GenerateOffensiveStats(homeTeam, homeTeamStats, homeWinning, playerStats, rng);
        GenerateOffensiveStats(awayTeam, awayTeamStats, !homeWinning, playerStats, rng);
        GenerateDefensiveStats(homeTeam, awayTeamStats, playerStats, rng);
        GenerateDefensiveStats(awayTeam, homeTeamStats, playerStats, rng);
        GenerateSpecialTeamsStats(homeTeam, homeScore, homeTeamStats, playerStats, rng);
        GenerateSpecialTeamsStats(awayTeam, awayScore, awayTeamStats, playerStats, rng);

        // Process injuries
        var homePlayers = GetActiveStarters(homeTeam);
        var awayPlayers = GetActiveStarters(awayTeam);
        var injuries = _injurySystem.ProcessGameInjuries(homePlayers, awayPlayers);

        // Player of the game
        var (potgId, potgLine) = DeterminePlayerOfTheGame(playerStats, homeTeam, awayTeam, homeScore, awayScore);

        // Key plays
        var keyPlays = GenerateKeyPlays(homeTeam, awayTeam, homeScore, awayScore, playerStats, rng);

        return new GameResult
        {
            GameId = game.Id,
            HomeScore = homeScore,
            AwayScore = awayScore,
            HomeQuarterScores = homeQuarters,
            AwayQuarterScores = awayQuarters,
            HomeTeamStats = homeTeamStats,
            AwayTeamStats = awayTeamStats,
            PlayerStats = playerStats,
            Injuries = injuries,
            PlayerOfTheGameId = potgId,
            PlayerOfTheGameLine = potgLine,
            KeyPlays = keyPlays,
        };
    }

    public float CalculateTeamPower(Team team)
    {
        float power = 0f;

        foreach (var (pos, weight) in PositionWeights)
        {
            var starter = GetStarter(team, pos);
            if (starter != null && starter.CurrentInjury == null)
            {
                power += starter.Overall * weight;
            }
            else
            {
                var backup = GetBackup(team, pos);
                if (backup != null)
                    power += backup.Overall * weight * 0.85f;
                else
                    power += 40f * weight;
            }
        }

        power += CalculateDepthBonus(team) * 0.05f;
        power += GetCoachingModifier(team);

        return power;
    }

    // --- Score Generation ---

    private (int home, int away) GenerateScore(float homePower, float awayPower, bool isPlayoff, Random rng)
    {
        float adjustedHomePower = homePower + 3.0f; // home field advantage

        float homeExpected = 17f + (adjustedHomePower - 50f) * 0.25f;
        float awayExpected = 17f + (awayPower - 50f) * 0.25f;

        // Clamp expected to reasonable range
        homeExpected = Math.Clamp(homeExpected, 10f, 42f);
        awayExpected = Math.Clamp(awayExpected, 10f, 42f);

        int homeScore = Math.Max(0, (int)Math.Round(homeExpected + NextGaussian(rng) * 7.0));
        int awayScore = Math.Max(0, (int)Math.Round(awayExpected + NextGaussian(rng) * 7.0));

        // Snap to realistic NFL scoring
        homeScore = SnapToNFLScore(homeScore, rng);
        awayScore = SnapToNFLScore(awayScore, rng);

        // Handle ties in playoffs
        if (isPlayoff && homeScore == awayScore)
        {
            // OT: slight home advantage
            if (rng.NextDouble() < 0.55)
                homeScore += rng.Next(2) == 0 ? 3 : 7;
            else
                awayScore += rng.Next(2) == 0 ? 3 : 7;
        }

        return (homeScore, awayScore);
    }

    private static int SnapToNFLScore(int raw, Random rng)
    {
        // NFL scores are combinations of TDs (7 pts) and FGs (3 pts)
        // Common scores: 0,3,6,7,9,10,13,14,16,17,20,21,23,24,27,28,30,31,34,35,37,38
        if (raw <= 0) return 0;

        // Calculate via TDs and FGs
        int tds = raw / 7;
        int remaining = raw - tds * 7;
        int fgs = remaining / 3;
        int reconstructed = tds * 7 + fgs * 3;

        // Small chance of 2-point conversions or safeties
        if (rng.Next(10) == 0 && reconstructed > 0)
            reconstructed += rng.Next(2) == 0 ? 1 : 2; // missed XP or 2-pt conversion

        return Math.Max(0, reconstructed);
    }

    private static int[] SplitIntoQuarters(int totalScore, Random rng)
    {
        var quarters = new int[4];
        int remaining = totalScore;

        // Distribute score across quarters with realistic weighting
        // Q1 ~20%, Q2 ~30%, Q3 ~20%, Q4 ~30%
        float[] weights = { 0.20f, 0.30f, 0.20f, 0.30f };

        for (int i = 0; i < 3; i++)
        {
            float expected = totalScore * weights[i];
            int qScore = Math.Max(0, (int)Math.Round(expected + (rng.NextDouble() - 0.5) * 6));

            // Snap to scoring increments (0, 3, 6, 7, 10, 13, 14)
            qScore = SnapQuarterScore(qScore);
            qScore = Math.Min(qScore, remaining);
            quarters[i] = qScore;
            remaining -= qScore;
        }

        quarters[3] = Math.Max(0, remaining);
        return quarters;
    }

    private static int SnapQuarterScore(int raw)
    {
        int[] common = { 0, 3, 6, 7, 10, 13, 14, 17, 20, 21, 24, 27, 28 };
        int closest = 0;
        int minDiff = int.MaxValue;
        foreach (var c in common)
        {
            int diff = Math.Abs(raw - c);
            if (diff < minDiff) { minDiff = diff; closest = c; }
        }
        return closest;
    }

    // --- Team Stats ---

    private static TeamGameStats GenerateTeamStats(int score, float power, bool isHome, bool isWinning, Random rng)
    {
        int totalYards = Math.Max(150, score * 13 + rng.Next(-40, 41));

        // Pass/rush split by game script
        float passRatio = isWinning ? 0.55f : 0.65f;
        if (Math.Abs(score) <= 3) passRatio = 0.60f; // close game

        int passingYards = (int)(totalYards * passRatio);
        int rushingYards = totalYards - passingYards;

        int turnovers = rng.Next(4); // 0-3
        if (power > 75) turnovers = Math.Max(0, turnovers - 1); // better teams turn it over less

        int firstDowns = totalYards / 15 + rng.Next(-2, 3);
        int thirdDownAtt = 10 + rng.Next(8);
        int thirdDownConv = (int)(thirdDownAtt * (0.30 + power * 0.003 + rng.NextDouble() * 0.1));
        thirdDownConv = Math.Clamp(thirdDownConv, 1, thirdDownAtt);

        int penalties = 4 + rng.Next(8);
        int penaltyYards = penalties * (5 + rng.Next(8));

        int topSeconds = isWinning ? 1800 + rng.Next(200) : 1600 + rng.Next(200);
        // Adjust to roughly 60 min total — will balance when both teams are summed

        int sacks = rng.Next(5); // team sack count against this team
        int sackYards = sacks * (5 + rng.Next(6));

        return new TeamGameStats
        {
            TotalYards = totalYards,
            PassingYards = passingYards,
            RushingYards = rushingYards,
            Turnovers = turnovers,
            FirstDowns = firstDowns,
            ThirdDownConversions = thirdDownConv,
            ThirdDownAttempts = thirdDownAtt,
            Penalties = penalties,
            PenaltyYards = penaltyYards,
            TimeOfPossessionSeconds = topSeconds,
            Sacks = sacks,
            SackYards = sackYards,
        };
    }

    // --- Offensive Stats ---

    private void GenerateOffensiveStats(
        Team team, TeamGameStats teamStats, bool isWinning,
        Dictionary<string, PlayerGameStats> stats, Random rng)
    {
        int totalPassYards = teamStats.PassingYards;
        int totalRushYards = teamStats.RushingYards;
        int score = teamStats.TotalYards / 13; // rough reverse

        // Estimate TDs and FGs from score (using the team's actual score would be better,
        // but we only have teamStats here - reconstruct from total yards)
        int totalTDs = Math.Max(1, score / 7);
        int passingTDs = (int)(totalTDs * 0.60f + rng.NextDouble() * 0.2 * totalTDs);
        int rushingTDs = totalTDs - passingTDs;
        passingTDs = Math.Max(0, passingTDs);
        rushingTDs = Math.Max(0, rushingTDs);

        // QB passing
        var qb = GetStarter(team, Position.QB);
        if (qb != null)
        {
            float compPct = 0.55f + qb.Attributes.ShortAccuracy * 0.002f + qb.Attributes.MediumAccuracy * 0.001f;
            compPct = Math.Clamp(compPct, 0.50f, 0.80f);
            compPct += (float)(rng.NextDouble() * 0.10 - 0.05);

            float ypc = 9f + qb.Attributes.DeepAccuracy * 0.04f + (float)(rng.NextDouble() * 3 - 1.5);
            int completions = Math.Max(10, (int)(totalPassYards / ypc));
            int attempts = Math.Max(completions, (int)(completions / compPct));

            int ints = teamStats.Turnovers > 0 ? rng.Next(Math.Min(teamStats.Turnovers, 3) + 1) : 0;
            int sacked = teamStats.Sacks;

            // QB rushing
            int qbRushYards = (int)(totalRushYards * (0.05f + rng.NextDouble() * 0.08));
            int qbRushAtts = Math.Max(1, qbRushYards / (3 + rng.Next(4)));

            var qbStats = GetOrCreateStats(stats, qb.Id);
            qbStats.Completions = completions;
            qbStats.Attempts = attempts;
            qbStats.PassingYards = totalPassYards;
            qbStats.PassingTDs = passingTDs;
            qbStats.Interceptions = ints;
            qbStats.Sacked = sacked;
            qbStats.RushAttempts = qbRushAtts;
            qbStats.RushingYards = qbRushYards;
            qbStats.RushingTDs = rng.Next(10) == 0 ? 1 : 0;

            totalRushYards -= qbRushYards;
        }

        // RB rushing
        var hbs = GetDepthPlayers(team, Position.HB);
        if (hbs.Count > 0)
        {
            float hb1Share = 0.65f + (float)(rng.NextDouble() * 0.10);
            int hb1Yards = (int)(totalRushYards * hb1Share);
            int hb1Atts = Math.Max(1, hb1Yards / (3 + rng.Next(3)));

            var hb1Stats = GetOrCreateStats(stats, hbs[0].Id);
            hb1Stats.RushAttempts = hb1Atts;
            hb1Stats.RushingYards = hb1Yards;
            hb1Stats.RushingTDs = Math.Min(rushingTDs, rng.Next(1, Math.Max(2, rushingTDs + 1)));
            hb1Stats.Fumbles = rng.Next(8) == 0 ? 1 : 0;
            hb1Stats.FumblesLost = hb1Stats.Fumbles > 0 && rng.Next(2) == 0 ? 1 : 0;

            int remainingRushYards = totalRushYards - hb1Yards;
            int remainingRushTDs = Math.Max(0, rushingTDs - hb1Stats.RushingTDs);

            if (hbs.Count > 1 && remainingRushYards > 0)
            {
                int hb2Yards = remainingRushYards;
                int hb2Atts = Math.Max(1, hb2Yards / (3 + rng.Next(3)));
                var hb2Stats = GetOrCreateStats(stats, hbs[1].Id);
                hb2Stats.RushAttempts = hb2Atts;
                hb2Stats.RushingYards = hb2Yards;
                hb2Stats.RushingTDs = remainingRushTDs;
            }

            // RB receiving
            int hbTargets = (int)(qb != null ? stats[qb.Id].Attempts * 0.12 : 4);
            float catchRate = 0.65f + hbs[0].Attributes.Catching * 0.002f;
            int hbRec = (int)(hbTargets * catchRate);
            int hbRecYards = hbRec * (5 + rng.Next(4));

            hb1Stats.Targets = hbTargets;
            hb1Stats.Receptions = hbRec;
            hb1Stats.ReceivingYards = hbRecYards;
        }

        // WR receiving
        var wrs = GetDepthPlayers(team, Position.WR);
        if (wrs.Count > 0 && qb != null)
        {
            int totalTargets = stats[qb.Id].Attempts;
            int hbTargetsUsed = hbs.Count > 0 ? GetOrCreateStats(stats, hbs[0].Id).Targets : 0;
            int remainingTargets = Math.Max(0, totalTargets - hbTargetsUsed);

            float[] wrShares = wrs.Count switch
            {
                1 => new[] { 1.0f },
                2 => new[] { 0.58f, 0.42f },
                3 => new[] { 0.35f, 0.30f, 0.35f },
                _ => new[] { 0.30f, 0.25f, 0.22f, 0.23f },
            };

            int remainingRecTDs = passingTDs;
            int totalRecYardsUsed = hbs.Count > 0 ? GetOrCreateStats(stats, hbs[0].Id).ReceivingYards : 0;
            int passYardsPool = Math.Max(0, totalPassYards - totalRecYardsUsed);

            for (int i = 0; i < Math.Min(wrs.Count, wrShares.Length); i++)
            {
                int targets = Math.Max(1, (int)(remainingTargets * wrShares[i]));
                float catchRate = 0.55f + wrs[i].Attributes.Catching * 0.003f;
                catchRate = Math.Clamp(catchRate, 0.45f, 0.85f);
                int receptions = Math.Max(0, (int)(targets * catchRate));

                float ypr = 10f + wrs[i].Attributes.Speed * 0.05f + (float)(rng.NextDouble() * 4 - 2);
                int recYards = (int)(receptions * ypr);
                recYards = Math.Min(recYards, passYardsPool);
                passYardsPool -= recYards;

                int recTDs = 0;
                if (remainingRecTDs > 0 && rng.NextDouble() < 0.4 + wrs[i].Overall * 0.003)
                {
                    recTDs = 1;
                    remainingRecTDs--;
                }

                var wrStats = GetOrCreateStats(stats, wrs[i].Id);
                wrStats.Targets = targets;
                wrStats.Receptions = receptions;
                wrStats.ReceivingYards = recYards;
                wrStats.ReceivingTDs = recTDs;
            }

            // Give any remaining TDs to WR1
            if (remainingRecTDs > 0 && wrs.Count > 0)
            {
                GetOrCreateStats(stats, wrs[0].Id).ReceivingTDs += remainingRecTDs;
            }
        }

        // TE receiving
        var tes = GetDepthPlayers(team, Position.TE);
        if (tes.Count > 0 && qb != null)
        {
            int teTargets = (int)(stats[qb.Id].Attempts * 0.15);
            float catchRate = 0.60f + tes[0].Attributes.Catching * 0.002f;
            int teRec = (int)(teTargets * catchRate);
            int teRecYards = teRec * (8 + rng.Next(5));

            var teStats = GetOrCreateStats(stats, tes[0].Id);
            teStats.Targets = teTargets;
            teStats.Receptions = teRec;
            teStats.ReceivingYards = teRecYards;
            teStats.ReceivingTDs = rng.Next(5) == 0 ? 1 : 0;
        }
    }

    // --- Defensive Stats ---

    private void GenerateDefensiveStats(
        Team team, TeamGameStats opponentStats,
        Dictionary<string, PlayerGameStats> stats, Random rng)
    {
        // Total tackles distributed across defense
        int totalTackles = 40 + rng.Next(25);

        var defenders = new List<(Player player, float tacklePower)>();
        foreach (var pos in new[] { Position.MLB, Position.OLB, Position.EDGE, Position.DT, Position.CB, Position.FS, Position.SS })
        {
            foreach (var p in GetDepthPlayers(team, pos).Take(2))
            {
                float power = (p.Attributes.Tackle + p.Attributes.Pursuit) / 2f;
                // Position weighting for tackles
                power *= pos switch
                {
                    Position.MLB => 1.5f,
                    Position.OLB => 1.2f,
                    Position.SS => 1.1f,
                    Position.EDGE => 0.9f,
                    Position.CB => 0.8f,
                    Position.FS => 1.0f,
                    Position.DT => 0.7f,
                    _ => 1.0f,
                };
                defenders.Add((p, power));
            }
        }

        if (defenders.Count == 0) return;

        float totalPower = defenders.Sum(d => d.tacklePower);

        foreach (var (player, tacklePower) in defenders)
        {
            float share = tacklePower / totalPower;
            int tackles = Math.Max(1, (int)(totalTackles * share + rng.NextDouble() * 2 - 1));
            int solo = (int)(tackles * (0.55 + rng.NextDouble() * 0.2));
            int assisted = tackles - solo;

            var pStats = GetOrCreateStats(stats, player.Id);
            pStats.TotalTackles = tackles;
            pStats.SoloTackles = solo;
        }

        // Sacks
        int teamSacks = opponentStats.Sacks;
        var passRushers = defenders
            .Where(d => d.player.Position is Position.EDGE or Position.DT or Position.OLB)
            .OrderByDescending(d => (d.player.Attributes.FinesseMoves + d.player.Attributes.PowerMoves) / 2f)
            .ToList();

        float remainingSacks = teamSacks;
        foreach (var (player, _) in passRushers)
        {
            if (remainingSacks <= 0) break;
            float rushPower = (player.Attributes.FinesseMoves + player.Attributes.PowerMoves) / 200f;
            float sacks = remainingSacks * rushPower * (0.8f + (float)rng.NextDouble() * 0.4f);
            sacks = Math.Min(sacks, remainingSacks);

            if (sacks >= 0.5f)
            {
                var pStats = GetOrCreateStats(stats, player.Id);
                pStats.Sacks = (float)Math.Round(sacks * 2) / 2f; // round to 0.5
                pStats.QBHits = (int)pStats.Sacks + rng.Next(2);
                pStats.TacklesForLoss = (int)pStats.Sacks + rng.Next(2);
                remainingSacks -= pStats.Sacks;
            }
        }

        // Interceptions
        int teamInts = opponentStats.Turnovers;
        var coverage = defenders
            .Where(d => d.player.Position is Position.CB or Position.FS or Position.SS)
            .OrderByDescending(d => d.player.Attributes.PlayRecognition + d.player.Attributes.ZoneCoverage)
            .ToList();

        for (int i = 0; i < Math.Min(teamInts, coverage.Count); i++)
        {
            if (rng.NextDouble() < 0.5) // not all turnovers are INTs (some are fumbles)
            {
                var pStats = GetOrCreateStats(stats, coverage[i].player.Id);
                pStats.InterceptionsDef++;
                pStats.PassesDefended++;
            }
        }

        // Passes defended
        foreach (var (player, _) in coverage)
        {
            if (rng.NextDouble() < 0.3)
            {
                var pStats = GetOrCreateStats(stats, player.Id);
                pStats.PassesDefended += 1 + rng.Next(2);
            }
        }

        // Forced fumbles
        if (teamInts > 0)
        {
            var hitter = defenders.OrderByDescending(d => d.player.Attributes.HitPower).FirstOrDefault();
            if (hitter.player != null && rng.NextDouble() < 0.3)
            {
                var pStats = GetOrCreateStats(stats, hitter.player.Id);
                pStats.ForcedFumbles = 1;
            }
        }
    }

    // --- Special Teams Stats ---

    private void GenerateSpecialTeamsStats(
        Team team, int teamScore, TeamGameStats teamStats,
        Dictionary<string, PlayerGameStats> stats, Random rng)
    {
        // Estimate TDs and FGs from score
        int estimatedTDs = teamScore / 7;
        int remainingPts = teamScore - estimatedTDs * 7;
        int fgCount = remainingPts / 3;

        // Kicker
        var kicker = GetStarter(team, Position.K);
        if (kicker != null)
        {
            int fgAtt = fgCount + (rng.Next(3) == 0 ? 1 : 0); // occasional miss
            int fgMade = fgCount;
            int xpAtt = estimatedTDs;
            int xpMade = Math.Max(0, xpAtt - (rng.Next(15) == 0 ? 1 : 0)); // rare miss

            var kStats = GetOrCreateStats(stats, kicker.Id);
            kStats.FGMade = fgMade;
            kStats.FGAttempted = fgAtt;
            kStats.XPMade = xpMade;
            kStats.XPAttempted = xpAtt;
        }

        // Punter
        var punter = GetStarter(team, Position.P);
        if (punter != null)
        {
            // More punts if team scored less
            int punts = Math.Max(1, 8 - estimatedTDs - fgCount + rng.Next(-1, 2));
            int puntYards = punts * (38 + rng.Next(12));

            var pStats = GetOrCreateStats(stats, punter.Id);
            pStats.Punts = punts;
            pStats.PuntYards = puntYards;
        }
    }

    // --- Player of the Game ---

    private (string? id, string? line) DeterminePlayerOfTheGame(
        Dictionary<string, PlayerGameStats> allStats,
        Team homeTeam, Team awayTeam, int homeScore, int awayScore)
    {
        string winningTeamId = homeScore >= awayScore ? homeTeam.Id : awayTeam.Id;
        float bestScore = 0;
        string? bestId = null;
        string? bestLine = null;

        foreach (var (playerId, pStats) in allStats)
        {
            var player = _getPlayer(playerId);
            if (player == null) continue;

            // Prefer winning team
            float teamBonus = player.TeamId == winningTeamId ? 1.2f : 1.0f;

            float score = 0;
            string line = "";

            if (pStats.PassingYards > 0)
            {
                score = pStats.PassingYards / 25f + pStats.PassingTDs * 6 - pStats.Interceptions * 4;
                line = $"{pStats.Completions}/{pStats.Attempts}, {pStats.PassingYards} yds, {pStats.PassingTDs} TD";
                if (pStats.Interceptions > 0) line += $", {pStats.Interceptions} INT";
            }

            if (pStats.RushingYards > 50 || pStats.ReceivingYards > 50)
            {
                float scrimmageScore = (pStats.RushingYards + pStats.ReceivingYards) / 15f
                    + (pStats.RushingTDs + pStats.ReceivingTDs) * 6;
                if (scrimmageScore > score)
                {
                    score = scrimmageScore;
                    var parts = new List<string>();
                    if (pStats.RushingYards > 0)
                        parts.Add($"{pStats.RushAttempts} car, {pStats.RushingYards} yds, {pStats.RushingTDs} TD");
                    if (pStats.ReceivingYards > 0)
                        parts.Add($"{pStats.Receptions} rec, {pStats.ReceivingYards} yds, {pStats.ReceivingTDs} TD");
                    line = string.Join(" | ", parts);
                }
            }

            float defScore = pStats.SoloTackles + pStats.Sacks * 3 + pStats.InterceptionsDef * 5
                + pStats.ForcedFumbles * 3 + pStats.DefensiveTDs * 8;
            if (defScore > score)
            {
                score = defScore;
                var parts = new List<string>();
                if (pStats.TotalTackles > 0) parts.Add($"{pStats.TotalTackles} tkl");
                if (pStats.Sacks > 0) parts.Add($"{pStats.Sacks} sack");
                if (pStats.InterceptionsDef > 0) parts.Add($"{pStats.InterceptionsDef} INT");
                if (pStats.ForcedFumbles > 0) parts.Add($"{pStats.ForcedFumbles} FF");
                line = string.Join(", ", parts);
            }

            score *= teamBonus;

            if (score > bestScore)
            {
                bestScore = score;
                bestId = playerId;
                bestLine = line;
            }
        }

        return (bestId, bestLine);
    }

    // --- Key Plays ---

    private List<string> GenerateKeyPlays(
        Team homeTeam, Team awayTeam, int homeScore, int awayScore,
        Dictionary<string, PlayerGameStats> stats, Random rng)
    {
        var plays = new List<string>();
        string[] quarters = { "Q1", "Q2", "Q3", "Q4" };

        // Find notable stat lines and generate narrative
        foreach (var (playerId, pStats) in stats)
        {
            var player = _getPlayer(playerId);
            if (player == null) continue;

            string q = quarters[rng.Next(4)];
            int min = rng.Next(1, 16);
            string clock = $"{q} {min}:{rng.Next(0, 60):D2}";

            if (pStats.PassingTDs >= 3)
                plays.Add($"{clock} - {player.LastName} throws TD #{pStats.PassingTDs}");
            if (pStats.RushingYards >= 100)
                plays.Add($"{clock} - {player.LastName} breaks 100 rushing yards");
            if (pStats.ReceivingYards >= 100)
                plays.Add($"{clock} - {player.LastName} hauls in a big catch");
            if (pStats.InterceptionsDef >= 1)
                plays.Add($"{clock} - {player.LastName} picks off the pass");
            if (pStats.Sacks >= 2)
                plays.Add($"{clock} - {player.LastName} gets sack #{(int)pStats.Sacks}");
            if (pStats.ForcedFumbles >= 1)
                plays.Add($"{clock} - {player.LastName} forces a fumble");
        }

        // Add a game-deciding play
        if (Math.Abs(homeScore - awayScore) <= 7)
            plays.Add($"Q4 2:{rng.Next(0, 60):D2} - Close finish, final score {homeTeam.Abbreviation} {homeScore} - {awayTeam.Abbreviation} {awayScore}");

        // Limit to 5 key plays
        return plays.OrderBy(_ => rng.Next()).Take(5).ToList();
    }

    // --- Helpers ---

    private Player? GetStarter(Team team, Position position)
    {
        if (!team.DepthChart.Chart.TryGetValue(position, out var chart) || chart.Count == 0)
            return null;

        foreach (var playerId in chart)
        {
            var player = _getPlayer(playerId);
            if (player != null && player.CurrentInjury == null)
                return player;
        }

        return null;
    }

    private Player? GetBackup(Team team, Position position)
    {
        if (!team.DepthChart.Chart.TryGetValue(position, out var chart) || chart.Count < 2)
            return null;

        for (int i = 1; i < chart.Count; i++)
        {
            var player = _getPlayer(chart[i]);
            if (player != null && player.CurrentInjury == null)
                return player;
        }

        return null;
    }

    private List<Player> GetDepthPlayers(Team team, Position position)
    {
        if (!team.DepthChart.Chart.TryGetValue(position, out var chart))
            return new List<Player>();

        return chart
            .Select(id => _getPlayer(id))
            .Where(p => p != null && p.CurrentInjury == null)
            .Cast<Player>()
            .ToList();
    }

    private List<Player> GetActiveStarters(Team team)
    {
        var starters = new List<Player>();
        foreach (var (pos, chart) in team.DepthChart.Chart)
        {
            if (chart.Count > 0)
            {
                var player = _getPlayer(chart[0]);
                if (player != null) starters.Add(player);
            }
        }
        return starters;
    }

    private float CalculateDepthBonus(Team team)
    {
        float totalBackupOvr = 0;
        int backupCount = 0;

        foreach (var (pos, chart) in team.DepthChart.Chart)
        {
            for (int i = 1; i < Math.Min(chart.Count, 3); i++)
            {
                var backup = _getPlayer(chart[i]);
                if (backup != null)
                {
                    totalBackupOvr += backup.Overall;
                    backupCount++;
                }
            }
        }

        return backupCount > 0 ? totalBackupOvr / backupCount : 50f;
    }

    private float GetCoachingModifier(Team team)
    {
        if (team.HeadCoachId == null) return 0;
        var hc = _getCoach(team.HeadCoachId);
        if (hc == null) return 0;

        // Map GameManagement (40-89 range) to [-5, +5]
        return (hc.GameManagement - 65f) / 5f;
    }

    private static PlayerGameStats GetOrCreateStats(Dictionary<string, PlayerGameStats> stats, string playerId)
    {
        if (!stats.TryGetValue(playerId, out var pStats))
        {
            pStats = new PlayerGameStats();
            stats[playerId] = pStats;
        }
        return pStats;
    }

    private static double NextGaussian(Random rng)
    {
        // Box-Muller transform
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
