using Godot;
using GMSimulator.Core;
using GMSimulator.UI.Components;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class GameShell : Control
{
    private Label _teamLabel = null!;
    private Label _recordLabel = null!;
    private Label _phaseLabel = null!;
    private Label _weekLabel = null!;
    private Label _capLabel = null!;
    private Control _contentArea = null!;
    private VBoxContainer _notificationContainer = null!;

    private PackedScene _dashboardScene = null!;
    private PackedScene _rosterViewScene = null!;
    private PackedScene _depthChartScene = null!;
    private PackedScene _capOverviewScene = null!;
    private PackedScene _playerCardScene = null!;
    private PackedScene _weekScheduleScene = null!;
    private PackedScene _standingsScene = null!;
    private PackedScene _postGameReportScene = null!;
    private PackedScene _freeAgentMarketScene = null!;
    private PackedScene _scoutingHubScene = null!;
    private PackedScene _draftBoardScene = null!;
    private PackedScene _draftRoomScene = null!;
    private PackedScene _tradeHubScene = null!;
    private PackedScene _staffOverviewScene = null!;
    private PackedScene _transactionLogScene = null!;
    private PackedScene _leagueLeadersScene = null!;
    private PackedScene _teamHistoryScene = null!;
    private PackedScene _playerComparisonScene = null!;
    private PackedScene _settingsPanelScene = null!;
    private Node? _currentContent;

    public override void _Ready()
    {
        _teamLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/TeamLabel");
        _recordLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/RecordLabel");
        _phaseLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/PhaseLabel");
        _weekLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/WeekLabel");
        _capLabel = GetNode<Label>("VBox/TopBar/TopBarHBox/CapLabel");
        _contentArea = GetNode<Control>("VBox/ContentArea");
        _notificationContainer = GetNode<VBoxContainer>("NotificationContainer");

        // Load settings
        SettingsManager.Load();

        // Apply theme
        ApplyTheme();

        // Connect to EventBus signals
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PhaseChanged += OnPhaseChanged;
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
            EventBus.Instance.PlayerCut += OnRosterChanged;
            EventBus.Instance.PlayerSigned += OnRosterChanged;
            EventBus.Instance.PlayerSelected += OnPlayerSelected;
            EventBus.Instance.WeekSimulated += OnWeekSimulated;
            EventBus.Instance.TradeAccepted += OnTradeCompleted;
            EventBus.Instance.CoachingCarouselCompleted += OnCarouselCompleted;
            EventBus.Instance.NotificationCreated += OnNotification;
        }

        // Preload all content scenes
        _dashboardScene = GD.Load<PackedScene>("res://Scenes/Dashboard/Dashboard.tscn");
        _rosterViewScene = GD.Load<PackedScene>("res://Scenes/Roster/RosterView.tscn");
        _depthChartScene = GD.Load<PackedScene>("res://Scenes/Roster/DepthChart.tscn");
        _capOverviewScene = GD.Load<PackedScene>("res://Scenes/Roster/CapOverview.tscn");
        _playerCardScene = GD.Load<PackedScene>("res://Scenes/Roster/PlayerCard.tscn");
        _weekScheduleScene = GD.Load<PackedScene>("res://Scenes/GameDay/WeekSchedule.tscn");
        _standingsScene = GD.Load<PackedScene>("res://Scenes/League/Standings.tscn");
        _postGameReportScene = GD.Load<PackedScene>("res://Scenes/GameDay/PostGameReport.tscn");
        _freeAgentMarketScene = GD.Load<PackedScene>("res://Scenes/FreeAgency/FreeAgentMarket.tscn");
        _scoutingHubScene = GD.Load<PackedScene>("res://Scenes/Scouting/ScoutingHub.tscn");
        _draftBoardScene = GD.Load<PackedScene>("res://Scenes/Draft/DraftBoard.tscn");
        _draftRoomScene = GD.Load<PackedScene>("res://Scenes/Draft/DraftRoom.tscn");
        _tradeHubScene = GD.Load<PackedScene>("res://Scenes/Trade/TradeHub.tscn");
        _staffOverviewScene = GD.Load<PackedScene>("res://Scenes/Staff/StaffOverview.tscn");
        _transactionLogScene = GD.Load<PackedScene>("res://Scenes/League/TransactionLog.tscn");
        _leagueLeadersScene = GD.Load<PackedScene>("res://Scenes/League/LeagueLeaders.tscn");
        _teamHistoryScene = GD.Load<PackedScene>("res://Scenes/League/TeamHistory.tscn");
        _playerComparisonScene = GD.Load<PackedScene>("res://Scenes/Roster/PlayerComparison.tscn");
        _settingsPanelScene = GD.Load<PackedScene>("res://Scenes/Main/SettingsPanel.tscn");

        LoadContent(_dashboardScene);
        RefreshTopBar();
    }

    private void ApplyTheme()
    {
        // App background
        var bg = new ColorRect();
        bg.Color = ThemeColors.BgDeep;
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.ZIndex = -1;
        AddChild(bg);
        MoveChild(bg, 0);

        // Top bar
        var topBar = GetNode<PanelContainer>("VBox/TopBar");
        topBar.AddThemeStyleboxOverride("panel", ThemeStyles.TopBar());

        _teamLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Subtitle);
        _teamLabel.AddThemeColorOverride("font_color", ThemeColors.TextPrimary);
        _recordLabel.AddThemeColorOverride("font_color", ThemeColors.AccentText);
        _phaseLabel.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);
        _weekLabel.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);
        _capLabel.AddThemeColorOverride("font_color", ThemeColors.Success);

        // Top bar buttons
        StyleButton(GetNode<Button>("VBox/TopBar/TopBarHBox/AdvanceButton"), true);
        StyleButton(GetNode<Button>("VBox/TopBar/TopBarHBox/SettingsButton"), false);
        StyleButton(GetNode<Button>("VBox/TopBar/TopBarHBox/SaveButton"), false);
        StyleButton(GetNode<Button>("VBox/TopBar/TopBarHBox/MenuButton"), false);

        // Nav bar
        var navBar = GetNode<PanelContainer>("VBox/NavBar");
        navBar.AddThemeStyleboxOverride("panel", ThemeStyles.NavBar());

        var navHBox = GetNode<HBoxContainer>("VBox/NavBar/NavHBox");
        foreach (var child in navHBox.GetChildren())
        {
            if (child is Button btn)
                StyleNavButton(btn);
        }
    }

    private static void StyleButton(Button btn, bool primary)
    {
        if (primary)
        {
            btn.AddThemeStyleboxOverride("normal", ThemeStyles.PrimaryButton());
            btn.AddThemeStyleboxOverride("hover", ThemeStyles.PrimaryButtonHover());
            btn.AddThemeStyleboxOverride("pressed", ThemeStyles.PrimaryButton());
            btn.AddThemeColorOverride("font_color", ThemeColors.TextPrimary);
            btn.AddThemeColorOverride("font_hover_color", ThemeColors.TextPrimary);
        }
        else
        {
            btn.AddThemeStyleboxOverride("normal", ThemeStyles.SecondaryButton());
            btn.AddThemeStyleboxOverride("hover", ThemeStyles.SecondaryButtonHover());
            btn.AddThemeStyleboxOverride("pressed", ThemeStyles.SecondaryButton());
            btn.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);
            btn.AddThemeColorOverride("font_hover_color", ThemeColors.TextPrimary);
        }
        btn.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
    }

    private static void StyleNavButton(Button btn)
    {
        btn.AddThemeStyleboxOverride("normal", ThemeStyles.NavItemNormal());
        btn.AddThemeStyleboxOverride("hover", ThemeStyles.NavItemHover());
        btn.AddThemeStyleboxOverride("pressed", ThemeStyles.NavItemActive());
        btn.AddThemeColorOverride("font_color", ThemeColors.NavItemText);
        btn.AddThemeColorOverride("font_hover_color", ThemeColors.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", ThemeColors.NavItemActiveText);
        btn.AddThemeColorOverride("font_focus_color", ThemeColors.NavItemActiveText);
        btn.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
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
            EventBus.Instance.WeekSimulated -= OnWeekSimulated;
            EventBus.Instance.TradeAccepted -= OnTradeCompleted;
            EventBus.Instance.CoachingCarouselCompleted -= OnCarouselCompleted;
            EventBus.Instance.NotificationCreated -= OnNotification;
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
    private void OnNavStaff() => LoadContent(_staffOverviewScene);
    private void OnNavSchedule() => LoadContent(_weekScheduleScene);
    private void OnNavStandings() => LoadContent(_standingsScene);
    private void OnNavFreeAgency() => LoadContent(_freeAgentMarketScene);
    private void OnNavTrade() => LoadContent(_tradeHubScene);
    private void OnNavScouting() => LoadContent(_scoutingHubScene);
    private void OnNavDraftBoard()
    {
        // During Draft phase, show DraftRoom instead of DraftBoard
        var gm = GameManager.Instance;
        if (gm != null && gm.Calendar.CurrentPhase == GMSimulator.Models.Enums.GamePhase.Draft)
            LoadContent(_draftRoomScene);
        else
            LoadContent(_draftBoardScene);
    }

    private void OnNavTransactionLog() => LoadContent(_transactionLogScene);
    private void OnNavLeagueLeaders() => LoadContent(_leagueLeadersScene);
    private void OnNavTeamHistory() => LoadContent(_teamHistoryScene);

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

    private void OnSettingsPressed()
    {
        var panel = _settingsPanelScene.Instantiate<SettingsPanel>();
        AddChild(panel);
        panel.PopupCentered();
    }

    public void OpenPlayerComparison(string? preselectedPlayerId = null)
    {
        var comparison = _playerComparisonScene.Instantiate<PlayerComparison>();
        if (preselectedPlayerId != null)
            comparison.Initialize(preselectedPlayerId);
        AddChild(comparison);
        comparison.PopupCentered();
    }

    // --- Signal Handlers ---

    private void OnPhaseChanged(int phase) => RefreshTopBar();
    private void OnWeekAdvanced(int year, int week) => RefreshTopBar();
    private void OnRosterChanged(string playerId, string teamId) => RefreshTopBar();
    private void OnTradeCompleted(string tradeId) => RefreshTopBar();
    private void OnCarouselCompleted(int year) => RefreshTopBar();

    private void OnWeekSimulated(int year, int week)
    {
        RefreshTopBar();

        // Auto-show PostGameReport for player's team game
        var gm = GameManager.Instance;
        if (gm == null) return;

        var playerResult = gm.RecentGameResults.FirstOrDefault(r =>
        {
            var game = gm.CurrentSeason.Games.FirstOrDefault(g => g.Id == r.GameId);
            return game != null && (game.HomeTeamId == gm.PlayerTeamId || game.AwayTeamId == gm.PlayerTeamId);
        });

        if (playerResult != null)
        {
            var report = _postGameReportScene.Instantiate<PostGameReport>();
            report.Initialize(playerResult);
            GetTree().Root.AddChild(report);
        }
    }

    // --- Notifications ---

    private void OnNotification(string title, string message, int priority)
    {
        var toast = NotificationToast.Create(title, message, priority);
        _notificationContainer.AddChild(toast);

        // Limit to 5 visible toasts
        while (_notificationContainer.GetChildCount() > 5)
        {
            var oldest = _notificationContainer.GetChild(0);
            _notificationContainer.RemoveChild(oldest);
            oldest.QueueFree();
        }
    }

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
