using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public static class ContractGenerator
{
    public static Contract GenerateRookieContract(int draftRound, int draftPick, int year, string playerId, string teamId)
    {
        int totalYears = draftRound == 0 ? 3 : 4; // UDFA = 3 years, drafted = 4
        long totalValue = GetRookieSlotValue(draftRound, draftPick);
        long guaranteed = draftRound == 1 ? totalValue : (long)(totalValue * 0.55);

        var contract = new Contract
        {
            PlayerId = playerId,
            TeamId = teamId,
            TotalYears = totalYears,
            TotalValue = totalValue,
            TotalGuaranteed = guaranteed,
            Type = draftRound == 0 ? ContractType.UDFA : ContractType.Rookie,
            Years = new List<ContractYear>()
        };

        long annualBase = totalValue / totalYears;
        long signingBonus = draftRound == 1 ? (long)(totalValue * 0.25) : (long)(totalValue * 0.10);
        long proratedBonus = signingBonus / Math.Min(totalYears, 5);

        for (int i = 0; i < totalYears; i++)
        {
            long yearBase = annualBase + (long)(annualBase * 0.05 * i); // slight annual escalation
            contract.Years.Add(new ContractYear
            {
                Year = year + i,
                YearNumber = i + 1,
                BaseSalary = yearBase,
                SigningBonus = i == 0 ? signingBonus : 0,
                CapHit = yearBase + proratedBonus,
                DeadCap = proratedBonus * (totalYears - i),
                Guaranteed = i < 2 ? yearBase + (i == 0 ? signingBonus : 0) : 0,
            });
        }

        return contract;
    }

    public static Contract GenerateVeteranContract(Player player, int currentYear, Random rng)
    {
        int years = GetTypicalContractLength(player, rng);
        long apy = GetMarketValue(player);
        long totalValue = apy * years;
        float guaranteedPct = GetGuaranteedPercentage(player, years);
        long totalGuaranteed = (long)(totalValue * guaranteedPct);

        var contract = new Contract
        {
            PlayerId = player.Id,
            TeamId = player.TeamId ?? string.Empty,
            TotalYears = years,
            TotalValue = totalValue,
            TotalGuaranteed = totalGuaranteed,
            Type = ContractType.Veteran,
            Years = new List<ContractYear>()
        };

        long signingBonus = (long)(totalValue * 0.15);
        int prorationYears = Math.Min(years, 5);
        long proratedBonus = signingBonus / prorationYears;

        for (int i = 0; i < years; i++)
        {
            // Escalating base salary
            float escalation = 1.0f + (0.08f * i);
            long yearBase = (long)(apy * escalation) - proratedBonus;
            if (yearBase < 0) yearBase = 79500000; // minimum

            long yearGuaranteed = i < 2 ? yearBase + (i == 0 ? signingBonus : 0) : 0;
            long yearDeadCap = i < prorationYears ? proratedBonus * (prorationYears - i) : 0;

            contract.Years.Add(new ContractYear
            {
                Year = currentYear + i,
                YearNumber = i + 1,
                BaseSalary = yearBase,
                SigningBonus = i == 0 ? signingBonus : 0,
                CapHit = yearBase + proratedBonus,
                DeadCap = yearDeadCap,
                Guaranteed = yearGuaranteed,
            });
        }

        return contract;
    }

    public static Contract GenerateMinimumContract(int yearsInLeague, int year, string playerId, string teamId)
    {
        long salary = GetMinimumSalary(yearsInLeague);

        var contract = new Contract
        {
            PlayerId = playerId,
            TeamId = teamId,
            TotalYears = 1,
            TotalValue = salary,
            TotalGuaranteed = 0,
            Type = ContractType.MinimumSalary,
            Years = new List<ContractYear>
            {
                new()
                {
                    Year = year,
                    YearNumber = 1,
                    BaseSalary = salary,
                    CapHit = salary,
                    DeadCap = 0,
                }
            }
        };

        return contract;
    }

    public static Contract GeneratePracticeSquadContract(int year, string playerId, string teamId)
    {
        long salary = 126000000; // $1.26M in cents

        return new Contract
        {
            PlayerId = playerId,
            TeamId = teamId,
            TotalYears = 1,
            TotalValue = salary,
            TotalGuaranteed = 0,
            Type = ContractType.PracticeSquad,
            Years = new List<ContractYear>
            {
                new()
                {
                    Year = year,
                    YearNumber = 1,
                    BaseSalary = salary,
                    CapHit = salary,
                    DeadCap = 0,
                }
            }
        };
    }

    private static long GetRookieSlotValue(int round, int pick)
    {
        // Approximate slot values (4-year total, in cents)
        return round switch
        {
            1 => pick switch
            {
                <= 5 => 3500000000L + (long)((5 - pick) * 150000000L),
                <= 15 => 2000000000L + (long)((15 - pick) * 50000000L),
                <= 32 => 1500000000L + (long)((32 - pick) * 30000000L),
                _ => 1500000000L,
            },
            2 => 700000000L + (long)(Math.Max(0, 32 - pick) * 10000000L),
            3 => 500000000L + (long)(Math.Max(0, 32 - pick) * 5000000L),
            4 => 420000000L,
            5 => 380000000L,
            6 => 350000000L,
            7 => 330000000L,
            _ => 320000000L, // UDFA
        };
    }

    private static long GetMarketValue(Player player)
    {
        // APY in cents based on overall and position
        float positionMultiplier = GetPositionPayMultiplier(player.Position);
        long baseAPY = player.Overall switch
        {
            >= 95 => 4500000000L,
            >= 90 => 3200000000L,
            >= 85 => 2200000000L,
            >= 80 => 1400000000L,
            >= 75 => 900000000L,
            >= 70 => 550000000L,
            >= 65 => 350000000L,
            _ => 200000000L,
        };

        return (long)(baseAPY * positionMultiplier);
    }

    private static float GetPositionPayMultiplier(Position pos)
    {
        return pos switch
        {
            Position.QB => 1.5f,
            Position.EDGE => 1.1f,
            Position.CB => 1.05f,
            Position.WR => 1.05f,
            Position.LT => 1.05f,
            Position.DT => 1.0f,
            Position.FS or Position.SS => 0.95f,
            Position.MLB => 0.95f,
            Position.TE => 0.90f,
            Position.HB => 0.85f,
            Position.RT => 0.95f,
            Position.LG or Position.RG or Position.C => 0.90f,
            Position.OLB => 0.90f,
            Position.K or Position.P => 0.40f,
            Position.FB => 0.35f,
            Position.LS => 0.20f,
            _ => 0.80f,
        };
    }

    private static int GetTypicalContractLength(Player player, Random rng)
    {
        if (player.Age >= 33) return rng.Next(1, 3);
        if (player.Age >= 30) return rng.Next(1, 4);
        if (player.Overall >= 85) return rng.Next(3, 6);
        if (player.Overall >= 75) return rng.Next(2, 5);
        return rng.Next(1, 4);
    }

    private static float GetGuaranteedPercentage(Player player, int years)
    {
        if (player.Overall >= 90) return 0.65f;
        if (player.Overall >= 85) return 0.55f;
        if (player.Overall >= 80) return 0.45f;
        if (player.Overall >= 75) return 0.35f;
        return years <= 2 ? 0.30f : 0.20f;
    }

    private static long GetMinimumSalary(int yearsOfService)
    {
        return yearsOfService switch
        {
            0 => 79500000,
            1 => 91500000,
            2 => 95500000,
            3 => 98500000,
            4 => 101500000,
            5 => 104500000,
            6 => 107500000,
            _ => 112500000,
        };
    }
}
