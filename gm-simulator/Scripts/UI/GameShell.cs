using Godot;
using GMSimulator.Core;

namespace GMSimulator.UI;

public partial class GameShell : Control
{
    private Label _teamLabel = null!;
    private Label _recordLabel = null!;
    private Label _phaseLabel = null!;
    private Label _weekLabel = null!;
    private Label _capLabel = null!;
    private Control _contentArea = null!;

    private PackedScene _dashboardScene = null!;
    private PackedScene _rosterViewScene = null!;
    private PackedScene _depthChartScene = null!;
    private PackedScene _capOverviewScene = null!;
    private PackedScene _playerCardScene = null!;
    private Node? _currentContent;

    public override void _Ready()
    {
        _teamLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/TeamLabel");
        _recordLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/RecordLabel");
        _phaseLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/PhaseLabel");
        _weekLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/WeekLabel");
        _capLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/CapLabel");
        _contentArea = GetNode<Control>("VBox/ContentArea");

        // Connect to EventBus signals
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PhaseChanged += OnPhaseChanged;
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
            EventBus.Instance.PlayerCut += OnRosterChanged;
            EventBus.Instance.PlayerSigned += OnRosterChanged;
            EventBus.Instance.PlayerSelected += OnPlayerSelected;
        }

        // Preload all content scenes
        _dashboardScene = GD.Load<PackedScene>("res://Scenes/Dashboard/Dashboard.tscn");
        _rosterViewScene = GD.Load<PackedScene>("res://Scenes/Roster/RosterView.tscn");
        _depthChartScene = GD.Load<PackedScene>("res://Scenes/Roster/DepthChart.tscn");
        _capOverviewScene = GD.Load<PackedScene>("res://Scenes/Roster/CapOverview.tscn");
        _playerCardScene = GD.Load<PackedScene>("res://Scenes/Roster/PlayerCard.tscn");

        LoadContent(_dashboardScene);
        RefreshTopBar();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PhaseChanged -= OnPhaseChanged;
            EventBus.Instance.WeekAdvanced -= OnWeekAdvanced;
            EventBus.Instance.PlayerCut -= OnRosterChanged;
            EventBus.Instance.PlayerSigned -= OnRosterChanged;
            EventBus.Instance.PlayerSelected -= OnPlayerSelected;
        }
    }

    private void LoadContent(PackedScene scene)
    {
        if (_currentContent != null)
        {
            _contentArea.RemoveChild(_currentContent);
            _currentContent.QueueFree();
        }

        _currentContent = scene.Instantiate();
        if (_currentContent is Control ctrl)
        {
            ctrl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        _contentArea.AddChild(_currentContent);
    }

    private void RefreshTopBar()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        _teamLabel.Text = team.FullName;
        _recordLabel.Text = $"{team.CurrentRecord.Wins}-{team.CurrentRecord.Losses}-{team.CurrentRecord.Ties}";
        _phaseLabel.Text = gm.Calendar.GetPhaseDisplayName();
        _weekLabel.Text = $"Wk {gm.Calendar.CurrentWeek}/{gm.Calendar.GetTotalWeeksInPhase()}";
        _capLabel.Text = $"Cap: {FormatCurrency(team.CapSpace)}";
    }

    // --- Navigation ---

    private void OnNavDashboard() => LoadContent(_dashboardScene);
    private void OnNavRoster() => LoadContent(_rosterViewScene);
    private void OnNavDepthChart() => LoadContent(_depthChartScene);
    private void OnNavCap() => LoadContent(_capOverviewScene);

    // --- PlayerCard Popup ---

    private void OnPlayerSelected(string playerId)
    {
        var card = _playerCardScene.Instantiate<PlayerCard>();
        card.Initialize(playerId);
        AddChild(card);
        card.PopupCentered();
    }

    // --- Top Bar Button Handlers ---

    private void OnAdvancePressed()
    {
        GameManager.Instance?.AdvanceWeek();
        RefreshTopBar();
    }

    private void OnSavePressed()
    {
        SaveLoadManager.SaveGame("Quick Save", 0);
    }

    private void OnMenuPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
    }

    // --- Signal Handlers ---

    private void OnPhaseChanged(int phase) => RefreshTopBar();
    private void OnWeekAdvanced(int year, int week) => RefreshTopBar();
    private void OnRosterChanged(string playerId, string teamId) => RefreshTopBar();

    // --- Utility ---

    public static string FormatCurrency(long cents)
    {
        decimal dollars = cents / 100m;
        if (Math.Abs(dollars) >= 1_000_000)
            return $"${dollars / 1_000_000:N1}M";
        if (Math.Abs(dollars) >= 1_000)
            return $"${dollars / 1_000:N0}K";
        return $"${dollars:N0}";
    }

    public static string FormatHeight(int totalInches)
    {
        int feet = totalInches / 12;
        int inches = totalInches % 12;
        return $"{feet}'{inches}\"";
    }
}
