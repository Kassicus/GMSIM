using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.Systems;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class DraftRoom : Control
{
    private Label _pickLabel = null!;
    private Label _teamLabel = null!;
    private Label _statusLabel = null!;
    private VBoxContainer _boardList = null!;
    private Label _needsLabel = null!;
    private VBoxContainer _historyList = null!;
    private Button _selectBtn = null!;
    private Button _autoPickBtn = null!;
    private Button _simBtn = null!;
    private Button _tradeBtn = null!;

    private PackedScene _tradeProposalScene = null!;

    private string? _selectedProspectId;
    private const int MaxBoardRows = 50;

    public override void _Ready()
    {
        _pickLabel = GetNode<Label>("VBox/TopBar/TopHBox/PickLabel");
        _teamLabel = GetNode<Label>("VBox/TopBar/TopHBox/TeamLabel");
        _statusLabel = GetNode<Label>("VBox/TopBar/TopHBox/StatusLabel");
        _boardList = GetNode<VBoxContainer>("VBox/HSplit/LeftPanel/BoardScroll/BoardList");
        _needsLabel = GetNode<Label>("VBox/HSplit/RightPanel/NeedsLabel");
        _historyList = GetNode<VBoxContainer>("VBox/HSplit/RightPanel/HistoryScroll/HistoryList");
        _selectBtn = GetNode<Button>("VBox/BottomBar/ButtonHBox/SelectBtn");
        _autoPickBtn = GetNode<Button>("VBox/BottomBar/ButtonHBox/AutoPickBtn");
        _simBtn = GetNode<Button>("VBox/BottomBar/ButtonHBox/SimBtn");

        _tradeProposalScene = GD.Load<PackedScene>("res://Scenes/Trade/TradeProposalScreen.tscn");

        // Add Trade Pick button to the button bar
        var buttonHBox = GetNode<HBoxContainer>("VBox/BottomBar/ButtonHBox");
        _tradeBtn = new Button
        {
            Text = "Trade Pick",
            CustomMinimumSize = new Vector2(100, 0),
        };
        _tradeBtn.Pressed += OnTradePickPressed;
        buttonHBox.AddChild(_tradeBtn);

        if (EventBus.Instance != null)
            EventBus.Instance.TradeAccepted += OnDraftTradeCompleted;

        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.TradeAccepted -= OnDraftTradeCompleted;
    }

    private void RefreshAll()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var draft = gm.Draft;

        if (draft.IsDraftComplete())
        {
            _pickLabel.Text = "DRAFT COMPLETE";
            _teamLabel.Text = "";
            _selectBtn.Disabled = true;
            _autoPickBtn.Disabled = true;
            _simBtn.Disabled = true;
            _tradeBtn.Disabled = true;
            RefreshHistory(draft);
            return;
        }

        var currentPick = draft.GetCurrentPick();
        if (currentPick == null) return;

        bool isPlayerPick = draft.IsPlayerPick();
        var team = gm.GetTeam(currentPick.CurrentTeamId);

        _pickLabel.Text = $"Round {currentPick.Round}, Pick {currentPick.OverallNumber}";
        _teamLabel.Text = team?.FullName ?? currentPick.CurrentTeamId;

        if (isPlayerPick)
        {
            _statusLabel.Text = "YOUR PICK!";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
            _selectBtn.Disabled = _selectedProspectId == null;
            _autoPickBtn.Disabled = false;
            _simBtn.Disabled = true;
        }
        else
        {
            _statusLabel.Text = "";
            _selectBtn.Disabled = true;
            _autoPickBtn.Disabled = true;
            _simBtn.Disabled = false;
        }

        _tradeBtn.Disabled = false;

        RefreshBoard(draft, gm);
        RefreshNeeds(gm);
        RefreshHistory(draft);
    }

    private void RefreshBoard(DraftSystem draft, GameManager gm)
    {
        foreach (var child in _boardList.GetChildren())
            child.QueueFree();

        _selectedProspectId = null;

        var available = draft.GetAvailableProspects()
            .OrderByDescending(p => p.DraftValue)
            .Take(MaxBoardRows)
            .ToList();

        foreach (var prospect in available)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            var selectBtn = new Button
            {
                Text = "Pick",
                CustomMinimumSize = new Vector2(40, 0),
                Disabled = !draft.IsPlayerPick(),
            };
            string pid = prospect.Id;
            selectBtn.Pressed += () => SelectProspect(pid);
            hbox.AddChild(selectBtn);

            var nameLabel = new Label
            {
                Text = prospect.FullName,
                CustomMinimumSize = new Vector2(130, 0),
            };
            nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(nameLabel);

            var posLabel = new Label
            {
                Text = prospect.Position.ToString(),
                CustomMinimumSize = new Vector2(45, 0),
            };
            posLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(posLabel);

            var collegeLabel = new Label
            {
                Text = prospect.College,
                CustomMinimumSize = new Vector2(90, 0),
                ClipText = true,
            };
            collegeLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(collegeLabel);

            var gradeLabel = new Label
            {
                Text = prospect.ScoutGrade.ToString(),
                CustomMinimumSize = new Vector2(75, 0),
            };
            gradeLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(gradeLabel);

            var projLabel = new Label
            {
                Text = $"Rd {prospect.ProjectedRound}",
                CustomMinimumSize = new Vector2(35, 0),
            };
            projLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(projLabel);

            _boardList.AddChild(hbox);
        }
    }

    private void RefreshNeeds(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) { _needsLabel.Text = ""; return; }

        var positionDepths = new Dictionary<Position, int>();
        foreach (Position pos in Enum.GetValues<Position>())
        {
            int depth = 0;
            if (team.DepthChart.Chart.TryGetValue(pos, out var list))
                depth = list.Count;
            positionDepths[pos] = depth;
        }

        var needs = positionDepths
            .Where(kv => kv.Value <= 1)
            .OrderBy(kv => kv.Value)
            .Select(kv => $"{kv.Key}: {(kv.Value == 0 ? "EMPTY" : "thin")}")
            .ToList();

        _needsLabel.Text = needs.Count > 0 ? string.Join("\n", needs) : "No critical needs.";
    }

    private void RefreshHistory(DraftSystem draft)
    {
        foreach (var child in _historyList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // Show most recent picks first
        foreach (var result in draft.DraftResults.AsEnumerable().Reverse().Take(20))
        {
            var team = gm.GetTeam(result.TeamId);
            string teamAbbr = team?.Abbreviation ?? "???";

            var label = new Label
            {
                Text = $"Rd {result.Round} #{result.OverallPick}: {teamAbbr} — {result.Position} {result.ProspectName} ({result.College})",
            };
            label.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);

            if (result.TeamId == gm.PlayerTeamId)
                label.AddThemeColorOverride("font_color", ThemeColors.Success);

            _historyList.AddChild(label);
        }
    }

    private void SelectProspect(string prospectId)
    {
        _selectedProspectId = prospectId;
        _selectBtn.Disabled = false;
        _selectBtn.Text = "Confirm Pick";
    }

    private void OnSelectPressed()
    {
        if (_selectedProspectId == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        var result = gm.Draft.MakeSelection(_selectedProspectId);
        if (result != null)
        {
            _statusLabel.Text = $"Selected: {result.Position} {result.ProspectName}";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
        }

        _selectedProspectId = null;
        _selectBtn.Text = "Select Player";
        RefreshAll();
    }

    private void OnAutoPickPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var result = gm.Draft.AutoPick();
        if (result != null)
        {
            _statusLabel.Text = $"Auto-picked: {result.Position} {result.ProspectName}";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Warning);
        }

        RefreshAll();
    }

    private void OnSimToNextPickPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var results = gm.Draft.SimulateToNextPlayerPick();

        if (results.Count > 0)
        {
            _statusLabel.Text = $"Simulated {results.Count} picks.";
        }

        RefreshAll();
    }

    // --- Draft-Day Trading ---

    private void OnTradePickPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Create a team selector popup
        var popup = new Window
        {
            Title = "Select Trade Partner",
            Size = new Vector2I(300, 500),
            Exclusive = true,
        };

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        popup.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(vbox);

        var titleLabel = new Label { Text = "Choose a team to trade with:" };
        titleLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
        vbox.AddChild(titleLabel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        var teamList = new VBoxContainer();
        teamList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(teamList);

        foreach (var team in gm.Teams.Where(t => t.Id != gm.PlayerTeamId).OrderBy(t => t.FullName))
        {
            var btn = new Button
            {
                Text = team.FullName,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            string teamId = team.Id;
            btn.Pressed += () =>
            {
                popup.QueueFree();
                OpenTradeProposal(teamId);
            };
            teamList.AddChild(btn);
        }

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Pressed += () => popup.QueueFree();
        vbox.AddChild(cancelBtn);

        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void OpenTradeProposal(string targetTeamId)
    {
        var screen = _tradeProposalScene.Instantiate<TradeProposalScreen>();
        screen.Initialize(targetTeamId);
        GetTree().Root.AddChild(screen);
    }

    private void OnDraftTradeCompleted(string tradeId)
    {
        RefreshAll();
    }
}
