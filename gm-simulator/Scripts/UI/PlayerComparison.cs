using System.Reflection;
using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.UI.Components;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class PlayerComparison : Window
{
    private OptionButton _player1Select = null!;
    private OptionButton _player2Select = null!;
    private VBoxContainer _content = null!;

    private List<Player> _allPlayers = new();
    private static readonly PropertyInfo[] AttrProps = typeof(PlayerAttributes)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(int))
        .ToArray();

    public override void _Ready()
    {
        _player1Select = GetNode<OptionButton>("MarginContainer/VBox/SelectionBar/Player1Select");
        _player2Select = GetNode<OptionButton>("MarginContainer/VBox/SelectionBar/Player2Select");
        _content = GetNode<VBoxContainer>("MarginContainer/VBox/ScrollContainer/ComparisonContent");

        CloseRequested += QueueFree;

        PopulatePlayerDropdowns();

        _player1Select.ItemSelected += _ => RefreshComparison();
        _player2Select.ItemSelected += _ => RefreshComparison();
    }

    public void Initialize(string? preselectedPlayerId = null)
    {
        if (preselectedPlayerId == null) return;

        // Will be applied after _Ready populates dropdowns
        CallDeferred(MethodName.SelectPlayer, preselectedPlayerId);
    }

    private void SelectPlayer(string playerId)
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            if (_allPlayers[i].Id == playerId)
            {
                _player1Select.Selected = i;
                RefreshComparison();
                return;
            }
        }
    }

    private void PopulatePlayerDropdowns()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        _allPlayers = gm.Players
            .Where(p => p.TeamId != null)
            .OrderBy(p => p.Position)
            .ThenByDescending(p => p.Overall)
            .ToList();

        foreach (var p in _allPlayers)
        {
            string entry = $"{p.Position} {p.FirstName} {p.LastName} ({p.Overall} OVR)";
            _player1Select.AddItem(entry);
            _player2Select.AddItem(entry);
        }

        if (_allPlayers.Count >= 2)
            _player2Select.Selected = 1;
    }

    private void RefreshComparison()
    {
        foreach (var child in _content.GetChildren())
            child.QueueFree();

        if (_allPlayers.Count < 2) return;

        var p1 = _allPlayers[_player1Select.Selected];
        var p2 = _allPlayers[_player2Select.Selected];
        var gm = GameManager.Instance!;

        // Identity section
        AddSectionHeader("IDENTITY");
        AddComparisonRow("Name", p1.FullName, p2.FullName);
        AddComparisonRow("Position", p1.Position.ToString(), p2.Position.ToString());
        var t1 = gm.GetTeam(p1.TeamId ?? "");
        var t2 = gm.GetTeam(p2.TeamId ?? "");
        AddComparisonRow("Team", t1?.Abbreviation ?? "FA", t2?.Abbreviation ?? "FA");
        AddComparisonRow("Age", p1.Age.ToString(), p2.Age.ToString(), true, true);
        AddNumericRow("Overall", p1.Overall, p2.Overall);

        // Contract section
        AddSectionHeader("CONTRACT");
        if (p1.CurrentContract != null || p2.CurrentContract != null)
        {
            int year = gm.Calendar.CurrentYear;
            string cap1 = p1.CurrentContract != null ? GameShell.FormatCurrency(p1.CurrentContract.GetCapHit(year)) : "N/A";
            string cap2 = p2.CurrentContract != null ? GameShell.FormatCurrency(p2.CurrentContract.GetCapHit(year)) : "N/A";
            AddComparisonRow("Cap Hit", cap1, cap2);

            string yrs1 = p1.CurrentContract != null ? $"{p1.CurrentContract.TotalYears - (year - p1.CurrentContract.Years[0].Year)}yr" : "N/A";
            string yrs2 = p2.CurrentContract != null ? $"{p2.CurrentContract.TotalYears - (year - p2.CurrentContract.Years[0].Year)}yr" : "N/A";
            AddComparisonRow("Remaining", yrs1, yrs2);
        }
        else
        {
            AddComparisonRow("Status", "No Contract", "No Contract");
        }

        // Attributes section
        AddSectionHeader("ATTRIBUTES");
        foreach (var prop in AttrProps)
        {
            int v1 = (int)(prop.GetValue(p1.Attributes) ?? 0);
            int v2 = (int)(prop.GetValue(p2.Attributes) ?? 0);

            // Only show attributes where at least one player has a meaningful value
            if (v1 >= 15 || v2 >= 15)
                AddAttributeRow(FormatAttrName(prop.Name), v1, v2);
        }

        // Season stats (current year if available)
        int currentYear = gm.Calendar.CurrentYear;
        bool has1 = p1.CareerStats.ContainsKey(currentYear);
        bool has2 = p2.CareerStats.ContainsKey(currentYear);
        if (has1 || has2)
        {
            AddSectionHeader($"{currentYear} STATS");
            var s1 = has1 ? p1.CareerStats[currentYear] : null;
            var s2 = has2 ? p2.CareerStats[currentYear] : null;

            AddNumericRow("Games", s1?.GamesPlayed ?? 0, s2?.GamesPlayed ?? 0);

            // Show relevant stats based on positions
            if (IsPassingRelevant(p1.Position) || IsPassingRelevant(p2.Position))
            {
                AddNumericRow("Pass Yds", s1?.PassingYards ?? 0, s2?.PassingYards ?? 0);
                AddNumericRow("Pass TDs", s1?.PassingTDs ?? 0, s2?.PassingTDs ?? 0);
                AddNumericRow("INTs", s1?.Interceptions ?? 0, s2?.Interceptions ?? 0, true);
            }
            if (IsRushingRelevant(p1.Position) || IsRushingRelevant(p2.Position))
            {
                AddNumericRow("Rush Yds", s1?.RushingYards ?? 0, s2?.RushingYards ?? 0);
                AddNumericRow("Rush TDs", s1?.RushingTDs ?? 0, s2?.RushingTDs ?? 0);
            }
            if (IsReceivingRelevant(p1.Position) || IsReceivingRelevant(p2.Position))
            {
                AddNumericRow("Rec Yds", s1?.ReceivingYards ?? 0, s2?.ReceivingYards ?? 0);
                AddNumericRow("Rec TDs", s1?.ReceivingTDs ?? 0, s2?.ReceivingTDs ?? 0);
                AddNumericRow("Receptions", s1?.Receptions ?? 0, s2?.Receptions ?? 0);
            }
            if (IsDefenseRelevant(p1.Position) || IsDefenseRelevant(p2.Position))
            {
                AddNumericRow("Tackles", s1?.TotalTackles ?? 0, s2?.TotalTackles ?? 0);
                AddNumericRow("Sacks", (int)(s1?.Sacks ?? 0), (int)(s2?.Sacks ?? 0));
                AddNumericRow("INTs(D)", s1?.InterceptionsDef ?? 0, s2?.InterceptionsDef ?? 0);
            }
        }
    }

    private void AddSectionHeader(string title)
    {
        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", ThemeFonts.Subtitle);
        label.AddThemeColorOverride("font_color", ThemeColors.AccentText);
        _content.AddChild(label);
    }

    private void AddComparisonRow(string label, string val1, string val2, bool lowerIsBetter = false, bool numeric = false)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var v1Label = new Label
        {
            Text = val1,
            CustomMinimumSize = new Vector2(200, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        v1Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);

        var nameLabel = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(120, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        nameLabel.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);

        var v2Label = new Label
        {
            Text = val2,
            CustomMinimumSize = new Vector2(200, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        v2Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);

        row.AddChild(v1Label);
        row.AddChild(nameLabel);
        row.AddChild(v2Label);
        _content.AddChild(row);
    }

    private void AddNumericRow(string label, int val1, int val2, bool lowerIsBetter = false)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        Color win = ThemeColors.Success;
        Color lose = ThemeColors.Danger;
        Color neutral = ThemeColors.TextPrimary;

        Color c1 = neutral, c2 = neutral;
        if (val1 != val2)
        {
            bool v1Wins = lowerIsBetter ? val1 < val2 : val1 > val2;
            c1 = v1Wins ? win : lose;
            c2 = v1Wins ? lose : win;
        }

        var v1Label = new Label
        {
            Text = val1.ToString(),
            CustomMinimumSize = new Vector2(200, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        v1Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        v1Label.AddThemeColorOverride("font_color", c1);

        var nameLabel = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(120, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        nameLabel.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);

        var v2Label = new Label
        {
            Text = val2.ToString(),
            CustomMinimumSize = new Vector2(200, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        v2Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        v2Label.AddThemeColorOverride("font_color", c2);

        row.AddChild(v1Label);
        row.AddChild(nameLabel);
        row.AddChild(v2Label);
        _content.AddChild(row);
    }

    private void AddAttributeRow(string label, int val1, int val2)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        Color win = ThemeColors.Success;
        Color lose = ThemeColors.Danger;
        Color neutral = ThemeColors.TextPrimary;

        Color c1 = neutral, c2 = neutral;
        if (val1 != val2)
        {
            c1 = val1 > val2 ? win : lose;
            c2 = val2 > val1 ? win : lose;
        }

        // Value 1
        var v1Label = new Label
        {
            Text = val1.ToString(),
            CustomMinimumSize = new Vector2(40, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        v1Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        v1Label.AddThemeColorOverride("font_color", c1);

        // Bar 1 (right-aligned, grows left)
        var bar1Container = new Control { CustomMinimumSize = new Vector2(160, 16) };
        var bar1 = new ColorRect
        {
            Color = c1 with { A = 0.5f },
            Size = new Vector2(val1 * 1.6f, 14),
        };
        bar1.Position = new Vector2(160 - val1 * 1.6f, 1);
        bar1Container.AddChild(bar1);

        // Label
        var nameLabel = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(120, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        nameLabel.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);

        // Bar 2 (left-aligned, grows right)
        var bar2Container = new Control { CustomMinimumSize = new Vector2(160, 16) };
        var bar2 = new ColorRect
        {
            Color = c2 with { A = 0.5f },
            Size = new Vector2(val2 * 1.6f, 14),
            Position = new Vector2(0, 1),
        };
        bar2Container.AddChild(bar2);

        // Value 2
        var v2Label = new Label
        {
            Text = val2.ToString(),
            CustomMinimumSize = new Vector2(40, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        v2Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        v2Label.AddThemeColorOverride("font_color", c2);

        row.AddChild(v1Label);
        row.AddChild(bar1Container);
        row.AddChild(nameLabel);
        row.AddChild(bar2Container);
        row.AddChild(v2Label);
        _content.AddChild(row);
    }

    private static string FormatAttrName(string name)
    {
        // Insert spaces before capitals: "ThrowPower" → "Throw Power"
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append(' ');
            result.Append(c);
        }
        return result.ToString();
    }

    private static bool IsPassingRelevant(Pos p) => p == Pos.QB;
    private static bool IsRushingRelevant(Pos p) => p is Pos.QB or Pos.HB or Pos.FB;
    private static bool IsReceivingRelevant(Pos p) => p is Pos.WR or Pos.TE or Pos.HB;
    private static bool IsDefenseRelevant(Pos p) => p is Pos.EDGE or Pos.DT or Pos.MLB or Pos.OLB or Pos.CB or Pos.FS or Pos.SS;
}
