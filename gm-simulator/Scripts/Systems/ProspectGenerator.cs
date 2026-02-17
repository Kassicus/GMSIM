using System.Text.Json;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class ProspectGenerator
{
    private string[] _firstNames = Array.Empty<string>();
    private string[] _lastNames = Array.Empty<string>();
    private CollegeEntry[] _colleges = Array.Empty<CollegeEntry>();
    private Dictionary<string, ArchetypeTemplate> _archetypes = new();
    private int _totalCollegeWeight;

    private record CollegeEntry(string Name, int Weight);
    private record ArchetypeTemplate(string Position, Dictionary<string, int[]> BaseAttributes);

    // Position -> valid archetypes (mirrors PlayerGenerator)
    private static readonly Dictionary<Position, Archetype[]> PositionArchetypes = new()
    {
        { Position.QB, new[] { Archetype.PocketPasser, Archetype.Scrambler, Archetype.FieldGeneral } },
        { Position.HB, new[] { Archetype.PowerBack, Archetype.SpeedBack, Archetype.ElusiveBack, Archetype.ReceivingBack } },
        { Position.FB, new[] { Archetype.Balanced } },
        { Position.WR, new[] { Archetype.DeepThreat, Archetype.PossessionReceiver, Archetype.SlotReceiver, Archetype.RouteRunner } },
        { Position.TE, new[] { Archetype.BlockingTE, Archetype.ReceivingTE, Archetype.Versatile } },
        { Position.LT, new[] { Archetype.PassProtector, Archetype.RunBlocker, Archetype.Balanced } },
        { Position.LG, new[] { Archetype.PassProtector, Archetype.RunBlocker, Archetype.Balanced } },
        { Position.C, new[] { Archetype.PassProtector, Archetype.RunBlocker, Archetype.Balanced } },
        { Position.RG, new[] { Archetype.PassProtector, Archetype.RunBlocker, Archetype.Balanced } },
        { Position.RT, new[] { Archetype.PassProtector, Archetype.RunBlocker, Archetype.Balanced } },
        { Position.EDGE, new[] { Archetype.SpeedRusher, Archetype.PowerRusher, Archetype.RunStopper } },
        { Position.DT, new[] { Archetype.NoseTackle, Archetype.PassRushDT, Archetype.ThreeDown } },
        { Position.MLB, new[] { Archetype.RunStuffer, Archetype.CoverageLB, Archetype.Blitzer } },
        { Position.OLB, new[] { Archetype.RunStuffer, Archetype.CoverageLB, Archetype.Blitzer } },
        { Position.CB, new[] { Archetype.ManCoverage, Archetype.ZoneCoverage, Archetype.SlotCorner } },
        { Position.FS, new[] { Archetype.CenterFielder, Archetype.BoxSafety, Archetype.Hybrid } },
        { Position.SS, new[] { Archetype.CenterFielder, Archetype.BoxSafety, Archetype.Hybrid } },
        { Position.K, new[] { Archetype.Accurate, Archetype.BigLeg } },
        { Position.P, new[] { Archetype.Accurate, Archetype.BigLeg } },
        { Position.LS, new[] { Archetype.Standard } },
    };

    // Height/weight ranges by position [minH, maxH inches, minW, maxW lbs]
    private static readonly Dictionary<Position, int[]> PhysicalRanges = new()
    {
        { Position.QB, new[] { 72, 78, 205, 245 } },
        { Position.HB, new[] { 67, 73, 185, 235 } },
        { Position.FB, new[] { 70, 74, 235, 260 } },
        { Position.WR, new[] { 68, 77, 170, 220 } },
        { Position.TE, new[] { 74, 79, 235, 270 } },
        { Position.LT, new[] { 76, 80, 295, 340 } },
        { Position.LG, new[] { 74, 78, 295, 340 } },
        { Position.C, new[] { 73, 77, 290, 320 } },
        { Position.RG, new[] { 74, 78, 295, 340 } },
        { Position.RT, new[] { 76, 80, 295, 340 } },
        { Position.EDGE, new[] { 74, 79, 240, 275 } },
        { Position.DT, new[] { 73, 78, 280, 340 } },
        { Position.MLB, new[] { 72, 76, 225, 260 } },
        { Position.OLB, new[] { 73, 77, 225, 255 } },
        { Position.CB, new[] { 69, 75, 175, 210 } },
        { Position.FS, new[] { 70, 75, 190, 215 } },
        { Position.SS, new[] { 70, 75, 195, 220 } },
        { Position.K, new[] { 69, 75, 180, 215 } },
        { Position.P, new[] { 71, 77, 195, 225 } },
        { Position.LS, new[] { 72, 76, 230, 260 } },
    };

    // How many prospects per position in a draft class (total ~270)
    private static readonly Dictionary<Position, int> ProspectDistribution = new()
    {
        { Position.QB, 12 }, { Position.HB, 18 }, { Position.FB, 4 },
        { Position.WR, 24 }, { Position.TE, 12 },
        { Position.LT, 12 }, { Position.LG, 10 }, { Position.C, 8 },
        { Position.RG, 10 }, { Position.RT, 12 },
        { Position.EDGE, 20 }, { Position.DT, 16 },
        { Position.MLB, 12 }, { Position.OLB, 14 },
        { Position.CB, 22 }, { Position.FS, 10 }, { Position.SS, 10 },
        { Position.K, 6 }, { Position.P, 6 }, { Position.LS, 4 },
    }; // total = 262

    private static readonly string[] StrengthPool = {
        "Elite arm strength", "Explosive first step", "Natural ball hawk",
        "Outstanding route runner", "Elite speed", "Exceptional hands",
        "Powerful run blocker", "Anchor against bull rush", "Fluid hips",
        "Quick twitch athlete", "Excellent motor", "Field general",
        "Versatile defender", "Strong tackler", "Good ball skills",
        "Natural leader", "Consistent performer", "Quick processor",
        "Accurate deep ball", "Good in space", "Reliable hands",
        "Strong at point of attack", "Good lateral movement", "Great technique",
    };

    private static readonly string[] WeaknessPool = {
        "Inconsistent accuracy", "Needs to add bulk", "Takes bad angles",
        "Slow processing", "Limited burst", "Struggles in traffic",
        "Stiff hips", "Below average awareness", "Poor run defense",
        "Lacks elite speed", "Questionable durability", "Limited range",
        "Inconsistent technique", "Needs to improve footwork", "Poor in zone coverage",
        "Below average ball tracking", "Tends to freelance", "Slow to react",
        "Struggles against power", "Limited football IQ",
    };

    private static readonly string[] RedFlagPool = {
        "Character concerns", "Injury history", "Failed drug test",
        "Off-field incidents", "Work ethic questions", "Maturity issues",
        "Medical red flag (shoulder)", "Medical red flag (knee)",
        "Suspension history", "Inconsistent effort",
    };

    public void LoadData(string dataPath)
    {
        var firstNamesJson = File.ReadAllText(Path.Combine(dataPath, "firstnames.json"));
        _firstNames = JsonSerializer.Deserialize<string[]>(firstNamesJson) ?? Array.Empty<string>();

        var lastNamesJson = File.ReadAllText(Path.Combine(dataPath, "lastnames.json"));
        _lastNames = JsonSerializer.Deserialize<string[]>(lastNamesJson) ?? Array.Empty<string>();

        var collegesJson = File.ReadAllText(Path.Combine(dataPath, "colleges.json"));
        var rawColleges = JsonSerializer.Deserialize<JsonElement[]>(collegesJson) ?? Array.Empty<JsonElement>();
        _colleges = rawColleges.Select(c => new CollegeEntry(
            c.GetProperty("name").GetString() ?? "Unknown",
            c.GetProperty("weight").GetInt32()
        )).ToArray();
        _totalCollegeWeight = _colleges.Sum(c => c.Weight);

        var archetypesJson = File.ReadAllText(Path.Combine(dataPath, "archetypes.json"));
        var rawArchetypes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(archetypesJson)
            ?? new Dictionary<string, JsonElement>();

        foreach (var (key, value) in rawArchetypes)
        {
            var position = value.GetProperty("position").GetString() ?? "";
            var baseAttrs = new Dictionary<string, int[]>();

            if (value.TryGetProperty("baseAttributes", out var attrsElement))
            {
                foreach (var prop in attrsElement.EnumerateObject())
                {
                    var arr = prop.Value.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    if (arr.Length == 2)
                        baseAttrs[prop.Name] = arr;
                }
            }

            _archetypes[key] = new ArchetypeTemplate(position, baseAttrs);
        }
    }

    public List<Prospect> GenerateDraftClass(int year, Random rng)
    {
        var prospects = new List<Prospect>();

        foreach (var (position, count) in ProspectDistribution)
        {
            for (int i = 0; i < count; i++)
            {
                var prospect = GenerateProspect(position, year, rng);
                prospects.Add(prospect);
            }
        }

        // Sort by DraftValue descending, assign projected rounds
        prospects.Sort((a, b) => b.DraftValue.CompareTo(a.DraftValue));
        for (int i = 0; i < prospects.Count; i++)
        {
            prospects[i].ProjectedRound = i switch
            {
                < 32 => 1,
                < 64 => 2,
                < 100 => 3,
                < 135 => 4,
                < 175 => 5,
                < 215 => 6,
                _ => 7,
            };
        }

        return prospects;
    }

    private Prospect GenerateProspect(Position position, int year, Random rng)
    {
        var archetype = PickArchetype(position, rng);
        int targetOverall = GenerateTargetOverall(rng);
        var attributes = GenerateAttributes(position, archetype, targetOverall, rng);
        NudgeToTarget(position, attributes, targetOverall, rng);
        int actual = OverallCalculator.Calculate(position, attributes);

        var traits = GenerateTraits(rng);
        var devTrait = PickDevTrait(actual, rng);
        int potential = Math.Min(99, actual + rng.Next(5, 20));
        var (height, weight) = GeneratePhysicals(position, rng);

        int age = 20 + rng.Next(4); // 20-23

        var prospect = new Prospect
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = _firstNames[rng.Next(_firstNames.Length)],
            LastName = _lastNames[rng.Next(_lastNames.Length)],
            Age = age,
            College = PickCollege(rng),
            Position = position,
            Archetype = archetype,
            HeightInches = height,
            WeightLbs = weight,
            TrueAttributes = attributes,
            TruePotential = potential,
            TrueDevTrait = devTrait,
            TrueTraits = traits,
            ScoutGrade = ScoutingGrade.Unscouted,
            ScoutingProgress = 0f,
        };

        // Combine / Pro Day
        bool attendsCombine = rng.NextDouble() < 0.70;
        prospect.AttendedCombine = attendsCombine;
        if (attendsCombine)
        {
            prospect.CombineResults = GenerateCombineResults(prospect, rng);
        }
        else
        {
            prospect.HadProDay = rng.NextDouble() < 0.85; // Most non-combine guys have pro day
            if (prospect.HadProDay)
                prospect.CombineResults = GenerateCombineResults(prospect, rng);
        }

        // Strengths/Weaknesses/RedFlags
        prospect.Strengths = PickRandom(StrengthPool, 2 + rng.Next(2), rng);
        prospect.Weaknesses = PickRandom(WeaknessPool, 1 + rng.Next(2), rng);
        if (rng.NextDouble() < 0.12)
            prospect.RedFlags = PickRandom(RedFlagPool, 1 + rng.Next(2), rng);

        // DraftValue: based on overall, potential, position value
        float posValue = GetPositionDraftValue(position);
        prospect.DraftValue = actual * 1.5f + potential * 0.5f + posValue + rng.Next(-5, 6);

        return prospect;
    }

    private CombineResults GenerateCombineResults(Prospect prospect, Random rng)
    {
        var attrs = prospect.TrueAttributes;
        var pos = prospect.Position;

        // Base 40-time from speed attribute: speed 99 → ~4.25s, speed 60 → ~4.65s
        float base40 = 5.10f - (attrs.Speed * 0.009f);
        float forty = base40 + (float)(rng.NextDouble() * 0.08 - 0.04); // ±0.04

        // Bench press: strength-based, position-adjusted
        int baseBench = (int)(attrs.Strength * 0.3f) + 5;
        if (pos is Position.LT or Position.LG or Position.C or Position.RG or Position.RT or Position.DT)
            baseBench += 8;
        int bench = baseBench + rng.Next(-3, 4);

        // Vertical: based on jumping + speed
        float baseVert = 20f + (attrs.Jumping * 0.16f) + (attrs.Speed * 0.04f);
        float vert = baseVert + (float)(rng.NextDouble() * 4 - 2);

        // Broad jump: similar
        float baseBroad = 90f + (attrs.Jumping * 0.25f) + (attrs.Speed * 0.08f);
        float broad = baseBroad + (float)(rng.NextDouble() * 8 - 4);

        // 3-cone: agility-based
        float base3Cone = 7.80f - (attrs.Agility * 0.012f);
        float threeCone = base3Cone + (float)(rng.NextDouble() * 0.10 - 0.05);

        // Shuttle: agility + speed
        float baseShuttle = 4.80f - (attrs.Agility * 0.008f) - (attrs.Speed * 0.003f);
        float shuttle = baseShuttle + (float)(rng.NextDouble() * 0.08 - 0.04);

        // Wonderlic: loosely correlated with awareness
        int wonderlic = Math.Clamp(15 + (int)(attrs.Awareness * 0.25f) + rng.Next(-8, 9), 5, 50);

        var results = new CombineResults
        {
            FortyYardDash = MathF.Round(forty, 2),
            BenchPress = Math.Max(0, bench),
            VerticalJump = MathF.Round(vert, 1),
            BroadJump = MathF.Round(broad, 0),
            ThreeConeDrill = MathF.Round(threeCone, 2),
            ShuttleRun = MathF.Round(shuttle, 2),
            WonderlicScore = wonderlic,
        };

        // Some combine participants skip certain drills
        if (rng.NextDouble() < 0.10) results.FortyYardDash = null;
        if (rng.NextDouble() < 0.15) results.BenchPress = null;
        if (rng.NextDouble() < 0.10) results.ThreeConeDrill = null;
        if (rng.NextDouble() < 0.10) results.ShuttleRun = null;

        return results;
    }

    private int GenerateTargetOverall(Random rng)
    {
        // Bell curve centered on 68, range 55-95
        // Use box-muller approximation: sum of 3 randoms
        double sum = rng.NextDouble() + rng.NextDouble() + rng.NextDouble();
        double normalized = (sum / 3.0 - 0.5) * 2.0; // -1 to 1
        int overall = 68 + (int)(normalized * 20);
        return Math.Clamp(overall, 55, 95);
    }

    private float GetPositionDraftValue(Position pos)
    {
        return pos switch
        {
            Position.QB => 15f,
            Position.EDGE => 10f,
            Position.LT => 8f,
            Position.CB => 8f,
            Position.WR => 7f,
            Position.DT => 6f,
            Position.RT => 5f,
            Position.TE => 5f,
            Position.MLB => 5f,
            Position.OLB => 4f,
            Position.FS => 4f,
            Position.SS => 4f,
            Position.HB => 4f,
            Position.LG => 3f,
            Position.RG => 3f,
            Position.C => 3f,
            Position.K => 0f,
            Position.P => 0f,
            Position.FB => 0f,
            Position.LS => -5f,
            _ => 0f,
        };
    }

    // --- Shared generation helpers (mirrors PlayerGenerator) ---

    private Archetype PickArchetype(Position position, Random rng)
    {
        if (PositionArchetypes.TryGetValue(position, out var archetypes))
            return archetypes[rng.Next(archetypes.Length)];
        return Archetype.Balanced;
    }

    private PlayerAttributes GenerateAttributes(Position position, Archetype archetype, int targetOverall, Random rng)
    {
        var attrs = new PlayerAttributes();
        string archetypeName = archetype.ToString();

        var template = FindArchetypeTemplate(archetypeName, position);
        if (template != null)
            SetAttributeFromTemplate(attrs, template.BaseAttributes, rng);
        else
            SetDefaultAttributes(attrs, position, rng);

        return attrs;
    }

    private ArchetypeTemplate? FindArchetypeTemplate(string archetypeName, Position position)
    {
        if (_archetypes.TryGetValue(archetypeName, out var template))
            return template;

        string posName = position.ToString();
        if (_archetypes.TryGetValue(archetypeName + posName, out template))
            return template;

        string groupSuffix = position switch
        {
            Position.FS or Position.SS => "Safety",
            Position.CB => "CB",
            Position.K => "K",
            Position.P => "P",
            Position.LS => "LS",
            _ => ""
        };
        if (!string.IsNullOrEmpty(groupSuffix) && _archetypes.TryGetValue(archetypeName + groupSuffix, out template))
            return template;

        return null;
    }

    private void SetAttributeFromTemplate(PlayerAttributes attrs, Dictionary<string, int[]> template, Random rng)
    {
        foreach (var prop in typeof(PlayerAttributes).GetProperties())
        {
            if (prop.PropertyType == typeof(int))
                prop.SetValue(attrs, 30 + rng.Next(25));
        }

        foreach (var (attrName, range) in template)
        {
            var prop = typeof(PlayerAttributes).GetProperty(attrName);
            if (prop != null && range.Length == 2)
            {
                int value = rng.Next(range[0], range[1] + 1);
                prop.SetValue(attrs, Math.Clamp(value, 0, 99));
            }
        }
    }

    private void SetDefaultAttributes(PlayerAttributes attrs, Position position, Random rng)
    {
        foreach (var prop in typeof(PlayerAttributes).GetProperties())
        {
            if (prop.PropertyType == typeof(int))
                prop.SetValue(attrs, 35 + rng.Next(30));
        }

        switch (position)
        {
            case Position.QB:
                attrs.ThrowPower = 65 + rng.Next(30);
                attrs.ShortAccuracy = 60 + rng.Next(35);
                attrs.MediumAccuracy = 60 + rng.Next(35);
                attrs.DeepAccuracy = 55 + rng.Next(35);
                attrs.ThrowOnRun = 50 + rng.Next(40);
                attrs.Awareness = 55 + rng.Next(35);
                break;
            case Position.HB:
                attrs.Speed = 70 + rng.Next(25);
                attrs.Acceleration = 70 + rng.Next(25);
                attrs.Carrying = 65 + rng.Next(30);
                attrs.BallCarrierVision = 60 + rng.Next(35);
                attrs.Elusiveness = 55 + rng.Next(35);
                break;
            case Position.WR:
                attrs.Speed = 70 + rng.Next(25);
                attrs.Catching = 65 + rng.Next(30);
                attrs.RouteRunning = 60 + rng.Next(35);
                attrs.Release = 55 + rng.Next(35);
                break;
            case Position.EDGE:
                attrs.FinesseMoves = 60 + rng.Next(35);
                attrs.PowerMoves = 60 + rng.Next(35);
                attrs.BlockShedding = 60 + rng.Next(35);
                attrs.Speed = 65 + rng.Next(25);
                attrs.Pursuit = 60 + rng.Next(30);
                break;
            case Position.CB:
                attrs.ManCoverage = 60 + rng.Next(35);
                attrs.ZoneCoverage = 60 + rng.Next(35);
                attrs.Speed = 70 + rng.Next(25);
                attrs.Press = 55 + rng.Next(35);
                break;
        }
    }

    private void NudgeToTarget(Position position, PlayerAttributes attrs, int target, Random rng)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int current = OverallCalculator.Calculate(position, attrs);
            int diff = target - current;
            if (Math.Abs(diff) <= 2) break;

            float scale = 1.0f + (diff * 0.01f);
            foreach (var prop in typeof(PlayerAttributes).GetProperties())
            {
                if (prop.PropertyType != typeof(int)) continue;
                int val = (int)prop.GetValue(attrs)!;
                int newVal = (int)(val * scale) + rng.Next(-1, 2);
                prop.SetValue(attrs, Math.Clamp(newVal, 15, 99));
            }
        }
    }

    private PlayerTraits GenerateTraits(Random rng)
    {
        return new PlayerTraits
        {
            FightForYards = rng.NextDouble() < 0.3,
            HighMotor = rng.NextDouble() < 0.25,
            Clutch = rng.NextDouble() < 0.15,
            PenaltyProne = rng.NextDouble() < 0.10,
            BigGamePlayer = rng.NextDouble() < 0.12,
            TeamPlayer = rng.NextDouble() < 0.50,
            LockerRoomCancer = rng.NextDouble() < 0.03,
            IronMan = rng.NextDouble() < 0.08,
            GlassBody = rng.NextDouble() < 0.06,
            SenseOfPressure = (SenseOfPressure)rng.Next(3),
            ForcePasses = (ForcePasses)rng.Next(3),
            CoversBall = (CoversBall)rng.Next(3),
        };
    }

    private DevelopmentTrait PickDevTrait(int overall, Random rng)
    {
        double roll = rng.NextDouble();

        if (overall >= 90) return roll < 0.15 ? DevelopmentTrait.XFactor :
                                  roll < 0.45 ? DevelopmentTrait.Superstar :
                                  roll < 0.75 ? DevelopmentTrait.Star :
                                  DevelopmentTrait.Normal;

        if (overall >= 80) return roll < 0.05 ? DevelopmentTrait.XFactor :
                                  roll < 0.20 ? DevelopmentTrait.Superstar :
                                  roll < 0.50 ? DevelopmentTrait.Star :
                                  DevelopmentTrait.Normal;

        if (overall >= 70) return roll < 0.01 ? DevelopmentTrait.Superstar :
                                  roll < 0.15 ? DevelopmentTrait.Star :
                                  DevelopmentTrait.Normal;

        return roll < 0.05 ? DevelopmentTrait.Star : DevelopmentTrait.Normal;
    }

    private (int height, int weight) GeneratePhysicals(Position position, Random rng)
    {
        if (PhysicalRanges.TryGetValue(position, out var range))
        {
            int height = rng.Next(range[0], range[1] + 1);
            int weight = rng.Next(range[2], range[3] + 1);
            return (height, weight);
        }
        return (73, 220);
    }

    private string PickCollege(Random rng)
    {
        int roll = rng.Next(_totalCollegeWeight);
        int cumulative = 0;
        foreach (var college in _colleges)
        {
            cumulative += college.Weight;
            if (roll < cumulative)
                return college.Name;
        }
        return _colleges.Length > 0 ? _colleges[^1].Name : "Unknown";
    }

    private static List<string> PickRandom(string[] pool, int count, Random rng)
    {
        var shuffled = pool.OrderBy(_ => rng.Next()).Take(count).ToList();
        return shuffled;
    }
}
