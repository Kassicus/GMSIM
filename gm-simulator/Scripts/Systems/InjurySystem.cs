using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class InjurySystem
{
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<string, Player?> _getPlayer;
    private readonly Func<Random> _getRng;

    private static readonly string[] MinorTypes = { "Hamstring", "Ankle Sprain", "Knee (minor)", "Shoulder", "Back Spasms", "Quadricep", "Calf" };
    private static readonly string[] ModerateTypes = { "Hamstring", "MCL Sprain", "Ankle", "Shoulder", "Concussion", "Rib", "Calf", "Groin" };
    private static readonly string[] SevereTypes = { "ACL Tear", "MCL Tear", "Broken Bone", "Torn Labrum", "Achilles", "Lisfranc", "High Ankle Sprain" };
    private static readonly string[] SeasonEndingTypes = { "ACL Tear", "Achilles Rupture", "Broken Leg", "Torn Patellar Tendon", "Neck", "Fractured Vertebra" };

    public InjurySystem(
        Func<List<Player>> getPlayers,
        Func<string, Player?> getPlayer,
        Func<Random> getRng)
    {
        _getPlayers = getPlayers;
        _getPlayer = getPlayer;
        _getRng = getRng;
    }

    /// <summary>
    /// Processes injuries for a game. Returns injury events without mutating player state.
    /// </summary>
    public List<GameInjuryEvent> ProcessGameInjuries(List<Player> homePlayers, List<Player> awayPlayers)
    {
        var rng = _getRng();
        var injuries = new List<GameInjuryEvent>();

        foreach (var player in homePlayers.Concat(awayPlayers))
        {
            if (player.CurrentInjury != null) continue; // already injured

            if (ShouldPlayerGetInjured(player, rng))
            {
                injuries.Add(GenerateInjury(player, rng));
            }
        }

        return injuries;
    }

    /// <summary>
    /// Applies injuries from a GameResult to actual Player objects.
    /// </summary>
    public void ApplyInjuries(GameResult result, int season, int week)
    {
        foreach (var injury in result.Injuries)
        {
            var player = _getPlayer(injury.PlayerId);
            if (player == null) continue;

            player.CurrentInjury = new Injury
            {
                PlayerId = injury.PlayerId,
                InjuryType = injury.InjuryType,
                Severity = injury.Severity,
                WeeksRemaining = injury.WeeksOut,
                WeeksTotal = injury.WeeksOut,
                GameWeekInjured = week,
                SeasonInjured = season,
                CanReturn = injury.CanReturn,
            };

            EventBus.Instance?.EmitSignal(
                EventBus.SignalName.PlayerInjured,
                injury.PlayerId, injury.InjuryType, injury.WeeksOut);
        }
    }

    /// <summary>
    /// Decrements WeeksRemaining on all active injuries.
    /// Clears injuries that have healed.
    /// </summary>
    public void TickInjuries()
    {
        foreach (var player in _getPlayers())
        {
            if (player.CurrentInjury == null) continue;

            player.CurrentInjury.WeeksRemaining--;

            if (player.CurrentInjury.WeeksRemaining <= 0 && player.CurrentInjury.CanReturn)
            {
                player.CurrentInjury = null;
            }
        }
    }

    private bool ShouldPlayerGetInjured(Player player, Random rng)
    {
        float baseRate = GetBaseInjuryRate(player.Position);

        // InjuryResistance modifier: 99 = ~50% less, 50 = normal, 1 = ~50% more
        float resistMod = 1.0f - (player.Attributes.InjuryResistance - 50f) / 100f;

        // Trait modifiers
        if (player.Traits.IronMan) resistMod *= 0.50f;
        if (player.Traits.GlassBody) resistMod *= 1.50f;

        // Fatigue modifier
        float fatigueMod = 1.0f + player.Fatigue * 0.005f;

        float finalRate = baseRate * resistMod * fatigueMod;

        return rng.NextDouble() < finalRate;
    }

    private GameInjuryEvent GenerateInjury(Player player, Random rng)
    {
        var (type, severity, weeks, canReturn) = RollInjuryDetails(rng);

        return new GameInjuryEvent
        {
            PlayerId = player.Id,
            InjuryType = type,
            Severity = severity,
            WeeksOut = weeks,
            CanReturn = canReturn,
        };
    }

    private static float GetBaseInjuryRate(Position position) => position switch
    {
        Position.QB => 0.035f,
        Position.HB => 0.050f,
        Position.WR => 0.035f,
        Position.TE => 0.035f,
        Position.LT or Position.LG or Position.C or Position.RG or Position.RT => 0.030f,
        Position.DT or Position.EDGE => 0.035f,
        Position.MLB or Position.OLB => 0.040f,
        Position.CB or Position.FS or Position.SS => 0.035f,
        Position.K or Position.P or Position.LS => 0.005f,
        _ => 0.030f,
    };

    private static (string type, string severity, int weeks, bool canReturn) RollInjuryDetails(Random rng)
    {
        float sevRoll = (float)rng.NextDouble();
        string severity;
        int minWeeks, maxWeeks;
        bool canReturn = true;

        if (sevRoll < 0.45f) { severity = "Minor"; minWeeks = 1; maxWeeks = 2; }
        else if (sevRoll < 0.75f) { severity = "Moderate"; minWeeks = 3; maxWeeks = 6; }
        else if (sevRoll < 0.92f) { severity = "Severe"; minWeeks = 6; maxWeeks = 16; }
        else { severity = "Season-Ending"; minWeeks = 16; maxWeeks = 52; canReturn = false; }

        int weeks = rng.Next(minWeeks, maxWeeks + 1);

        string[] pool = severity switch
        {
            "Minor" => MinorTypes,
            "Moderate" => ModerateTypes,
            "Severe" => SevereTypes,
            _ => SeasonEndingTypes,
        };

        string type = pool[rng.Next(pool.Length)];
        return (type, severity, weeks, canReturn);
    }
}
