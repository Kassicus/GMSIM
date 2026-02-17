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

    private List<ScoutAssignment> _assignments = new();
    private int _scoutingBudget = 1500;

    // Scouting point costs by target tier
    private const int CostInitial = 10;       // → 25%
    private const int CostIntermediate = 25;   // → 50%
    private const int CostAdvanced = 50;       // → 75%
    private const int CostFull = 100;          // → 100%

    public int ScoutingBudget => _scoutingBudget;
    public IReadOnlyList<ScoutAssignment> Assignments => _assignments;

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
        _assignments.Clear();
        _scoutingBudget = 1500;
    }

    public (bool Success, string Message) AssignScout(string scoutId, string prospectId)
    {
        var scouts = _getScouts();
        var prospects = _getProspects();

        var scout = scouts.FirstOrDefault(s => s.Id == scoutId);
        if (scout == null) return (false, "Scout not found.");

        var prospect = prospects.FirstOrDefault(p => p.Id == prospectId);
        if (prospect == null) return (false, "Prospect not found.");

        // Check if scout is already assigned
        if (_assignments.Any(a => a.ScoutId == scoutId))
            return (false, $"{scout.Name} is already assigned to a prospect.");

        // Check if prospect is already being scouted
        if (_assignments.Any(a => a.ProspectId == prospectId))
            return (false, $"{prospect.FullName} is already being scouted.");

        // Determine cost based on next scouting tier
        int cost = GetNextTierCost(prospect);
        if (cost <= 0) return (false, $"{prospect.FullName} is already fully scouted.");
        if (_scoutingBudget < cost)
            return (false, $"Insufficient scouting budget. Need {cost}, have {_scoutingBudget}.");

        _scoutingBudget -= cost;

        _assignments.Add(new ScoutAssignment
        {
            ScoutId = scoutId,
            ProspectId = prospectId,
            WeeksAssigned = 0,
        });

        EventBus.Instance?.EmitSignal(EventBus.SignalName.ScoutAssigned, scoutId, prospectId);
        return (true, $"Assigned {scout.Name} to scout {prospect.FullName}. ({cost} points spent, {_scoutingBudget} remaining)");
    }

    public void UnassignScout(string scoutId)
    {
        _assignments.RemoveAll(a => a.ScoutId == scoutId);
    }

    public void ProcessScoutingWeek()
    {
        var scouts = _getScouts();
        var prospects = _getProspects();
        var rng = _getRng();

        var completedAssignments = new List<ScoutAssignment>();

        foreach (var assignment in _assignments)
        {
            var scout = scouts.FirstOrDefault(s => s.Id == assignment.ScoutId);
            var prospect = prospects.FirstOrDefault(p => p.Id == assignment.ProspectId);
            if (scout == null || prospect == null) continue;

            assignment.WeeksAssigned++;

            // Progress rate: scout Speed determines how fast scouting progresses
            // Speed 80 → ~15% per week, Speed 50 → ~8% per week
            float progressRate = scout.Speed * 0.002f;
            prospect.ScoutingProgress = Math.Min(1.0f, prospect.ScoutingProgress + progressRate);

            // Check for tier advancement
            var oldGrade = prospect.ScoutGrade;
            var newGrade = GetGradeFromProgress(prospect.ScoutingProgress);

            if (newGrade > oldGrade)
            {
                prospect.ScoutGrade = newGrade;
                RevealAttributes(prospect, newGrade, scout.Accuracy, rng);
                EventBus.Instance?.EmitSignal(EventBus.SignalName.ProspectScouted, prospect.Id, (int)newGrade);
            }

            // If fully scouted, unassign
            if (prospect.ScoutGrade == ScoutingGrade.FullyScouted)
                completedAssignments.Add(assignment);
        }

        foreach (var completed in completedAssignments)
            _assignments.Remove(completed);
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

    private int GetNextTierCost(Prospect prospect)
    {
        return prospect.ScoutGrade switch
        {
            ScoutingGrade.Unscouted => CostInitial,
            ScoutingGrade.Initial => CostIntermediate,
            ScoutingGrade.Intermediate => CostAdvanced,
            ScoutingGrade.Advanced => CostFull,
            ScoutingGrade.FullyScouted => 0,
            _ => 0,
        };
    }

    // --- Save/Load State ---

    public (List<ScoutAssignment> Assignments, int Budget) GetState()
    {
        return (_assignments, _scoutingBudget);
    }

    public void SetState(List<ScoutAssignment> assignments, int budget)
    {
        _assignments = assignments;
        _scoutingBudget = budget;
    }
}
