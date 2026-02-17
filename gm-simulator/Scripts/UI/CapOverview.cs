using Godot;
using GMSimulator.Core;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class CapOverview : Control
{
    private Label _capTotalValue = null!;
    private Label _rolloverValue = null!;
    private Label _adjustedCapValue = null!;
    private Label _activeContractsValue = null!;
    private Label _deadCapValue = null!;
    private Label _totalCommittedValue = null!;
    private Label _availableSpaceValue = null!;
    private ProgressBar _capUsageBar = null!;
    private Label _capUsageLabel = null!;
    private VBoxContainer _topHitsList = null!;
    private GridContainer _projectionsGrid = null!;

    public override void _Ready()
    {
        var vbox = "ScrollContainer/MarginContainer/VBox";
        var grid = $"{vbox}/SummaryGrid";
        _capTotalValue = GetNode<Label>($"{grid}/CapTotalValue");
        _rolloverValue = GetNode<Label>($"{grid}/RolloverValue");
        _adjustedCapValue = GetNode<Label>($"{grid}/AdjustedCapValue");
        _activeContractsValue = GetNode<Label>($"{grid}/ActiveContractsValue");
        _deadCapValue = GetNode<Label>($"{grid}/DeadCapValue");
        _totalCommittedValue = GetNode<Label>($"{grid}/TotalCommittedValue");
        _availableSpaceValue = GetNode<Label>($"{grid}/AvailableSpaceValue");
        _capUsageBar = GetNode<ProgressBar>($"{vbox}/CapUsageBar");
        _capUsageLabel = GetNode<Label>($"{vbox}/CapUsageLabel");
        _topHitsList = GetNode<VBoxContainer>($"{vbox}/TopHitsList");
        _projectionsGrid = GetNode<GridContainer>($"{vbox}/ProjectionsGrid");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerCut += OnRosterChanged;
            EventBus.Instance.PlayerSigned += OnRosterChanged;
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerCut -= OnRosterChanged;
            EventBus.Instance.PlayerSigned -= OnRosterChanged;
            EventBus.Instance.WeekAdvanced -= OnWeekAdvanced;
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        int currentYear = gm.Calendar.CurrentYear;
        var capMgr = gm.SalaryCapManager;

        // Summary values
        long baseCap = capMgr.GetCapForYear(currentYear);
        long adjustedCap = capMgr.GetAdjustedCap(team, currentYear);
        long capUsed = team.CurrentCapUsed;
        long deadCap = team.DeadCapTotal;
        long totalCommitted = capUsed + deadCap;
        long available = team.CapSpace;

        _capTotalValue.Text = GameShell.FormatCurrency(baseCap);
        _rolloverValue.Text = GameShell.FormatCurrency(team.CarryoverCap);
        _adjustedCapValue.Text = GameShell.FormatCurrency(adjustedCap);
        _activeContractsValue.Text = GameShell.FormatCurrency(capUsed);
        _deadCapValue.Text = GameShell.FormatCurrency(deadCap);
        _totalCommittedValue.Text = GameShell.FormatCurrency(totalCommitted);
        _availableSpaceValue.Text = GameShell.FormatCurrency(available);

        // Color available space
        if (available > 0)
            _availableSpaceValue.Modulate = ThemeColors.Success;
        else
            _availableSpaceValue.Modulate = ThemeColors.Danger;

        // Cap usage bar
        float usagePercent = adjustedCap > 0 ? (float)capUsed / adjustedCap * 100f : 0;
        _capUsageBar.Value = usagePercent;
        _capUsageLabel.Text = $"{usagePercent:F1}% of cap used";

        // Top cap hits
        RefreshTopCapHits(gm, team, currentYear);

        // Projections
        RefreshProjections(gm, team, currentYear);
    }

    private void RefreshTopCapHits(GameManager gm, Models.Team team, int currentYear)
    {
        foreach (var child in _topHitsList.GetChildren())
            child.QueueFree();

        var topHits = gm.SalaryCapManager.GetTopCapHits(team, gm.Players, currentYear, 10);

        int rank = 1;
        foreach (var (player, capHit) in topHits)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);

            var rankLabel = new Label { Text = $"#{rank}" };
            rankLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            rankLabel.CustomMinimumSize = new Vector2(30, 0);
            hbox.AddChild(rankLabel);

            var nameBtn = new Button
            {
                Text = $"{player.Position} {player.FullName}",
                Flat = true,
            };
            nameBtn.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            nameBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            string capturedId = player.Id;
            nameBtn.Pressed += () => EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSelected, capturedId);
            hbox.AddChild(nameBtn);

            var ovrLabel = new Label { Text = player.Overall.ToString() };
            ovrLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            ovrLabel.Modulate = Components.OverallBadge.GetOverallColor(player.Overall);
            ovrLabel.CustomMinimumSize = new Vector2(30, 0);
            hbox.AddChild(ovrLabel);

            var capLabel = new Label { Text = GameShell.FormatCurrency(capHit) };
            capLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            capLabel.CustomMinimumSize = new Vector2(80, 0);
            capLabel.HorizontalAlignment = HorizontalAlignment.Right;
            hbox.AddChild(capLabel);

            _topHitsList.AddChild(hbox);
            rank++;
        }
    }

    private void RefreshProjections(GameManager gm, Models.Team team, int currentYear)
    {
        // Clear existing projection rows (keep header row)
        var children = _projectionsGrid.GetChildren();
        for (int i = children.Count - 1; i >= 4; i--) // Keep first 4 (header labels)
            children[i].QueueFree();

        var projections = gm.SalaryCapManager.GetCapProjections(team, gm.Players, currentYear, 3);

        foreach (var (year, committed) in projections)
        {
            long cap = gm.SalaryCapManager.GetCapForYear(year);
            long available = cap + team.CarryoverCap - committed;
            string marker = year == currentYear ? " *" : "";

            var yearLabel = new Label { Text = $"{year}{marker}" };
            yearLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _projectionsGrid.AddChild(yearLabel);

            var capLabel = new Label { Text = GameShell.FormatCurrency(cap) };
            capLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _projectionsGrid.AddChild(capLabel);

            var committedLabel = new Label { Text = GameShell.FormatCurrency(committed) };
            committedLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _projectionsGrid.AddChild(committedLabel);

            var availableLabel = new Label { Text = GameShell.FormatCurrency(available) };
            availableLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            availableLabel.Modulate = available >= 0
                ? ThemeColors.Success
                : ThemeColors.Danger;
            _projectionsGrid.AddChild(availableLabel);
        }
    }

    private void OnRosterChanged(string playerId, string teamId) => Refresh();
    private void OnWeekAdvanced(int year, int week) => Refresh();
}
