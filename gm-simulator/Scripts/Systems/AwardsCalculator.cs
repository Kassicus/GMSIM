using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public static class AwardsCalculator
{
    public static SeasonAwards Calculate(int year, List<Player> players, List<Team> teams, Season season)
    {
        var awards = new SeasonAwards { Year = year };

        var completedGames = season.Games.Where(g => g.IsCompleted && !g.IsPlayoff).ToList();
        if (completedGames.Count == 0) return awards;

        var teamWinPct = CalculateTeamWinPcts(completedGames);

        // Only consider active players with stats for this season
        var eligible = players
            .Where(p => p.RosterStatus != RosterStatus.Retired && p.CareerStats.ContainsKey(year))
            .ToList();

        if (eligible.Count == 0) return awards;

        // MVP
        awards.MvpId = CalculateMVP(eligible, year, teamWinPct);

        // DPOY
        awards.DpoyId = CalculateDPOY(eligible, year);

        // OROY / DROY
        var rookies = eligible.Where(p => p.YearsInLeague <= 1).ToList();
        awards.OroyId = CalculateOROY(rookies, year, teamWinPct);
        awards.DroyId = CalculateDROY(rookies, year);

        // All-Pro and Pro Bowl
        CalculateAllProAndProBowl(awards, eligible, year, teamWinPct);

        return awards;
    }

    private static Dictionary<string, float> CalculateTeamWinPcts(List<Game> games)
    {
        var records = new Dictionary<string, (int wins, int games)>();

        foreach (var game in games)
        {
            if (!records.ContainsKey(game.HomeTeamId))
                records[game.HomeTeamId] = (0, 0);
            if (!records.ContainsKey(game.AwayTeamId))
                records[game.AwayTeamId] = (0, 0);

            var home = records[game.HomeTeamId];
            var away = records[game.AwayTeamId];

            home.games++;
            away.games++;

            if (game.HomeScore > game.AwayScore)
                home.wins++;
            else if (game.AwayScore > game.HomeScore)
                away.wins++;

            records[game.HomeTeamId] = home;
            records[game.AwayTeamId] = away;
        }

        var result = new Dictionary<string, float>();
        foreach (var (teamId, record) in records)
            result[teamId] = record.games > 0 ? (float)record.wins / record.games : 0.5f;

        return result;
    }

    private static string? CalculateMVP(List<Player> players, int year, Dictionary<string, float> winPct)
    {
        float bestScore = float.MinValue;
        string? bestId = null;

        foreach (var p in players)
        {
            var stats = p.CareerStats[year];
            float teamWin = p.TeamId != null && winPct.ContainsKey(p.TeamId)
                ? winPct[p.TeamId] : 0.5f;

            float score;
            if (p.Position == Position.QB)
            {
                score = stats.PassingYards * 0.01f
                    + stats.PassingTDs * 4f
                    - stats.Interceptions * 3f
                    + stats.RushingYards * 0.01f
                    + stats.RushingTDs * 3f
                    + teamWin * 20f;
            }
            else if (IsOffensivePosition(p.Position))
            {
                int totalYds = stats.RushingYards + stats.ReceivingYards;
                int totalTds = stats.RushingTDs + stats.ReceivingTDs;
                score = totalYds * 0.02f + totalTds * 5f + teamWin * 15f;
            }
            else
            {
                // Defensive players can win MVP but need exceptional stats
                score = stats.Sacks * 5f + stats.InterceptionsDef * 8f
                    + stats.ForcedFumbles * 4f + teamWin * 12f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestId = p.Id;
            }
        }

        return bestId;
    }

    private static string? CalculateDPOY(List<Player> players, int year)
    {
        var defenders = players.Where(p => IsDefensivePosition(p.Position)).ToList();
        if (defenders.Count == 0) return null;

        return defenders
            .OrderByDescending(p =>
            {
                var s = p.CareerStats[year];
                return s.Sacks * 5f + s.InterceptionsDef * 6f + s.TacklesForLoss * 3f
                    + s.ForcedFumbles * 4f + s.PassesDefended * 2f + s.TotalTackles * 0.3f;
            })
            .First().Id;
    }

    private static string? CalculateOROY(List<Player> rookies, int year, Dictionary<string, float> winPct)
    {
        var offRookies = rookies.Where(p => IsOffensivePosition(p.Position) || p.Position == Position.QB).ToList();
        if (offRookies.Count == 0) return null;

        return offRookies
            .OrderByDescending(p =>
            {
                var s = p.CareerStats[year];
                float teamWin = p.TeamId != null && winPct.ContainsKey(p.TeamId)
                    ? winPct[p.TeamId] : 0.5f;

                if (p.Position == Position.QB)
                    return s.PassingYards * 0.01f + s.PassingTDs * 4f - s.Interceptions * 3f + teamWin * 10f;

                int totalYds = s.RushingYards + s.ReceivingYards;
                int totalTds = s.RushingTDs + s.ReceivingTDs;
                return totalYds * 0.02f + totalTds * 5f + teamWin * 8f;
            })
            .First().Id;
    }

    private static string? CalculateDROY(List<Player> rookies, int year)
    {
        var defRookies = rookies.Where(p => IsDefensivePosition(p.Position)).ToList();
        if (defRookies.Count == 0) return null;

        return defRookies
            .OrderByDescending(p =>
            {
                var s = p.CareerStats[year];
                return s.Sacks * 5f + s.InterceptionsDef * 6f + s.TacklesForLoss * 3f
                    + s.ForcedFumbles * 4f + s.PassesDefended * 2f + s.TotalTackles * 0.3f;
            })
            .First().Id;
    }

    private static void CalculateAllProAndProBowl(
        SeasonAwards awards, List<Player> players, int year, Dictionary<string, float> winPct)
    {
        // Group by position, score each player, select top per position
        var positionGroups = new[]
        {
            Position.QB, Position.HB, Position.WR, Position.TE,
            Position.LT, Position.LG, Position.C, Position.RG, Position.RT,
            Position.EDGE, Position.DT, Position.MLB, Position.OLB,
            Position.CB, Position.FS, Position.SS,
            Position.K, Position.P,
        };

        // How many pro bowlers per position
        var proBowlSlots = new Dictionary<Position, int>
        {
            { Position.QB, 3 }, { Position.HB, 3 }, { Position.WR, 4 }, { Position.TE, 2 },
            { Position.LT, 2 }, { Position.LG, 2 }, { Position.C, 2 }, { Position.RG, 2 }, { Position.RT, 2 },
            { Position.EDGE, 4 }, { Position.DT, 3 }, { Position.MLB, 3 }, { Position.OLB, 3 },
            { Position.CB, 4 }, { Position.FS, 2 }, { Position.SS, 2 },
            { Position.K, 1 }, { Position.P, 1 },
        };

        foreach (var pos in positionGroups)
        {
            var posPlayers = players.Where(p => p.Position == pos).ToList();
            if (posPlayers.Count == 0) continue;

            var ranked = posPlayers
                .OrderByDescending(p => GetCompositeScore(p, year, winPct))
                .ToList();

            // 1st Team All-Pro
            if (ranked.Count > 0)
                awards.FirstTeamAllPro.Add(ranked[0].Id);

            // 2nd Team All-Pro
            if (ranked.Count > 1)
                awards.SecondTeamAllPro.Add(ranked[1].Id);

            // Pro Bowl
            int slots = proBowlSlots.GetValueOrDefault(pos, 2);
            for (int i = 0; i < Math.Min(slots, ranked.Count); i++)
            {
                if (!awards.ProBowlIds.Contains(ranked[i].Id))
                    awards.ProBowlIds.Add(ranked[i].Id);
            }
        }
    }

    private static float GetCompositeScore(Player p, int year, Dictionary<string, float> winPct)
    {
        float ovrScore = p.Overall * 0.4f;

        if (!p.CareerStats.ContainsKey(year))
            return ovrScore;

        var s = p.CareerStats[year];
        float teamWin = p.TeamId != null && winPct.ContainsKey(p.TeamId)
            ? winPct[p.TeamId] : 0.5f;

        float statsScore;
        if (p.Position == Position.QB)
        {
            statsScore = s.PassingYards * 0.005f + s.PassingTDs * 2f
                - s.Interceptions * 1.5f + teamWin * 10f;
        }
        else if (IsDefensivePosition(p.Position))
        {
            statsScore = s.Sacks * 3f + s.InterceptionsDef * 4f + s.TacklesForLoss * 2f
                + s.ForcedFumbles * 2.5f + s.PassesDefended * 1.5f + s.TotalTackles * 0.2f;
        }
        else if (p.Position == Position.K)
        {
            statsScore = s.FGAttempted > 0 ? (float)s.FGMade / s.FGAttempted * 30f + s.FGLong * 0.3f : 0;
        }
        else if (p.Position == Position.P)
        {
            statsScore = s.PuntAverage * 1.5f + s.PuntsInside20 * 2f - s.Touchbacks * 0.5f;
        }
        else
        {
            // Offensive skill/line positions
            int totalYds = s.RushingYards + s.ReceivingYards;
            int totalTds = s.RushingTDs + s.ReceivingTDs;
            statsScore = totalYds * 0.01f + totalTds * 3f + s.Receptions * 0.1f + teamWin * 5f;
        }

        return ovrScore + statsScore * 0.6f;
    }

    private static bool IsOffensivePosition(Position pos) =>
        pos is Position.QB or Position.HB or Position.FB or Position.WR or Position.TE
            or Position.LT or Position.LG or Position.C or Position.RG or Position.RT;

    private static bool IsDefensivePosition(Position pos) =>
        pos is Position.EDGE or Position.DT or Position.MLB or Position.OLB
            or Position.CB or Position.FS or Position.SS;
}
