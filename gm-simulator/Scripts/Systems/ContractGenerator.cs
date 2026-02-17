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

    public static long GetMarketValue(Player player)
    {
        // APY in cents based on overall and position.
        // Calibrated against real 2024 NFL data ($255.4M cap):
        //   Top QB: ~$55M, Top EDGE: ~$34M, Top WR: ~$35M
        //   Average starter: ~$8-15M, Backup: ~$2-4M, Depth: minimum
        // See: PFF highest-paid players 2024, Spotrac average salaries
        long baseAPY = player.Overall switch
        {
            >= 97 => 3500000000L,  // Elite (top 1-2 at position): $35M base
            >= 93 => 2400000000L,  // All-Pro caliber: $24M base
            >= 90 => 1800000000L,  // Pro Bowl level: $18M base
            >= 85 => 1200000000L,  // Quality starter: $12M base
            >= 80 => 700000000L,   // Average starter: $7M base
            >= 75 => 400000000L,   // Low-end starter: $4M base
            >= 70 => 200000000L,   // Backup: $2M base
            _ => 0,                // Depth: minimum salary (handled below)
        };

        // Sub-70 OVR veterans just get minimum salary
        if (baseAPY == 0)
            return GetMinimumSalary(player.YearsInLeague);

        float positionMultiplier = GetPositionPayMultiplier(player.Position);
        long apy = (long)(baseAPY * positionMultiplier);

        // Floor: never pay less than minimum salary
        long minimum = GetMinimumSalary(player.YearsInLeague);
        return Math.Max(apy, minimum);
    }

    public static float GetPositionPayMultiplier(Position pos)
    {
        // Calibrated against real 2024 NFL top-of-market data:
        //   QB #1: $55M → 1.6x of $35M base
        //   EDGE #1: $34M → ~1.0x
        //   WR #1: $35M → ~1.0x
        //   OT #1: $28M → ~0.8x
        //   DT #1: $32M → ~0.9x
        //   CB #1: $21M → ~0.6x (but top CBs are 90+ OVR, so $18M*0.6=$10.8M... no)
        // Actually position multipliers need to reflect the PAY PREMIUM at each position:
        return pos switch
        {
            Position.QB => 1.55f,      // QBs are paid far above all others
            Position.EDGE => 1.0f,     // Top EDGE ~$34M aligns with base
            Position.WR => 1.0f,       // Top WR ~$35M aligns with base
            Position.DT => 0.90f,      // Top DT ~$32M
            Position.LT => 0.85f,      // Top OT ~$28M
            Position.CB => 0.80f,      // Top CB ~$21M
            Position.FS or Position.SS => 0.70f,  // Top S ~$21M
            Position.MLB => 0.65f,     // Top LB ~$20M
            Position.OLB => 0.60f,     // Mid-tier LB
            Position.RT => 0.75f,      // Slightly less than LT
            Position.LG or Position.RG => 0.65f, // Top OG ~$21M
            Position.C => 0.55f,       // Top C ~$13.5M
            Position.TE => 0.55f,      // Top TE ~$17M
            Position.HB => 0.55f,      // Top RB ~$19M (but most RBs underpaid)
            Position.FB => 0.15f,      // FBs are minimum or near-minimum
            Position.K or Position.P => 0.20f,   // Specialists ~$5-7M at top
            Position.LS => 0.08f,      // Long snappers: ~$1-2M max
            _ => 0.50f,
        };
    }

    public static Contract GenerateFranchiseTagContract(Player player, long tagValue, int year)
    {
        return new Contract
        {
            PlayerId = player.Id,
            TeamId = player.TeamId ?? string.Empty,
            TotalYears = 1,
            TotalValue = tagValue,
            TotalGuaranteed = tagValue,
            Type = ContractType.Veteran,
            Years = new List<ContractYear>
            {
                new()
                {
                    Year = year,
                    YearNumber = 1,
                    BaseSalary = tagValue,
                    CapHit = tagValue,
                    DeadCap = tagValue,
                    Guaranteed = tagValue,
                }
            }
        };
    }

    public static Contract GenerateFromOffer(FreeAgentOffer offer, int startYear)
    {
        int years = offer.Years;
        long signingBonus = offer.SigningBonus;
        int prorationYears = Math.Min(years, 5);
        long proratedBonus = prorationYears > 0 ? signingBonus / prorationYears : 0;
        long remainingValue = offer.TotalValue - signingBonus;
        long guaranteedRemaining = offer.GuaranteedMoney - signingBonus;

        var contract = new Contract
        {
            PlayerId = offer.PlayerId,
            TeamId = offer.TeamId,
            TotalYears = years,
            TotalValue = offer.TotalValue,
            TotalGuaranteed = offer.GuaranteedMoney,
            Type = ContractType.Veteran,
            Years = new List<ContractYear>()
        };

        for (int i = 0; i < years; i++)
        {
            // Escalating base salary (~5% per year)
            float escalation = 1.0f + (0.05f * i);
            float totalEscalation = 0;
            for (int j = 0; j < years; j++)
                totalEscalation += 1.0f + (0.05f * j);

            long yearBase = (long)(remainingValue * (escalation / totalEscalation));
            if (yearBase < 79500000) yearBase = 79500000; // minimum floor

            // Front-load guarantees: first 2 years get guaranteed base
            long yearGuaranteed = i < 2 && guaranteedRemaining > 0
                ? Math.Min(yearBase + (i == 0 ? signingBonus : 0), guaranteedRemaining)
                : 0;
            if (i < 2) guaranteedRemaining -= yearGuaranteed;

            long yearDeadCap = i < prorationYears ? proratedBonus * (prorationYears - i) : 0;

            contract.Years.Add(new ContractYear
            {
                Year = startYear + i,
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

    public static Contract GenerateExtensionContract(Player player, int currentYear, int additionalYears, long totalValue, long guaranteed)
    {
        var existing = player.CurrentContract;
        int remainingYears = existing?.Years.Count(y => y.Year >= currentYear) ?? 0;
        int totalYears = remainingYears + additionalYears;

        long signingBonus = (long)(totalValue * 0.15);
        int prorationYears = Math.Min(totalYears, 5);
        long proratedBonus = prorationYears > 0 ? signingBonus / prorationYears : 0;
        long remainingValue = totalValue - signingBonus;
        long guaranteedRemaining = guaranteed - signingBonus;

        var contract = new Contract
        {
            PlayerId = player.Id,
            TeamId = player.TeamId ?? string.Empty,
            TotalYears = totalYears,
            TotalValue = totalValue,
            TotalGuaranteed = guaranteed,
            Type = ContractType.Extension,
            Years = new List<ContractYear>()
        };

        for (int i = 0; i < totalYears; i++)
        {
            float escalation = 1.0f + (0.05f * i);
            float totalEscalation = 0;
            for (int j = 0; j < totalYears; j++)
                totalEscalation += 1.0f + (0.05f * j);

            long yearBase = (long)(remainingValue * (escalation / totalEscalation));
            if (yearBase < 79500000) yearBase = 79500000;

            long yearGuaranteed = i < 3 && guaranteedRemaining > 0
                ? Math.Min(yearBase + (i == 0 ? signingBonus : 0), guaranteedRemaining)
                : 0;
            if (i < 3) guaranteedRemaining -= yearGuaranteed;

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
