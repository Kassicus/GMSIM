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
    public static List<Game> GenerateRegularSeason(
        List<Team> teams, int season, Random rng,
        Dictionary<string, int>? priorDivisionRanks = null)
    {
        var divisions = GroupByDivision(teams);

        List<Game>? bestSchedule = null;
        int bestErrorCount = int.MaxValue;

        for (int attempt = 1; attempt <= 10; attempt++)
        {
            var matchups = GenerateAllMatchups(teams, divisions, season, rng, priorDivisionRanks);
            var games = AssignWeeks(matchups, teams, divisions, season, rng);

            var (errors, warnings) = VerifySchedule(games, teams, divisions);

            if (errors.Count == 0)
            {
                if (attempt > 1)
                    Godot.GD.Print($"Schedule: valid on attempt {attempt}");
                foreach (var w in warnings)
                    Godot.GD.Print($"Schedule soft warning: {w}");
                return games;
            }

            if (errors.Count < bestErrorCount)
            {
                bestErrorCount = errors.Count;
                bestSchedule = games;
            }

            foreach (var e in errors)
                Godot.GD.Print($"Schedule attempt {attempt}: {e}");
        }

        Godot.GD.Print($"Schedule WARNING: returning best-effort schedule ({bestErrorCount} errors after 10 attempts)");
        return bestSchedule!;
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
        int season, Random rng, Dictionary<string, int>? priorDivisionRanks)
    {
        var matchups = new List<(string home, string away)>();
        var allDivs = new[] { Division.North, Division.South, Division.East, Division.West };

        // Build effective division ranks (guaranteed unique 1-4 per division)
        var ranks = BuildEffectiveRanks(divisions, priorDivisionRanks, rng);

        // === 1. Divisional Games (6 per team, 96 total) ===
        // Each team plays every division rival home and away
        foreach (var divTeams in divisions.Values)
        {
            for (int i = 0; i < divTeams.Count; i++)
            for (int j = i + 1; j < divTeams.Count; j++)
            {
                matchups.Add((divTeams[i].Id, divTeams[j].Id));
                matchups.Add((divTeams[j].Id, divTeams[i].Id));
            }
        }

        // === 2. Intra-Conference Rotation (4 per team, 64 total) ===
        // 3 possible complete pairings of 4 divisions, rotating every 3 years
        var intraPairings = new (int, int)[][]
        {
            new[] { (0, 1), (2, 3) },  // N↔S, E↔W
            new[] { (0, 2), (1, 3) },  // N↔E, S↔W
            new[] { (0, 3), (1, 2) },  // N↔W, S↔E
        };
        int intraPairingIdx = ((season - BaseYear) % 3 + 3) % 3;
        var currentIntraPairs = intraPairings[intraPairingIdx];

        foreach (var conf in new[] { Conference.AFC, Conference.NFC })
        {
            foreach (var (divIdxA, divIdxB) in currentIntraPairs)
            {
                var teamsA = divisions[(conf, allDivs[divIdxA])];
                var teamsB = divisions[(conf, allDivs[divIdxB])];
                AddFullDivisionMatchups(matchups, teamsA, teamsB);
            }
        }

        // === 3. Inter-Conference Rotation (4 per team, 64 total) ===
        // Each AFC division plays a different NFC division, rotating yearly over 4 years
        int interRotIdx = ((season - BaseYear) % 4 + 4) % 4;
        for (int d = 0; d < 4; d++)
        {
            var afcTeams = divisions[(Conference.AFC, allDivs[d])];
            var nfcDiv = allDivs[(d + interRotIdx) % 4];
            var nfcTeams = divisions[(Conference.NFC, nfcDiv)];
            AddFullDivisionMatchups(matchups, afcTeams, nfcTeams);
        }

        // === 4. Ranked Pair (2 per team, 32 total) ===
        // Each team plays the same-ranked team from the 2 remaining same-conference divisions
        var intraPartner = new Dictionary<int, int>();
        foreach (var (a, b) in currentIntraPairs)
        {
            intraPartner[a] = b;
            intraPartner[b] = a;
        }

        var rankedPairSeen = new HashSet<(string, string)>();
        foreach (var conf in new[] { Conference.AFC, Conference.NFC })
        {
            for (int divIdx = 0; divIdx < 4; divIdx++)
            {
                int partnerIdx = intraPartner[divIdx];
                var remainingDivIdxs = Enumerable.Range(0, 4)
                    .Where(d => d != divIdx && d != partnerIdx)
                    .ToList();

                var ownTeams = divisions[(conf, allDivs[divIdx])];

                foreach (var remDivIdx in remainingDivIdxs)
                {
                    var remTeams = divisions[(conf, allDivs[remDivIdx])];

                    foreach (var team in ownTeams)
                    {
                        int teamRank = ranks[team.Id];
                        var opponent = remTeams.First(t => ranks[t.Id] == teamRank);

                        // Skip if already added (from opponent's perspective)
                        var pairKey = string.Compare(team.Id, opponent.Id, StringComparison.Ordinal) < 0
                            ? (team.Id, opponent.Id) : (opponent.Id, team.Id);
                        if (!rankedPairSeen.Add(pairKey)) continue;

                        // Alternate home/away based on division indices + season
                        bool homeFirst = (divIdx + remDivIdx + season) % 2 == 0;
                        matchups.Add(homeFirst
                            ? (team.Id, opponent.Id)
                            : (opponent.Id, team.Id));
                    }
                }
            }
        }

        // === 5. 17th Game (1 per team, 16 total) ===
        // Inter-conference game vs team with same rank from the division played 2 years ago
        int pastInterRotIdx = ((season - BaseYear - 2) % 4 + 4) % 4;
        bool afcHome = (season % 2 == 0);

        for (int d = 0; d < 4; d++)
        {
            var afcTeams = divisions[(Conference.AFC, allDivs[d])];
            var nfcDiv = allDivs[(d + pastInterRotIdx) % 4];
            var nfcTeams = divisions[(Conference.NFC, nfcDiv)];

            foreach (var afcTeam in afcTeams)
            {
                int teamRank = ranks[afcTeam.Id];
                var nfcOpponent = nfcTeams.First(t => ranks[t.Id] == teamRank);

                matchups.Add(afcHome
                    ? (afcTeam.Id, nfcOpponent.Id)
                    : (nfcOpponent.Id, afcTeam.Id));
            }
        }

        // === Verification ===
        var gameCount = new Dictionary<string, int>();
        foreach (var t in teams) gameCount[t.Id] = 0;
        foreach (var (h, a) in matchups) { gameCount[h]++; gameCount[a]++; }

        foreach (var t in teams)
        {
            if (gameCount[t.Id] != 17)
                Godot.GD.Print($"Schedule WARNING: {t.FullName} has {gameCount[t.Id]} games (expected 17)");
        }

        return matchups;
    }

    /// <summary>
    /// Generates 16 matchups between two 4-team divisions (2H/2A per team).
    /// </summary>
    private static void AddFullDivisionMatchups(
        List<(string home, string away)> matchups,
        List<Team> divA, List<Team> divB)
    {
        for (int i = 0; i < divA.Count; i++)
        {
            // i hosts divB[i] and divB[(i+1)%4]
            matchups.Add((divA[i].Id, divB[i].Id));
            matchups.Add((divA[i].Id, divB[(i + 1) % 4].Id));
            // i visits divB[(i+2)%4] and divB[(i+3)%4]
            matchups.Add((divB[(i + 2) % 4].Id, divA[i].Id));
            matchups.Add((divB[(i + 3) % 4].Id, divA[i].Id));
        }
    }

    /// <summary>
    /// Builds a rank dictionary where every team in every division has a unique rank 1-4.
    /// Uses prior-year division ranks when available, falls back to random.
    /// </summary>
    private static Dictionary<string, int> BuildEffectiveRanks(
        Dictionary<(Conference, Division), List<Team>> divisions,
        Dictionary<string, int>? priorRanks, Random rng)
    {
        var ranks = new Dictionary<string, int>();

        foreach (var (key, divTeams) in divisions)
        {
            bool usePrior = false;
            if (priorRanks != null)
            {
                var divRanks = divTeams.Select(t => priorRanks.GetValueOrDefault(t.Id, 0)).ToList();
                usePrior = divRanks.Distinct().Count() == 4 && divRanks.All(r => r >= 1 && r <= 4);
            }

            if (usePrior)
            {
                foreach (var t in divTeams)
                    ranks[t.Id] = priorRanks![t.Id];
            }
            else
            {
                var shuffled = divTeams.OrderBy(_ => rng.Next()).ToList();
                for (int i = 0; i < shuffled.Count; i++)
                    ranks[shuffled[i].Id] = i + 1;
            }
        }

        return ranks;
    }

    // --- Private: Week Assignment ---

    private static List<Game> AssignWeeks(
        List<(string home, string away)> matchups, List<Team> teams,
        Dictionary<(Conference, Division), List<Team>> divisions,
        int season, Random rng)
    {
        int totalWeeks = 18;
        var byeWeeks = AssignByeWeeks(teams, rng);

        // Build divisional pairs set for week 18 constraint
        var divisionalPairs = new HashSet<(string, string)>();
        foreach (var divTeams in divisions.Values)
        {
            for (int i = 0; i < divTeams.Count; i++)
            for (int j = i + 1; j < divTeams.Count; j++)
            {
                divisionalPairs.Add((divTeams[i].Id, divTeams[j].Id));
                divisionalPairs.Add((divTeams[j].Id, divTeams[i].Id));
            }
        }

        // --- State arrays ---
        var gameWeek = new int[matchups.Count]; // matchup index → assigned week (-1 = unassigned)
        var used = new Dictionary<string, HashSet<int>>();
        var teamWeekGame = new Dictionary<string, Dictionary<int, int>>(); // team → week → matchup index

        // --- Local helpers ---
        void ResetState()
        {
            Array.Fill(gameWeek, -1);
            foreach (var t in teams)
            {
                used[t.Id] = new HashSet<int> { byeWeeks[t.Id] };
                teamWeekGame[t.Id] = new Dictionary<int, int>();
            }
        }

        bool CanPlace(int gi, int week)
        {
            var (h, a) = matchups[gi];
            return !used[h].Contains(week) && !used[a].Contains(week);
        }

        void Place(int gi, int week)
        {
            var (h, a) = matchups[gi];
            gameWeek[gi] = week;
            used[h].Add(week);
            used[a].Add(week);
            teamWeekGame[h][week] = gi;
            teamWeekGame[a][week] = gi;
        }

        void Unplace(int gi)
        {
            int w = gameWeek[gi];
            if (w < 0) return;
            var (h, a) = matchups[gi];
            used[h].Remove(w);
            used[a].Remove(w);
            teamWeekGame[h].Remove(w);
            teamWeekGame[a].Remove(w);
            gameWeek[gi] = -1;
        }

        string GetOpponent(int gi, string teamId)
        {
            var (h, a) = matchups[gi];
            return h == teamId ? a : h;
        }

        bool HasConsecutiveViolation(int gi, int week)
        {
            var (h, a) = matchups[gi];
            var sched = teamWeekGame[h];
            if (sched.TryGetValue(week - 1, out int prevGi) && prevGi != gi
                && GetOpponent(prevGi, h) == a) return true;
            if (sched.TryGetValue(week + 1, out int nextGi) && nextGi != gi
                && GetOpponent(nextGi, h) == a) return true;
            return false;
        }

        // ===========================================
        // Phase 1: Place ALL games (greedy + swap)
        // Only constraints: bye weeks + max 1 game per team per week
        // No week-18 or consecutive-opponent constraints yet
        // ===========================================
        int bestMissed = int.MaxValue;
        int[]? bestAssignment = null;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            ResetState();
            var order = Enumerable.Range(0, matchups.Count).OrderBy(_ => rng.Next()).ToList();
            int missed = 0;

            foreach (int gi in order)
            {
                bool placed = false;
                foreach (int w in Enumerable.Range(1, totalWeeks).OrderBy(_ => rng.Next()))
                {
                    if (CanPlace(gi, w))
                    {
                        Place(gi, w);
                        placed = true;
                        break;
                    }
                }
                if (!placed) missed++;
            }

            if (missed == 0) { bestMissed = 0; break; }
            if (missed < bestMissed)
            {
                bestMissed = missed;
                bestAssignment = (int[])gameWeek.Clone();
            }
        }

        // Swap fallback for any unassigned games from best attempt
        if (bestMissed > 0)
        {
            ResetState();
            for (int i = 0; i < matchups.Count; i++)
            {
                if (bestAssignment![i] > 0) Place(i, bestAssignment[i]);
            }

            var unassigned = Enumerable.Range(0, matchups.Count)
                .Where(i => gameWeek[i] < 0)
                .OrderBy(_ => rng.Next())
                .ToList();

            foreach (int gi in unassigned)
            {
                var (home, away) = matchups[gi];
                bool resolved = false;

                for (int w = 1; w <= totalWeeks && !resolved; w++)
                {
                    if (CanPlace(gi, w))
                    {
                        Place(gi, w);
                        resolved = true;
                        continue;
                    }

                    bool hFree = !used[home].Contains(w);
                    bool aFree = !used[away].Contains(w);
                    if (!hFree && !aFree) continue;

                    string blocker = hFree ? away : home;
                    if (!teamWeekGame[blocker].TryGetValue(w, out int blockingGi)) continue;

                    int origWeek = gameWeek[blockingGi];
                    Unplace(blockingGi);

                    if (!CanPlace(gi, w)) { Place(blockingGi, origWeek); continue; }

                    bool movedBlocker = false;
                    foreach (int altW in Enumerable.Range(1, totalWeeks)
                        .Where(ww => ww != w).OrderBy(_ => rng.Next()))
                    {
                        if (CanPlace(blockingGi, altW))
                        {
                            Place(blockingGi, altW);
                            Place(gi, w);
                            resolved = true;
                            movedBlocker = true;
                            break;
                        }
                    }

                    if (!movedBlocker) Place(blockingGi, origWeek);
                }

                if (!resolved)
                    Godot.GD.Print($"Schedule WARNING: Could not place {home} vs {away}");
            }
        }

        // Perturb-and-retry: randomly displace placed games to create slack
        // for any remaining unassigned games (handles zero-slack scheduling)
        var stillUnassigned = Enumerable.Range(0, matchups.Count)
            .Where(i => gameWeek[i] < 0).ToList();

        for (int repair = 0; repair < 50 && stillUnassigned.Count > 0; repair++)
        {
            var savedState = (int[])gameWeek.Clone();
            int prevCount = stillUnassigned.Count;

            // Unplace ~15 random games to create slack
            var toDisplace = Enumerable.Range(0, matchups.Count)
                .Where(i => gameWeek[i] > 0)
                .OrderBy(_ => rng.Next())
                .Take(15)
                .ToList();

            foreach (int pgi in toDisplace)
                Unplace(pgi);

            // Re-run greedy on all unplaced games (unassigned + displaced)
            var toPlace = Enumerable.Range(0, matchups.Count)
                .Where(i => gameWeek[i] < 0)
                .OrderBy(_ => rng.Next())
                .ToList();

            foreach (int gi in toPlace)
            {
                if (gameWeek[gi] > 0) continue;
                foreach (int w in Enumerable.Range(1, totalWeeks).OrderBy(_ => rng.Next()))
                {
                    if (CanPlace(gi, w)) { Place(gi, w); break; }
                }
            }

            var newUnassigned = Enumerable.Range(0, matchups.Count)
                .Where(i => gameWeek[i] < 0).ToList();

            if (newUnassigned.Count < prevCount)
            {
                stillUnassigned = newUnassigned;
            }
            else
            {
                // No improvement — restore previous state
                ResetState();
                for (int i = 0; i < matchups.Count; i++)
                {
                    if (savedState[i] > 0) Place(i, savedState[i]);
                }
            }
        }

        if (stillUnassigned.Count > 0)
            Godot.GD.Print($"Schedule WARNING: {stillUnassigned.Count} games could not be placed after all repair attempts");

        // ===========================================
        // Phase 2a: Week 18 = divisional games only
        // Swap non-divisional week-18 games with divisional games from other weeks
        // ===========================================
        var week18NonDiv = Enumerable.Range(0, matchups.Count)
            .Where(i => gameWeek[i] == 18 && !divisionalPairs.Contains(matchups[i]))
            .OrderBy(_ => rng.Next())
            .ToList();

        foreach (int ndGi in week18NonDiv)
        {
            var candidates = Enumerable.Range(0, matchups.Count)
                .Where(i => gameWeek[i] >= 1 && gameWeek[i] <= 17
                    && divisionalPairs.Contains(matchups[i]))
                .OrderBy(_ => rng.Next())
                .ToList();

            bool swapped = false;
            foreach (int divGi in candidates)
            {
                int divWeek = gameWeek[divGi];
                Unplace(ndGi);
                Unplace(divGi);

                if (CanPlace(ndGi, divWeek) && CanPlace(divGi, 18))
                {
                    Place(ndGi, divWeek);
                    Place(divGi, 18);
                    swapped = true;
                    break;
                }

                // Restore both games to original weeks
                Place(divGi, divWeek);
                Place(ndGi, 18);
            }

            if (!swapped)
            {
                var (h, a) = matchups[ndGi];
                Godot.GD.Print($"Schedule WARNING: Non-divisional game {h} vs {a} stuck in week 18");
            }
        }

        // ===========================================
        // Phase 2b: Fix consecutive-opponent violations
        // Move games to eliminate same-opponent-in-consecutive-weeks
        // ===========================================
        for (int pass = 0; pass < 5; pass++)
        {
            bool anyFixed = false;

            for (int gi = 0; gi < matchups.Count; gi++)
            {
                int w = gameWeek[gi];
                if (w < 1) continue;
                if (!HasConsecutiveViolation(gi, w)) continue;

                Unplace(gi);
                bool moved = false;

                foreach (int altW in Enumerable.Range(1, totalWeeks)
                    .Where(ww => ww != w).OrderBy(_ => rng.Next()))
                {
                    if (!CanPlace(gi, altW)) continue;

                    Place(gi, altW);
                    if (HasConsecutiveViolation(gi, altW))
                    {
                        Unplace(gi);
                        continue;
                    }

                    moved = true;
                    anyFixed = true;
                    break;
                }

                if (!moved) Place(gi, w); // restore original
            }

            if (!anyFixed) break;
        }

        // ===========================================
        // Build output
        // ===========================================
        var games = new List<Game>();
        for (int i = 0; i < matchups.Count; i++)
        {
            if (gameWeek[i] > 0)
            {
                games.Add(new Game
                {
                    Season = season,
                    Week = gameWeek[i],
                    HomeTeamId = matchups[i].home,
                    AwayTeamId = matchups[i].away,
                });
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

    // --- Private: Schedule Verification ---

    /// <summary>
    /// Verifies schedule correctness. Returns (hardErrors, softWarnings).
    /// Hard errors trigger regeneration; soft warnings are logged only.
    /// </summary>
    private static (List<string> errors, List<string> warnings) VerifySchedule(
        List<Game> games, List<Team> teams,
        Dictionary<(Conference, Division), List<Team>> divisions)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Build per-team schedule: which weeks each team plays
        var teamWeeks = new Dictionary<string, HashSet<int>>();
        var gameCount = new Dictionary<string, int>();
        foreach (var t in teams)
        {
            teamWeeks[t.Id] = new HashSet<int>();
            gameCount[t.Id] = 0;
        }

        foreach (var g in games)
        {
            if (!teamWeeks[g.HomeTeamId].Add(g.Week))
                errors.Add($"DUPLICATE: {g.HomeTeamId} has multiple games in week {g.Week}");
            if (!teamWeeks[g.AwayTeamId].Add(g.Week))
                errors.Add($"DUPLICATE: {g.AwayTeamId} has multiple games in week {g.Week}");
            gameCount[g.HomeTeamId]++;
            gameCount[g.AwayTeamId]++;
        }

        // 1. Each team plays exactly 17 games
        foreach (var t in teams)
        {
            if (gameCount[t.Id] != 17)
                errors.Add($"{t.FullName}: {gameCount[t.Id]} games (expected 17)");
        }

        // 2. All byes must be in weeks 5-14
        foreach (var t in teams)
        {
            int byeCount = 0;
            for (int w = 1; w <= 18; w++)
            {
                if (!teamWeeks[t.Id].Contains(w))
                {
                    byeCount++;
                    if (w < 5 || w > 14)
                        errors.Add($"{t.FullName}: bye in week {w} (must be 5-14)");
                }
            }
            if (byeCount != 1)
                errors.Add($"{t.FullName}: {byeCount} bye weeks (expected 1)");
        }

        // 3. Week 18 = divisional games only
        var divPairs = new HashSet<(string, string)>();
        foreach (var divTeams in divisions.Values)
        {
            for (int i = 0; i < divTeams.Count; i++)
            for (int j = i + 1; j < divTeams.Count; j++)
            {
                divPairs.Add((divTeams[i].Id, divTeams[j].Id));
                divPairs.Add((divTeams[j].Id, divTeams[i].Id));
            }
        }
        foreach (var g in games.Where(g => g.Week == 18))
        {
            if (!divPairs.Contains((g.HomeTeamId, g.AwayTeamId)))
                warnings.Add($"Week 18: non-divisional {g.HomeTeamId} vs {g.AwayTeamId}");
        }

        // 4. No consecutive opponents
        foreach (var t in teams)
        {
            var weekOpp = new Dictionary<int, string>();
            foreach (var g in games)
            {
                if (g.HomeTeamId == t.Id) weekOpp[g.Week] = g.AwayTeamId;
                else if (g.AwayTeamId == t.Id) weekOpp[g.Week] = g.HomeTeamId;
            }
            for (int w = 1; w <= 17; w++)
            {
                if (weekOpp.TryGetValue(w, out var o1) && weekOpp.TryGetValue(w + 1, out var o2) && o1 == o2)
                    warnings.Add($"{t.FullName}: same opponent in weeks {w} and {w + 1}");
            }
        }

        return (errors, warnings);
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
