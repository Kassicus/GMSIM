using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public record PlayoffSeed(string TeamId, int Seed, bool IsDivisionWinner);

public static class ScheduleGenerator
{
    private const int BaseYear = 2025;

    /// <summary>
    /// Generates an 18-week regular season schedule for all 32 teams.
    /// 17 games per team + 1 bye week per team.
    /// </summary>
    public static List<Game> GenerateRegularSeason(List<Team> teams, int season, Random rng)
    {
        var divisions = GroupByDivision(teams);
        var matchups = GenerateAllMatchups(teams, divisions, season, rng);
        return AssignWeeks(matchups, teams, season, rng);
    }

    /// <summary>
    /// Determines the 7 playoff teams per conference from final standings.
    /// </summary>
    public static (List<PlayoffSeed> AFC, List<PlayoffSeed> NFC) DeterminePlayoffSeeds(
        List<Team> teams, List<Game> regularSeasonGames)
    {
        var afc = SeedConference(teams, regularSeasonGames, Conference.AFC);
        var nfc = SeedConference(teams, regularSeasonGames, Conference.NFC);
        return (afc, nfc);
    }

    /// <summary>
    /// Generates playoff games for a given round.
    /// </summary>
    public static List<Game> GeneratePlayoffRound(
        List<PlayoffSeed> afcSeeds, List<PlayoffSeed> nfcSeeds,
        PlayoffRound round, int season, int week)
    {
        var games = new List<Game>();

        switch (round)
        {
            case PlayoffRound.WildCard:
                // #2 vs #7, #3 vs #6, #4 vs #5 per conference, #1 has bye
                games.AddRange(CreatePlayoffMatchups(afcSeeds, new[] { (1, 6), (2, 5), (3, 4) }, season, week));
                games.AddRange(CreatePlayoffMatchups(nfcSeeds, new[] { (1, 6), (2, 5), (3, 4) }, season, week));
                break;

            case PlayoffRound.Divisional:
                // #1 vs lowest remaining, other two play each other
                games.AddRange(CreateDivisionalMatchups(afcSeeds, season, week));
                games.AddRange(CreateDivisionalMatchups(nfcSeeds, season, week));
                break;

            case PlayoffRound.ConferenceChampionship:
                // Two remaining per conference
                if (afcSeeds.Count >= 2)
                    games.Add(CreatePlayoffGame(afcSeeds[0].TeamId, afcSeeds[1].TeamId, season, week));
                if (nfcSeeds.Count >= 2)
                    games.Add(CreatePlayoffGame(nfcSeeds[0].TeamId, nfcSeeds[1].TeamId, season, week));
                break;

            case PlayoffRound.SuperBowl:
                if (afcSeeds.Count >= 1 && nfcSeeds.Count >= 1)
                    games.Add(CreatePlayoffGame(afcSeeds[0].TeamId, nfcSeeds[0].TeamId, season, week));
                break;
        }

        return games;
    }

    /// <summary>
    /// Filters playoff seeds to only include winners from completed games.
    /// </summary>
    public static List<PlayoffSeed> FilterToWinners(List<PlayoffSeed> seeds, List<Game> completedGames)
    {
        var winnerIds = new HashSet<string>();
        foreach (var game in completedGames.Where(g => g.IsCompleted && g.IsPlayoff))
        {
            winnerIds.Add(game.HomeScore > game.AwayScore ? game.HomeTeamId : game.AwayTeamId);
        }

        // Also include teams that had a bye (weren't in any game)
        var participatingTeams = new HashSet<string>();
        foreach (var game in completedGames.Where(g => g.IsPlayoff))
        {
            participatingTeams.Add(game.HomeTeamId);
            participatingTeams.Add(game.AwayTeamId);
        }

        return seeds
            .Where(s => winnerIds.Contains(s.TeamId) || !participatingTeams.Contains(s.TeamId))
            .OrderBy(s => s.Seed)
            .ToList();
    }

    // --- Private: Matchup Generation ---

