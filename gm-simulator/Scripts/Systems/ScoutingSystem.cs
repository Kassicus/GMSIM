using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

public class ScoutingSystem
{
    private readonly Func<List<Prospect>> _getProspects;
    private readonly Func<List<Scout>> _getScouts;
    private readonly Func<Random> _getRng;
    private readonly Func<string> _getPlayerTeamId;

    private int _weeklyPointPool;
    private int _currentPoints;

    public const int CostPerAction = 25;

    public int WeeklyPointPool => _weeklyPointPool;
    public int CurrentPoints => _currentPoints;

    public ScoutingSystem(
        Func<List<Prospect>> getProspects,
        Func<List<Scout>> getScouts,
        Func<Random> getRng,
        Func<string> getPlayerTeamId)
    {
        _getProspects = getProspects;
        _getScouts = getScouts;
        _getRng = getRng;
        _getPlayerTeamId = getPlayerTeamId;
    }

    public void InitializeForDraftCycle()
    {
        CalculateWeeklyPoints();
        _currentPoints = _weeklyPointPool;
    }

    public void CalculateWeeklyPoints()
    {
        var scouts = _getScouts();
        _weeklyPointPool = scouts.Sum(s => (s.Accuracy + s.Speed) / 2);
    }

    public (bool Success, string Message) ScoutProspect(string prospectId)
    {
        var prospects = _getProspects();
        var scouts = _getScouts();
        var rng = _getRng();

        var prospect = prospects.FirstOrDefault(p => p.Id == prospectId);
        if (prospect == null) return (false, "Prospect not found.");

        if (prospect.ScoutGrade == ScoutingGrade.FullyScouted)
            return (false, $"{prospect.FullName} is already fully scouted.");

        if (_currentPoints < CostPerAction)
            return (false, $"Not enough scouting points. Need {CostPerAction}, have {_currentPoints}.");

        _currentPoints -= CostPerAction;

        // Advance exactly one tier
        float newProgress = prospect.ScoutGrade switch
        {
            ScoutingGrade.Unscouted => 0.25f,
            ScoutingGrade.Initial => 0.50f,
            ScoutingGrade.Intermediate => 0.75f,
            ScoutingGrade.Advanced => 1.0f,
            _ => prospect.ScoutingProgress,
        };
        prospect.ScoutingProgress = newProgress;

        var newGrade = GetGradeFromProgress(newProgress);
        prospect.ScoutGrade = newGrade;

        // Use average scout accuracy for attribute reveal
        int avgAccuracy = scouts.Count > 0
            ? (int)scouts.Average(s => s.Accuracy)
            : 70;
        RevealAttributes(prospect, newGrade, avgAccuracy, rng);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.ProspectScouted, prospect.Id, (int)newGrade);
        return (true, $"Scouted {prospect.FullName} to {newGrade}. ({_currentPoints} pts remaining)");
    }

    public void ProcessScoutingWeek()
    {
        CalculateWeeklyPoints();
        _currentPoints = _weeklyPointPool;
    }

    public void AutoScoutCombine()
    {
        var prospects = _getProspects();
        var rng = _getRng();

        foreach (var prospect in prospects)
        {
            if (!prospect.AttendedCombine && !prospect.HadProDay) continue;

            // Combine/Pro Day gives ~15% baseline scouting
            prospect.ScoutingProgress = Math.Max(prospect.ScoutingProgress, 0.15f);

            if (prospect.ScoutGrade < ScoutingGrade.Initial)
            {
                // Partial reveal: physical attrs only, with moderate accuracy (70)
                RevealPhysicalAttributes(prospect, 70, rng);
            }
        }
    }

    public ScoutingGrade GetGradeFromProgress(float progress)
    {
        return progress switch
        {
            >= 1.0f => ScoutingGrade.FullyScouted,
            >= 0.75f => ScoutingGrade.Advanced,
            >= 0.50f => ScoutingGrade.Intermediate,
            >= 0.25f => ScoutingGrade.Initial,
            _ => ScoutingGrade.Unscouted,
        };
    }

    private void RevealAttributes(Prospect prospect, ScoutingGrade grade, int scoutAccuracy, Random rng)
    {
        // Create or update scouted attributes with accuracy variance
        prospect.ScoutedAttributes ??= new PlayerAttributes();
        int errorRange = (100 - scoutAccuracy) / 10; // e.g., accuracy 80 → ±2

        var trueProps = typeof(PlayerAttributes).GetProperties();

        switch (grade)
        {
            case ScoutingGrade.Initial:
                // Reveal physical attributes: Speed, Strength, Agility, Acceleration, Jumping, Stamina
                RevealSpecificAttributes(prospect, new[] {
                    "Speed", "Strength", "Agility", "Acceleration", "Jumping", "Stamina"
                }, errorRange, rng);
                break;

            case ScoutingGrade.Intermediate:
                // Reveal primary position attributes + archetype hint
                RevealPositionAttributes(prospect, errorRange, rng);
                break;

            case ScoutingGrade.Advanced:
                // Reveal all attributes with accuracy variance
                foreach (var prop in trueProps)
                {
                    if (prop.PropertyType != typeof(int)) continue;
                    int trueVal = (int)prop.GetValue(prospect.TrueAttributes)!;
                    int scoutedVal = trueVal + rng.Next(-errorRange, errorRange + 1);
                    prop.SetValue(prospect.ScoutedAttributes, Math.Clamp(scoutedVal, 0, 99));
                }
                break;

            case ScoutingGrade.FullyScouted:
                // Reveal everything exactly (minimal error)
                int smallError = Math.Max(1, errorRange / 2);
                foreach (var prop in trueProps)
                {
                    if (prop.PropertyType != typeof(int)) continue;
                    int trueVal = (int)prop.GetValue(prospect.TrueAttributes)!;
                    int scoutedVal = trueVal + rng.Next(-smallError, smallError + 1);
                    prop.SetValue(prospect.ScoutedAttributes, Math.Clamp(scoutedVal, 0, 99));
                }
                prospect.ScoutedPotential = prospect.TruePotential + rng.Next(-smallError, smallError + 1);
                break;
        }
    }

    private void RevealSpecificAttributes(Prospect prospect, string[] attrNames, int errorRange, Random rng)
    {
        prospect.ScoutedAttributes ??= new PlayerAttributes();
        foreach (var name in attrNames)
        {
            var prop = typeof(PlayerAttributes).GetProperty(name);
            if (prop == null || prop.PropertyType != typeof(int)) continue;
            int trueVal = (int)prop.GetValue(prospect.TrueAttributes)!;
            int scoutedVal = trueVal + rng.Next(-errorRange, errorRange + 1);
            prop.SetValue(prospect.ScoutedAttributes, Math.Clamp(scoutedVal, 0, 99));
        }
    }

    private void RevealPositionAttributes(Prospect prospect, int errorRange, Random rng)
    {
        // First, include all physical attributes from Initial tier
        RevealSpecificAttributes(prospect, new[] {
            "Speed", "Strength", "Agility", "Acceleration", "Jumping", "Stamina"
        }, errorRange, rng);

        // Then position-specific primary attributes
        string[] posAttrs = prospect.Position switch
        {
            Position.QB => new[] { "ThrowPower", "ShortAccuracy", "MediumAccuracy", "DeepAccuracy", "ThrowOnRun", "Awareness" },
            Position.HB => new[] { "Carrying", "BallCarrierVision", "Elusiveness", "BreakTackle", "Catching" },
            Position.WR => new[] { "Catching", "RouteRunning", "Release", "CatchInTraffic", "SpectacularCatch" },
            Position.TE => new[] { "Catching", "RouteRunning", "RunBlock", "PassBlock", "BreakTackle" },
            Position.LT or Position.LG or Position.C or Position.RG or Position.RT =>
                new[] { "RunBlock", "PassBlock", "ImpactBlock", "Awareness", "Pulling" },
            Position.EDGE => new[] { "FinesseMoves", "PowerMoves", "BlockShedding", "Pursuit", "Tackle" },
            Position.DT => new[] { "BlockShedding", "PowerMoves", "Pursuit", "Tackle", "PlayRecognition" },
            Position.MLB or Position.OLB => new[] { "Tackle", "Pursuit", "PlayRecognition", "ManCoverage", "ZoneCoverage" },
            Position.CB => new[] { "ManCoverage", "ZoneCoverage", "Press", "PlayRecognition", "Catching" },
            Position.FS or Position.SS => new[] { "ZoneCoverage", "ManCoverage", "Tackle", "PlayRecognition", "HitPower" },
            Position.K or Position.P => new[] { "KickPower", "KickAccuracy", "Awareness" },
            _ => Array.Empty<string>(),
        };

        RevealSpecificAttributes(prospect, posAttrs, errorRange, rng);
    }

    private void RevealPhysicalAttributes(Prospect prospect, int scoutAccuracy, Random rng)
    {
        int errorRange = (100 - scoutAccuracy) / 10;
        RevealSpecificAttributes(prospect, new[] {
            "Speed", "Strength", "Agility", "Acceleration", "Jumping"
        }, errorRange, rng);
    }

    public void RecalculatePoints()
    {
        CalculateWeeklyPoints();
        _currentPoints = Math.Min(_currentPoints, _weeklyPointPool);
    }

    // --- Save/Load State ---

    public (int WeeklyPool, int CurrentPoints) GetState()
    {
        return (_weeklyPointPool, _currentPoints);
    }

    public void SetState(int weeklyPool, int currentPoints)
    {
        _weeklyPointPool = weeklyPool;
        _currentPoints = currentPoints;
    }
}
