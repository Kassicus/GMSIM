using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

/// <summary>
/// Calculates compensatory draft picks based on net free agent losses.
/// Teams that lose more valuable FAs than they gain receive comp picks in rounds 3-7.
/// </summary>
public static class CompensatoryPickCalculator
{
    public static List<DraftPick> CalculateCompensatoryPicks(
        List<TransactionRecord> transactions,
        List<Player> players,
        List<Team> teams,
        int faYear)
    {
        var compPicks = new List<DraftPick>();
        int draftYear = faYear; // comp picks awarded for same year's draft

        // Find all FA signings from this offseason
        var faSignings = transactions
            .Where(t => t.Type == TransactionType.Signed
                        && t.Year == faYear
                        && t.Phase == GamePhase.FreeAgency)
            .ToList();

        foreach (var team in teams)
        {
            // Players this team lost to FA (signed by other teams)
            var lost = new List<(Player Player, long APY)>();
            // Players this team gained from FA
            var gained = new List<(Player Player, long APY)>();

            foreach (var signing in faSignings)
            {
                if (signing.PlayerId == null || signing.TeamId == null) continue;
                var player = players.FirstOrDefault(p => p.Id == signing.PlayerId);
                if (player == null) continue;

                long apy = player.CurrentContract != null && player.CurrentContract.TotalYears > 0
                    ? player.CurrentContract.TotalValue / player.CurrentContract.TotalYears
                    : 0;

                if (signing.TeamId == team.Id && signing.OtherTeamId != null)
                {
                    // This team signed a player from another team
                    gained.Add((player, apy));
                }
                else if (signing.OtherTeamId == team.Id || IsLostFA(signing, team.Id, transactions, faYear))
                {
                    // This team lost a player to another team
                    lost.Add((player, apy));
                }
            }

            // Calculate net losses (lost value - gained value)
            var netLosses = CalculateNetLosses(lost, gained);

            // Generate comp picks from net losses (max 4)
            int picksAwarded = 0;
            foreach (var (_, apy) in netLosses.OrderByDescending(x => x.APY))
            {
                if (picksAwarded >= 4) break;

                int round = GetCompRound(apy);
                if (round > 7) continue; // APY too low for a comp pick

                compPicks.Add(new DraftPick
                {
                    Year = draftYear,
                    Round = round,
                    OriginalTeamId = team.Id,
                    CurrentTeamId = team.Id,
                    IsCompensatory = true,
                });
                picksAwarded++;
            }
        }

        return compPicks;
    }

    private static bool IsLostFA(TransactionRecord signing, string teamId,
        List<TransactionRecord> allTransactions, int year)
    {
        // Check if a player was previously on this team and left in FA
        // Look for a cut or contract expiry that preceded this signing
        if (signing.PlayerId == null) return false;

        return allTransactions.Any(t =>
            t.PlayerId == signing.PlayerId
            && t.TeamId == teamId
            && t.Year == year
            && (t.Type == TransactionType.Cut || t.Type == TransactionType.ContractExpired));
    }

    private static List<(Player Player, long APY)> CalculateNetLosses(
        List<(Player Player, long APY)> lost,
        List<(Player Player, long APY)> gained)
    {
        // Match lost players against gained players by value (highest first)
        var sortedLost = lost.OrderByDescending(x => x.APY).ToList();
        var sortedGained = gained.OrderByDescending(x => x.APY).ToList();

        var netLosses = new List<(Player Player, long APY)>();
        int gainedIdx = 0;

        foreach (var lostPlayer in sortedLost)
        {
            if (gainedIdx < sortedGained.Count)
            {
                // This loss is offset by a gain
                gainedIdx++;
            }
            else
            {
                // Uncompensated loss → eligible for comp pick
                netLosses.Add(lostPlayer);
            }
        }

        return netLosses;
    }

    private static int GetCompRound(long apy)
    {
        return apy switch
        {
            >= 1800000000L => 3,  // $18M+ APY → Round 3
            >= 1000000000L => 4,  // $10M+ APY → Round 4
            >= 500000000L => 5,   // $5M+ APY → Round 5
            >= 200000000L => 6,   // $2M+ APY → Round 6
            >= 100000000L => 7,   // $1M+ APY → Round 7
            _ => 8,               // Below threshold, no comp pick
        };
    }
}