    private static Dictionary<(Conference, Division), List<Team>> GroupByDivision(List<Team> teams)
    {
        return teams
            .GroupBy(t => (t.Conference, t.Division))
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private static List<(string home, string away)> GenerateAllMatchups(
        List<Team> teams, Dictionary<(Conference, Division), List<Team>> divisions,
        int season, Random rng)
    {
        var matchups = new List<(string home, string away)>();
        var homeGames = new Dictionary<string, int>();
        foreach (var t in teams) homeGames[t.Id] = 0;

        var divisionKeys = divisions.Keys.ToList();

        foreach (var team in teams)
        {
            var conf = team.Conference;
            var div = team.Division;
            var ownDivKey = (conf, div);

            // 1. Divisional games (6): play each rival twice (home + away)
            var rivals = divisions[ownDivKey].Where(t => t.Id != team.Id).ToList();
            foreach (var rival in rivals)
            {
                var key = StringComparer.Ordinal.Compare(team.Id, rival.Id) < 0
                    ? (team.Id, rival.Id) : (rival.Id, team.Id);
                if (!matchups.Contains(key) && !matchups.Contains((key.Item2, key.Item1)))
                {
                    matchups.Add((team.Id, rival.Id));
                    matchups.Add((rival.Id, team.Id));
                }
            }

            // Will add cross-division games below
        }

        // Remove duplicates from divisional (we added both directions per pair above)
        // Actually rebuild divisional properly
        matchups.Clear();

        // Divisional: each pair plays home-and-home
        foreach (var divTeams in divisions.Values)
        {
            for (int i = 0; i < divTeams.Count; i++)
            {
                for (int j = i + 1; j < divTeams.Count; j++)
                {
                    matchups.Add((divTeams[i].Id, divTeams[j].Id)); // i hosts j
                    matchups.Add((divTeams[j].Id, divTeams[i].Id)); // j hosts i
                }
            }
        }
        // 6 games per team, 48 matchups per conference, 96 total divisional games

        // 2. Intra-conference rotation (4 games vs full division)
        var conferenceDivisions = new Dictionary<Conference, List<Division>>
        {
            { Conference.AFC, new List<Division> { Division.North, Division.South, Division.East, Division.West } },
            { Conference.NFC, new List<Division> { Division.North, Division.South, Division.East, Division.West } },
        };

        foreach (var conf in new[] { Conference.AFC, Conference.NFC })
        {
            var divs = conferenceDivisions[conf];
            foreach (var div in divs)
            {
                var ownTeams = divisions[(conf, div)];
                var otherDivs = divs.Where(d => d != div).ToList();
                int rotIndex = (season - BaseYear) % otherDivs.Count;
                if (rotIndex < 0) rotIndex += otherDivs.Count;
                var targetDiv = otherDivs[rotIndex];
                var targetTeams = divisions[(conf, targetDiv)];

                // Each team in div plays all 4 teams in targetDiv (2H/2A)
                for (int i = 0; i < ownTeams.Count; i++)
                {
                    // Alternate home/away: teams 0,1 host targetTeams 0,1; away vs 2,3
                    matchups.Add((ownTeams[i].Id, targetTeams[i].Id));
                    matchups.Add((ownTeams[i].Id, targetTeams[(i + 1) % 4].Id));
                    matchups.Add((targetTeams[(i + 2) % 4].Id, ownTeams[i].Id));
                    matchups.Add((targetTeams[(i + 3) % 4].Id, ownTeams[i].Id));
                }
            }
        }

        // Deduplicate intra-conference (each matchup was added from both divisions' perspective)
        matchups = DeduplicateMatchups(matchups);

        // 3. Inter-conference rotation (4 games vs full opposite-conference division)
        var allDivisions = new[] { Division.North, Division.South, Division.East, Division.West };
        foreach (var div in allDivisions)
        {
            int rotIndex = (season - BaseYear) % 4;
            if (rotIndex < 0) rotIndex += 4;
            var targetDiv = allDivisions[rotIndex];

            var afcTeams = divisions[(Conference.AFC, div)];
            var nfcTeams = divisions[(Conference.NFC, targetDiv)];

            for (int i = 0; i < afcTeams.Count; i++)
            {
                matchups.Add((afcTeams[i].Id, nfcTeams[i].Id));
                matchups.Add((afcTeams[i].Id, nfcTeams[(i + 1) % 4].Id));
                matchups.Add((nfcTeams[(i + 2) % 4].Id, afcTeams[i].Id));
                matchups.Add((nfcTeams[(i + 3) % 4].Id, afcTeams[i].Id));
            }
        }

        matchups = DeduplicateMatchups(matchups);

        // 4. Intra-conference same-finish-rank (2 games)
        foreach (var conf in new[] { Conference.AFC, Conference.NFC })
        {
            var divs = conferenceDivisions[conf];
            foreach (var div in divs)
            {
                var ownTeams = divisions[(conf, div)];
                var otherDivs = divs.Where(d => d != div).ToList();
                int rotIndex = (season - BaseYear) % otherDivs.Count;
                if (rotIndex < 0) rotIndex += otherDivs.Count;
                var rotationDiv = otherDivs[rotIndex]; // already played in full

                var remainingDivs = otherDivs.Where(d => d != rotationDiv).ToList();

                foreach (var team in ownTeams)
                {
                    // For year 1 (no prior standings), use random rank 0-3
                    int teamRank = rng.Next(4);

                    foreach (var remDiv in remainingDivs)
                    {
                        var remTeams = divisions[(conf, remDiv)];
                        int opponentRank = Math.Min(teamRank, remTeams.Count - 1);
                        var opponent = remTeams[opponentRank];

                        // Check if we already have this matchup
                        if (!HasMatchup(matchups, team.Id, opponent.Id))
                        {
                            // Alternate home/away
                            if (rng.Next(2) == 0)
                                matchups.Add((team.Id, opponent.Id));
                            else
                                matchups.Add((opponent.Id, team.Id));
                        }
                    }
                }
            }
        }

        matchups = DeduplicateMatchups(matchups);

        // 5. Inter-conference 17th game (1 game)
        foreach (var div in allDivisions)
        {
            int rotIndex = (season - BaseYear) % 4;
            if (rotIndex < 0) rotIndex += 4;
            var interDiv = allDivisions[rotIndex]; // already played in full

            var remainingDivs = allDivisions.Where(d => d != interDiv).ToList();
            // Pick one remaining division for the 17th game
            int seventeenthRotIndex = ((season - BaseYear) / 4) % remainingDivs.Count;
            if (seventeenthRotIndex < 0) seventeenthRotIndex += remainingDivs.Count;
            var seventeenthDiv = remainingDivs[seventeenthRotIndex];

            var afcTeams = divisions[(Conference.AFC, div)];
            var nfcTeams = divisions[(Conference.NFC, seventeenthDiv)];

            for (int i = 0; i < afcTeams.Count; i++)
            {
                int opponentIdx = rng.Next(nfcTeams.Count);
                var opponent = nfcTeams[opponentIdx];

                if (!HasMatchup(matchups, afcTeams[i].Id, opponent.Id))
                {
                    bool afcHome = (season % 2 == 0);
                    if (afcHome)
                        matchups.Add((afcTeams[i].Id, opponent.Id));
                    else
                        matchups.Add((opponent.Id, afcTeams[i].Id));
                }
            }
        }

        matchups = DeduplicateMatchups(matchups);

        // Verify: each team should have ~17 games. If short, fill with random opponents.
        var gameCount = new Dictionary<string, int>();
        foreach (var t in teams) gameCount[t.Id] = 0;
        foreach (var (h, a) in matchups)
        {
            gameCount[h]++;
            gameCount[a]++;
        }

        // Fill any team that has < 17 games
        foreach (var team in teams)
        {
            while (gameCount[team.Id] < 17)
            {
                // Find a team also short on games that we haven't played
                var candidate = teams
                    .Where(t => t.Id != team.Id && gameCount[t.Id] < 17
                                && !HasMatchup(matchups, team.Id, t.Id))
                    .OrderBy(_ => rng.Next())
                    .FirstOrDefault();

                if (candidate == null) break;

                if (rng.Next(2) == 0)
                    matchups.Add((team.Id, candidate.Id));
                else
                    matchups.Add((candidate.Id, team.Id));

                gameCount[team.Id]++;
                gameCount[candidate.Id]++;
            }
        }

        // Trim any team that has > 17 games
        var teamGames = new Dictionary<string, int>();
        foreach (var t in teams) teamGames[t.Id] = 0;
        var finalMatchups = new List<(string home, string away)>();

        foreach (var m in matchups)
        {
            if (teamGames.GetValueOrDefault(m.home) < 17 && teamGames.GetValueOrDefault(m.away) < 17)
            {
                finalMatchups.Add(m);
                teamGames[m.home]++;
                teamGames[m.away]++;
            }
        }

        return finalMatchups;
    }

    private static List<(string, string)> DeduplicateMatchups(List<(string home, string away)> matchups)
    {
        var seen = new HashSet<(string, string)>();
        var result = new List<(string, string)>();

        foreach (var m in matchups)
        {
            if (seen.Add(m))
                result.Add(m);
        }

        return result;
    }

    private static bool HasMatchup(List<(string home, string away)> matchups, string teamA, string teamB)
    {
        return matchups.Any(m =>
            (m.home == teamA && m.away == teamB) ||
            (m.home == teamB && m.away == teamA));
    }

    // --- Private: Week Assignment ---

    private static List<Game> AssignWeeks(
        List<(string home, string away)> matchups, List<Team> teams, int season, Random rng)
    {
        int totalWeeks = 18;
        var games = new List<Game>();

        // Assign bye weeks: each team gets 1 bye in weeks 5-14
        var byeWeeks = AssignByeWeeks(teams, rng);

        // Shuffle matchups for variety
        var shuffled = matchups.OrderBy(_ => rng.Next()).ToList();

        // Track which week each team plays
        var teamWeekUsed = new Dictionary<string, HashSet<int>>();
        foreach (var t in teams)
        {
            teamWeekUsed[t.Id] = new HashSet<int>();
            teamWeekUsed[t.Id].Add(byeWeeks[t.Id]); // block bye week
        }

        // Greedy assignment
        foreach (var (home, away) in shuffled)
        {
            bool assigned = false;

            for (int week = 1; week <= totalWeeks; week++)
            {
                if (!teamWeekUsed[home].Contains(week) && !teamWeekUsed[away].Contains(week))
                {
                    var game = new Game
                    {
                        Season = season,
                        Week = week,
                        HomeTeamId = home,
                        AwayTeamId = away,
                    };
                    games.Add(game);
                    teamWeekUsed[home].Add(week);
                    teamWeekUsed[away].Add(week);
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                // Fallback: find any open week
                for (int week = 1; week <= totalWeeks; week++)
                {
                    if (!teamWeekUsed[home].Contains(week) && !teamWeekUsed[away].Contains(week))
                    {
                        games.Add(new Game { Season = season, Week = week, HomeTeamId = home, AwayTeamId = away });
                        teamWeekUsed[home].Add(week);
                        teamWeekUsed[away].Add(week);
                        break;
                    }
                }
            }
        }

        return games.OrderBy(g => g.Week).ToList();
    }

    private static Dictionary<string, int> AssignByeWeeks(List<Team> teams, Random rng)
    {
        var byes = new Dictionary<string, int>();

        // Available bye weeks: 5-14 (10 weeks)
        // 32 teams / 10 weeks ≈ 3-4 teams per bye week
        // Ensure no more than 2 teams from same division share a bye
        var weekSlots = new Dictionary<int, List<string>>();
        for (int w = 5; w <= 14; w++) weekSlots[w] = new List<string>();

        var shuffledTeams = teams.OrderBy(_ => rng.Next()).ToList();
        int maxPerWeek = 4;

        foreach (var team in shuffledTeams)
        {
            var validWeeks = Enumerable.Range(5, 10)
                .Where(w => weekSlots[w].Count < maxPerWeek)
                .OrderBy(_ => rng.Next())
                .ToList();

            if (validWeeks.Count > 0)
            {
                int week = validWeeks[0];
                byes[team.Id] = week;
                weekSlots[week].Add(team.Id);
            }
            else
            {
                // Fallback
                byes[team.Id] = 5 + rng.Next(10);
            }
        }

        return byes;
    }

    // --- Private: Playoff Seeding ---

    private static List<PlayoffSeed> SeedConference(
        List<Team> allTeams, List<Game> games, Conference conf)
    {
        var confTeams = allTeams.Where(t => t.Conference == conf).ToList();

        // Group by division, find division winners
        var divisionGroups = confTeams.GroupBy(t => t.Division).ToList();
        var divisionWinners = new List<Team>();
        var nonWinners = new List<Team>();

        foreach (var divGroup in divisionGroups)
        {
            var sorted = divGroup
                .OrderByDescending(t => GetWinPct(t))
                .ThenByDescending(t => GetDivisionRecord(t, divGroup.ToList(), games))
                .ThenByDescending(t => GetConferenceRecord(t, confTeams, games))
                .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
                .ThenByDescending(t => t.CurrentRecord.PointsFor)
                .ToList();

            divisionWinners.Add(sorted[0]);
            nonWinners.AddRange(sorted.Skip(1));
        }

        // Sort division winners by record (seeds 1-4)
        divisionWinners = divisionWinners
            .OrderByDescending(t => GetWinPct(t))
            .ThenByDescending(t => GetConferenceRecord(t, confTeams, games))
            .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
            .ToList();

        // Sort remaining for wild card (seeds 5-7)
        var wildCards = nonWinners
            .OrderByDescending(t => GetWinPct(t))
            .ThenByDescending(t => GetConferenceRecord(t, confTeams, games))
            .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
            .ThenByDescending(t => t.CurrentRecord.PointsFor)
            .Take(3)
            .ToList();

        var seeds = new List<PlayoffSeed>();
        for (int i = 0; i < divisionWinners.Count; i++)
            seeds.Add(new PlayoffSeed(divisionWinners[i].Id, i + 1, true));
        for (int i = 0; i < wildCards.Count; i++)
            seeds.Add(new PlayoffSeed(wildCards[i].Id, divisionWinners.Count + i + 1, false));

        return seeds;
    }

    private static float GetWinPct(Team team)
    {
        int total = team.CurrentRecord.Wins + team.CurrentRecord.Losses + team.CurrentRecord.Ties;
        if (total == 0) return 0f;
        return (team.CurrentRecord.Wins + team.CurrentRecord.Ties * 0.5f) / total;
    }

    private static float GetDivisionRecord(Team team, List<Team> divTeams, List<Game> games)
    {
        var divIds = divTeams.Select(t => t.Id).ToHashSet();
        int wins = 0, total = 0;

        foreach (var game in games.Where(g => g.IsCompleted && !g.IsPlayoff))
        {
            bool isHome = game.HomeTeamId == team.Id;
            bool isAway = game.AwayTeamId == team.Id;
            if (!isHome && !isAway) continue;

            string opponentId = isHome ? game.AwayTeamId : game.HomeTeamId;
            if (!divIds.Contains(opponentId)) continue;

            total++;
            if (isHome && game.HomeScore > game.AwayScore) wins++;
            else if (isAway && game.AwayScore > game.HomeScore) wins++;
        }

        return total == 0 ? 0f : (float)wins / total;
    }

    private static float GetConferenceRecord(Team team, List<Team> confTeams, List<Game> games)
    {
        var confIds = confTeams.Select(t => t.Id).ToHashSet();
        int wins = 0, total = 0;

        foreach (var game in games.Where(g => g.IsCompleted && !g.IsPlayoff))
        {
            bool isHome = game.HomeTeamId == team.Id;
            bool isAway = game.AwayTeamId == team.Id;
            if (!isHome && !isAway) continue;

            string opponentId = isHome ? game.AwayTeamId : game.HomeTeamId;
            if (!confIds.Contains(opponentId)) continue;

            total++;
            if (isHome && game.HomeScore > game.AwayScore) wins++;
            else if (isAway && game.AwayScore > game.HomeScore) wins++;
        }

        return total == 0 ? 0f : (float)wins / total;
    }

    // --- Private: Playoff Game Creation ---

    private static List<Game> CreatePlayoffMatchups(
        List<PlayoffSeed> seeds, (int higher, int lower)[] pairings, int season, int week)
    {
        var games = new List<Game>();
        foreach (var (h, l) in pairings)
        {
            // Seeds are 1-indexed, list is 0-indexed
            var higher = seeds.FirstOrDefault(s => s.Seed == h + 1);
            var lower = seeds.FirstOrDefault(s => s.Seed == l + 1);
            if (higher != null && lower != null)
            {
                games.Add(CreatePlayoffGame(higher.TeamId, lower.TeamId, season, week));
            }
        }
        return games;
    }

    private static List<Game> CreateDivisionalMatchups(List<PlayoffSeed> seeds, int season, int week)
    {
        // Seeds should be the remaining teams after Wild Card
        // #1 plays lowest seed, the other two play each other
        if (seeds.Count < 3) return new List<Game>();

        var sorted = seeds.OrderBy(s => s.Seed).ToList();
        var games = new List<Game>
        {
            CreatePlayoffGame(sorted[0].TeamId, sorted[^1].TeamId, season, week), // #1 vs lowest
            CreatePlayoffGame(sorted[1].TeamId, sorted[^2].TeamId, season, week), // middle two
        };

        return games;
    }

    private static Game CreatePlayoffGame(string homeTeamId, string awayTeamId, int season, int week)
    {
        return new Game
        {
            Season = season,
            Week = week,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            IsPlayoff = true,
        };
    }
}
