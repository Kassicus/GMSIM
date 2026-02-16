using System.Text.Json;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class PlayerGenerator
{
    private string[] _firstNames = Array.Empty<string>();
    private string[] _lastNames = Array.Empty<string>();
    private CollegeEntry[] _colleges = Array.Empty<CollegeEntry>();
    private Dictionary<string, ArchetypeTemplate> _archetypes = new();
    private int _totalCollegeWeight;

    private record CollegeEntry(string Name, int Weight);
    private record ArchetypeTemplate(string Position, Dictionary<string, int[]> BaseAttributes);

    // Position -> list of valid archetypes
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

    // Height/weight ranges by position [minHeight, maxHeight (inches), minWeight, maxWeight (lbs)]
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

    // Roster composition: how many of each position per team (active 53)
    private static readonly Dictionary<Position, int> RosterComposition = new()
    {
        { Position.QB, 3 }, { Position.HB, 4 }, { Position.FB, 1 },
        { Position.WR, 6 }, { Position.TE, 3 },
        { Position.LT, 2 }, { Position.LG, 2 }, { Position.C, 2 },
        { Position.RG, 2 }, { Position.RT, 2 },
        { Position.EDGE, 4 }, { Position.DT, 3 },
        { Position.MLB, 2 }, { Position.OLB, 3 },
        { Position.CB, 5 }, { Position.FS, 2 }, { Position.SS, 2 },
        { Position.K, 1 }, { Position.P, 1 }, { Position.LS, 1 },
    };
    // total = 3+4+1+6+3+2+2+2+2+2+4+3+2+3+5+2+2+1+1+1 = 51, we add 2 flex

    // Practice squad composition (16 total)
    private static readonly Dictionary<Position, int> PracticeSquadComposition = new()
    {
        { Position.QB, 1 }, { Position.HB, 1 }, { Position.WR, 3 },
        { Position.TE, 1 }, { Position.LG, 1 }, { Position.RT, 1 },
        { Position.EDGE, 2 }, { Position.DT, 1 }, { Position.OLB, 1 },
        { Position.CB, 2 }, { Position.SS, 1 }, { Position.MLB, 1 },
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

    public Player GeneratePlayer(Position position, int targetOverall, int age, int currentYear, Random rng)
    {
        var archetype = PickArchetype(position, rng);
        var attributes = GenerateAttributes(position, archetype, targetOverall, rng);
        var traits = GenerateTraits(rng);
        var (height, weight) = GeneratePhysicals(position, rng);

        int actual = OverallCalculator.Calculate(position, attributes);
        // Nudge attributes to get closer to target
        NudgeToTarget(position, attributes, targetOverall, rng);
        actual = OverallCalculator.Calculate(position, attributes);

        var devTrait = PickDevTrait(actual, rng);
        int yearsInLeague = Math.Max(0, age - 22 + rng.Next(-1, 2));
        int potential = Math.Min(99, actual + rng.Next(0, 15));

        return new Player
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = _firstNames[rng.Next(_firstNames.Length)],
            LastName = _lastNames[rng.Next(_lastNames.Length)],
            Age = age,
            YearsInLeague = yearsInLeague,
            College = PickCollege(rng),
            DraftYear = currentYear - yearsInLeague,
            DraftRound = yearsInLeague > 0 ? rng.Next(1, 8) : 0,
            DraftPick = rng.Next(1, 33),
            IsUndrafted = rng.NextDouble() < 0.15,
            HeightInches = height,
            WeightLbs = weight,
            Position = position,
            Archetype = archetype,
            Overall = actual,
            PotentialCeiling = potential,
            Attributes = attributes,
            Traits = traits,
            RosterStatus = RosterStatus.Active53,
            Morale = 60 + rng.Next(30),
            Fatigue = rng.Next(20),
            DevTrait = devTrait,
            TrajectoryModifier = rng.Next(-3, 4),
            CareerStats = new Dictionary<int, SeasonStats>(),
        };
    }

    public List<Player> GenerateRoster(Team team, int currentYear, Random rng)
    {
        var players = new List<Player>();

        // Generate active roster (53)
        foreach (var (position, count) in RosterComposition)
        {
            for (int i = 0; i < count; i++)
            {
                int targetOvr = GetTargetOverall(i, count, rng);
                int age = GetRandomAge(position, rng);
                var player = GeneratePlayer(position, targetOvr, age, currentYear, rng);
                player.TeamId = team.Id;
                player.RosterStatus = RosterStatus.Active53;

                // Generate contract
                player.CurrentContract = ContractGenerator.GenerateVeteranContract(player, currentYear, rng);
                player.CurrentContract.TeamId = team.Id;

                players.Add(player);
                team.PlayerIds.Add(player.Id);
            }
        }

        // Add 2 flex players to reach 53
        var flexPositions = new[] { Position.WR, Position.CB };
        foreach (var pos in flexPositions)
        {
            int targetOvr = 60 + rng.Next(15);
            int age = 23 + rng.Next(5);
            var player = GeneratePlayer(pos, targetOvr, age, currentYear, rng);
            player.TeamId = team.Id;
            player.RosterStatus = RosterStatus.Active53;
            player.CurrentContract = ContractGenerator.GenerateVeteranContract(player, currentYear, rng);
            player.CurrentContract.TeamId = team.Id;
            players.Add(player);
            team.PlayerIds.Add(player.Id);
        }

        // Generate practice squad (16)
        foreach (var (position, count) in PracticeSquadComposition)
        {
            for (int i = 0; i < count; i++)
            {
                int targetOvr = 50 + rng.Next(18);
                int age = 22 + rng.Next(4);
                var player = GeneratePlayer(position, targetOvr, age, currentYear, rng);
                player.TeamId = team.Id;
                player.RosterStatus = RosterStatus.PracticeSquad;
                player.CurrentContract = ContractGenerator.GeneratePracticeSquadContract(currentYear, player.Id, team.Id);
                players.Add(player);
                team.PracticeSquadIds.Add(player.Id);
            }
        }

        return players;
    }

    public void SetupDepthChart(Team team, List<Player> teamPlayers)
    {
        team.DepthChart = new DepthChart();

        foreach (Position pos in Enum.GetValues<Position>())
        {
            var posPlayers = teamPlayers
                .Where(p => p.Position == pos && p.RosterStatus == RosterStatus.Active53)
                .OrderByDescending(p => p.Overall)
                .Select(p => p.Id)
                .ToList();

            if (posPlayers.Count > 0)
                team.DepthChart.Chart[pos] = posPlayers;
        }
    }

    public void RecalculateTeamCap(Team team, List<Player> allPlayers, int currentYear)
    {
        long totalCapUsed = 0;
        foreach (var playerId in team.PlayerIds.Concat(team.PracticeSquadIds))
        {
            var player = allPlayers.FirstOrDefault(p => p.Id == playerId);
            if (player?.CurrentContract != null)
            {
                totalCapUsed += player.CurrentContract.GetCapHit(currentYear);
            }
        }
        team.CurrentCapUsed = totalCapUsed;
    }

    private Archetype PickArchetype(Position position, Random rng)
    {
        if (PositionArchetypes.TryGetValue(position, out var archetypes))
            return archetypes[rng.Next(archetypes.Length)];
        return Archetype.Balanced;
    }

    private ArchetypeTemplate? FindArchetypeTemplate(string archetypeName, Position position)
    {
        // Try exact match first
        if (_archetypes.TryGetValue(archetypeName, out var template))
            return template;

        // Try with position suffix (e.g., "ManCoverageCB", "AccurateK")
        string posName = position.ToString();
        if (_archetypes.TryGetValue(archetypeName + posName, out template))
            return template;

        // Try with position group suffix (e.g., "HybridSafety")
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

    private PlayerAttributes GenerateAttributes(Position position, Archetype archetype, int targetOverall, Random rng)
    {
        var attrs = new PlayerAttributes();
        string archetypeName = archetype.ToString();

        // Try multiple key formats to match archetype templates
        // The JSON may use "ManCoverageCB", "AccurateK", "HybridSafety" etc.
        var template = FindArchetypeTemplate(archetypeName, position);
        if (template != null)
        {
            SetAttributeFromTemplate(attrs, template.BaseAttributes, rng);
        }
        else
        {
            // Fallback: generate based on position with reasonable defaults
            SetDefaultAttributes(attrs, position, rng);
        }

        return attrs;
    }

    private void SetAttributeFromTemplate(PlayerAttributes attrs, Dictionary<string, int[]> template, Random rng)
    {
        // Set defaults first
        foreach (var prop in typeof(PlayerAttributes).GetProperties())
        {
            if (prop.PropertyType == typeof(int))
                prop.SetValue(attrs, 30 + rng.Next(25)); // 30-54 default
        }

        // Override with template values
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
        // Base everything on 40-65 range with position-specific bumps
        foreach (var prop in typeof(PlayerAttributes).GetProperties())
        {
            if (prop.PropertyType == typeof(int))
                prop.SetValue(attrs, 35 + rng.Next(30));
        }

        // Apply position-specific boosts
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
            default:
                break;
        }
    }

    private void NudgeToTarget(Position position, PlayerAttributes attrs, int target, Random rng)
    {
        // Iteratively nudge attributes to get overall closer to target
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

    private DevelopmentTrait PickDevTrait(int overall, Random rng)
    {
        double roll = rng.NextDouble();

        // Higher overall = higher chance of better dev trait
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

    private int GetTargetOverall(int depthIndex, int totalAtPosition, Random rng)
    {
        // Starters (index 0) get higher overalls, backups get lower
        if (depthIndex == 0)
            return 72 + rng.Next(22); // 72-93 for starters
        if (depthIndex == 1)
            return 62 + rng.Next(18); // 62-79 for primary backups
        return 52 + rng.Next(18); // 52-69 for depth
    }

    private int GetRandomAge(Position position, Random rng)
    {
        // Weighted age distribution: 40% 22-25, 35% 26-29, 20% 30-33, 5% 34+
        double roll = rng.NextDouble();
        if (roll < 0.40) return 22 + rng.Next(4);
        if (roll < 0.75) return 26 + rng.Next(4);
        if (roll < 0.95) return 30 + rng.Next(4);
        return 34 + rng.Next(4);
    }
}
