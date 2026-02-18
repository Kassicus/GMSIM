using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class ProspectCard : Window
{
    private Label _nameLabel = null!;
    private Label _infoLabel = null!;
    private Label _gradeLabel = null!;
    private ProgressBar _gradeBar = null!;
    private Label _projectedLabel = null!;
    private Label _combineLabel = null!;
    private GridContainer _attrsGrid = null!;
    private Label _strengthsLabel = null!;
    private Label _redFlagsLabel = null!;
    private Button _assignScoutBtn = null!;

    private string _prospectId = "";
    private Prospect? _prospect;

    public void Initialize(string prospectId)
    {
        _prospectId = prospectId;
    }

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/NameLabel");
        _infoLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/InfoLabel");
        _gradeLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/GradeHBox/GradeLabel");
        _gradeBar = GetNode<ProgressBar>("MarginContainer/ScrollContainer/VBox/GradeHBox/GradeBar");
        _projectedLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/ProjectedLabel");
        _combineLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/CombineLabel");
        _attrsGrid = GetNode<GridContainer>("MarginContainer/ScrollContainer/VBox/AttrsGrid");
        _strengthsLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/StrengthsLabel");
        _redFlagsLabel = GetNode<Label>("MarginContainer/ScrollContainer/VBox/RedFlagsLabel");
        _assignScoutBtn = GetNode<Button>("MarginContainer/ScrollContainer/VBox/ButtonHBox/AssignScoutBtn");

        _assignScoutBtn.Pressed += OnScoutPressed;

        Populate();
    }

    private void Populate()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        _prospect = gm.CurrentDraftClass.FirstOrDefault(p => p.Id == _prospectId);
        if (_prospect == null) return;

        _nameLabel.Text = _prospect.FullName;
        _infoLabel.Text = $"{_prospect.Position} | {_prospect.College} | Age {_prospect.Age} | {GameShell.FormatHeight(_prospect.HeightInches)} {_prospect.WeightLbs} lbs";

        _gradeLabel.Text = $"Scouting: {_prospect.ScoutGrade}";
        _gradeBar.Value = _prospect.ScoutingProgress * 100f;
        _gradeBar.Modulate = ThemeColors.GetScoutGradeColor(_prospect.ScoutGrade);

        if (_prospect.ScoutGrade == ScoutingGrade.FullyScouted)
        {
            string talentText = _prospect.TalentRound <= 7 ? $"Round {_prospect.TalentRound}" : "UDFA";
            _projectedLabel.Text = $"Projected: Round {_prospect.ProjectedRound} | True Talent: {talentText}";

            if (_prospect.TalentRound < _prospect.ProjectedRound)
                _projectedLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
            else if (_prospect.TalentRound > _prospect.ProjectedRound)
                _projectedLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
        }
        else
        {
            _projectedLabel.Text = $"Projected: Round {_prospect.ProjectedRound}";
            _projectedLabel.RemoveThemeColorOverride("font_color");
        }

        PopulateCombine();
        PopulateAttributes();
        PopulateStrengthsWeaknesses();

        // Update scout button state
        bool canScout = _prospect.ScoutGrade != ScoutingGrade.FullyScouted
            && gm.Scouting.CurrentPoints >= Systems.ScoutingSystem.CostPerAction;
        _assignScoutBtn.Visible = _prospect.ScoutGrade != ScoutingGrade.FullyScouted;
        _assignScoutBtn.Disabled = !canScout;
        _assignScoutBtn.Text = canScout
            ? $"Scout ({Systems.ScoutingSystem.CostPerAction} pts)"
            : _prospect.ScoutGrade == ScoutingGrade.FullyScouted ? "Fully Scouted" : "No Points";
    }

    private void PopulateCombine()
    {
        if (_prospect == null) return;

        if (_prospect.CombineResults == null)
        {
            _combineLabel.Text = _prospect.AttendedCombine ? "Combine data not available." : "Did not attend combine.";
            return;
        }

        var c = _prospect.CombineResults;
        var parts = new List<string>();
        if (c.FortyYardDash.HasValue) parts.Add($"40-yd: {c.FortyYardDash:F2}s");
        if (c.BenchPress.HasValue) parts.Add($"Bench: {c.BenchPress} reps");
        if (c.VerticalJump.HasValue) parts.Add($"Vert: {c.VerticalJump:F1}\"");
        if (c.BroadJump.HasValue) parts.Add($"Broad: {c.BroadJump:F0}\"");
        if (c.ThreeConeDrill.HasValue) parts.Add($"3-Cone: {c.ThreeConeDrill:F2}s");
        if (c.ShuttleRun.HasValue) parts.Add($"Shuttle: {c.ShuttleRun:F2}s");
        if (c.WonderlicScore.HasValue) parts.Add($"Wonderlic: {c.WonderlicScore}");

        _combineLabel.Text = parts.Count > 0 ? string.Join(" | ", parts) : "No results recorded.";
    }

    private void PopulateAttributes()
    {
        if (_prospect == null) return;

        // Clear existing
        foreach (var child in _attrsGrid.GetChildren())
            child.QueueFree();

        if (_prospect.ScoutGrade == ScoutingGrade.Unscouted)
        {
            AddAttrRow("Not yet scouted", "");
            return;
        }

        var scouted = _prospect.ScoutedAttributes;
        if (scouted == null) return;

        var props = typeof(PlayerAttributes).GetProperties();
        foreach (var prop in props)
        {
            if (prop.PropertyType != typeof(int)) continue;
            int val = (int)prop.GetValue(scouted)!;
            if (val == 0) continue; // Not yet revealed

            AddAttrRow(prop.Name, val.ToString());
        }
    }

    private void AddAttrRow(string name, string value)
    {
        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        _attrsGrid.AddChild(nameLabel);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        if (int.TryParse(value, out int v))
        {
            valLabel.AddThemeColorOverride("font_color", ThemeColors.GetRatingColor(v));
        }
        _attrsGrid.AddChild(valLabel);
    }

    private void PopulateStrengthsWeaknesses()
    {
        if (_prospect == null) return;

        if (_prospect.ScoutGrade < ScoutingGrade.Intermediate)
        {
            _strengthsLabel.Text = "Scout more to reveal strengths/weaknesses.";
            _redFlagsLabel.Text = "";
            return;
        }

        var lines = new List<string>();
        if (_prospect.Strengths.Count > 0)
            lines.Add("Strengths: " + string.Join(", ", _prospect.Strengths));
        if (_prospect.Weaknesses.Count > 0)
            lines.Add("Weaknesses: " + string.Join(", ", _prospect.Weaknesses));

        _strengthsLabel.Text = lines.Count > 0 ? string.Join("\n", lines) : "None identified.";

        if (_prospect.ScoutGrade >= ScoutingGrade.Advanced && _prospect.RedFlags.Count > 0)
        {
            _redFlagsLabel.Text = "RED FLAGS: " + string.Join(", ", _prospect.RedFlags);
            _redFlagsLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
        }
        else
        {
            _redFlagsLabel.Text = "";
        }
    }

    private void OnScoutPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null || _prospect == null) return;

        var result = gm.Scouting.ScoutProspect(_prospect.Id);
        _assignScoutBtn.Text = result.Message;

        // Re-populate to show newly revealed attributes
        Populate();
    }

    private void OnClosePressed() => QueueFree();
}
