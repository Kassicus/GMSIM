using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Components;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class DepthChartView : Control
{
    private static readonly Pos[] OffensePositions =
        { Pos.QB, Pos.HB, Pos.FB, Pos.WR, Pos.TE, Pos.LT, Pos.LG, Pos.C, Pos.RG, Pos.RT };
    private static readonly Pos[] DefensePositions =
        { Pos.EDGE, Pos.DT, Pos.MLB, Pos.OLB, Pos.CB, Pos.FS, Pos.SS };
    private static readonly Pos[] STPositions =
        { Pos.K, Pos.P, Pos.LS };

    private HBoxContainer _offenseGrid = null!;
    private HBoxContainer _defenseGrid = null!;
    private HBoxContainer _stGrid = null!;
    private Button _autoSetButton = null!;

    public override void _Ready()
    {
        var vbox = "ScrollContainer/MarginContainer/VBox";
        _offenseGrid = GetNode<HBoxContainer>($"{vbox}/OffenseGrid");
        _defenseGrid = GetNode<HBoxContainer>($"{vbox}/DefenseGrid");
        _stGrid = GetNode<HBoxContainer>($"{vbox}/STGrid");
        _autoSetButton = GetNode<Button>($"{vbox}/HeaderHBox/AutoSetButton");

        _autoSetButton.Pressed += OnAutoSetPressed;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.DepthChartChanged += OnDepthChartChanged;
            EventBus.Instance.PlayerCut += OnRosterChanged;
            EventBus.Instance.PlayerSigned += OnRosterChanged;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.DepthChartChanged -= OnDepthChartChanged;
            EventBus.Instance.PlayerCut -= OnRosterChanged;
            EventBus.Instance.PlayerSigned -= OnRosterChanged;
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        BuildPositionColumns(_offenseGrid, OffensePositions, team, gm);
        BuildPositionColumns(_defenseGrid, DefensePositions, team, gm);
        BuildPositionColumns(_stGrid, STPositions, team, gm);
    }

    private void BuildPositionColumns(HBoxContainer grid, Pos[] positions, Team team, GameManager gm)
    {
        foreach (var child in grid.GetChildren())
            child.QueueFree();

        foreach (var pos in positions)
        {
            var column = new VBoxContainer();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 4);

            // Position header
            var header = new Label
            {
                Text = pos.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            header.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            header.Modulate = ThemeColors.AccentText;
            column.AddChild(header);

            // Separator
            var sep = new HSeparator();
            column.AddChild(sep);

            // Depth slots
            List<string> playerIds = new();
            if (team.DepthChart.Chart.TryGetValue(pos, out var depthList))
                playerIds = depthList;

            int maxSlots = GetMaxDepthSlots(pos);
            for (int i = 0; i < maxSlots; i++)
            {
                var slot = CreateDepthSlot(pos, i, playerIds, gm);
                column.AddChild(slot);
            }

            grid.AddChild(column);
        }
    }

    private Control CreateDepthSlot(Pos pos, int depthIndex, List<string> playerIds, GameManager gm)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(110, 60);

        var vbox = new VBoxContainer();
        vbox.Alignment = BoxContainer.AlignmentMode.Center;

        var depthLabel = new Label
        {
            Text = GetDepthLabel(depthIndex),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        depthLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Caption);
        depthLabel.Modulate = ThemeColors.TextTertiary;
        vbox.AddChild(depthLabel);

        if (depthIndex < playerIds.Count)
        {
            string playerId = playerIds[depthIndex];
            var player = gm.GetPlayer(playerId);

            if (player != null)
            {
                var nameLabel = new Label
                {
                    Text = TruncateName(player.FullName, 12),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
                vbox.AddChild(nameLabel);

                var ovrLabel = new Label
                {
                    Text = player.Overall.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                ovrLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
                ovrLabel.Modulate = OverallBadge.GetOverallColor(player.Overall);
                vbox.AddChild(ovrLabel);

                // Make clickable
                var button = new Button();
                button.Flat = true;
                button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                button.Modulate = new Color(1, 1, 1, 0); // Invisible overlay
                string capturedId = playerId;
                button.Pressed += () => EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSelected, capturedId);
                panel.AddChild(button);

                // Move up/down buttons for reordering
                if (depthIndex > 0)
                {
                    var upBtn = new Button { Text = "^", CustomMinimumSize = new Vector2(20, 16) };
                    upBtn.AddThemeFontSizeOverride("font_size", ThemeFonts.Caption);
                    int capturedIdx = depthIndex;
                    Pos capturedPos = pos;
                    upBtn.Pressed += () => OnSwapDepth(capturedPos, capturedIdx, capturedIdx - 1);
                    vbox.AddChild(upBtn);
                }
            }
        }
        else
        {
            var emptyLabel = new Label
            {
                Text = "(Empty)",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            emptyLabel.Modulate = ThemeColors.TextPlaceholder;
            vbox.AddChild(emptyLabel);
        }

        panel.AddChild(vbox);
        return panel;
    }

    private static string GetDepthLabel(int index) => index switch
    {
        0 => "1st",
        1 => "2nd",
        2 => "3rd",
        _ => $"{index + 1}th"
    };

    private static int GetMaxDepthSlots(Pos pos) => pos switch
    {
        Pos.QB => 3,
        Pos.HB or Pos.WR or Pos.CB => 4,
        Pos.K or Pos.P or Pos.LS => 1,
        _ => 3
    };

    private static string TruncateName(string name, int max)
    {
        if (name.Length <= max) return name;
        // Try last name only
        var parts = name.Split(' ');
        if (parts.Length >= 2)
        {
            string abbreviated = $"{parts[0][0]}. {parts[^1]}";
            if (abbreviated.Length <= max) return abbreviated;
            return parts[^1].Length <= max ? parts[^1] : parts[^1][..max];
        }
        return name[..max];
    }

    private void OnSwapDepth(Pos position, int indexA, int indexB)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        var (success, message) = gm.RosterManager.SwapDepthChart(team, position, indexA, indexB);
        if (!success)
            GD.Print($"Swap failed: {message}");
        // Refresh triggered by DepthChartChanged signal
    }

    private void OnAutoSetPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        var activePlayers = gm.GetTeamActivePlayers(team.Id);
        gm.RosterManager.AutoSetDepthChart(team, activePlayers);
        // Refresh triggered by DepthChartChanged signal
    }

    private void OnDepthChartChanged(string teamId)
    {
        var gm = GameManager.Instance;
        if (gm != null && teamId == gm.PlayerTeamId)
            Refresh();
    }

    private void OnRosterChanged(string playerId, string teamId)
    {
        var gm = GameManager.Instance;
        if (gm != null && teamId == gm.PlayerTeamId)
            Refresh();
    }
}
