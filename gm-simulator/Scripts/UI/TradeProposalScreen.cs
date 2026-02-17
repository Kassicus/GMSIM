using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class TradeProposalScreen : Window
{
    private string _targetTeamId = "";

    // Left (player's team)
    private Label _leftTeamLabel = null!;
    private VBoxContainer _leftList = null!;

    // Right (target team)
    private Label _rightTeamLabel = null!;
    private VBoxContainer _rightList = null!;

    // Value display
    private Label _leftValueLabel = null!;
    private Label _rightValueLabel = null!;
    private ProgressBar _leftBar = null!;
    private ProgressBar _rightBar = null!;
    private Label _assessmentLabel = null!;
    private Label _capImpactLabel = null!;
    private Label _statusLabel = null!;
    private Button _proposeBtn = null!;

    // Selected assets
    private readonly HashSet<string> _offeredPlayerIds = new();
    private readonly HashSet<string> _offeredPickIds = new();
    private readonly HashSet<string> _requestedPlayerIds = new();
    private readonly HashSet<string> _requestedPickIds = new();

    public void Initialize(string targetTeamId)
    {
        _targetTeamId = targetTeamId;
    }

    public override void _Ready()
    {
        _leftTeamLabel = GetNode<Label>("Margin/VBox/HSplit/LeftPanel/LeftTeamLabel");
        _leftList = GetNode<VBoxContainer>("Margin/VBox/HSplit/LeftPanel/LeftScroll/LeftList");
        _rightTeamLabel = GetNode<Label>("Margin/VBox/HSplit/RightPanel/RightTeamLabel");
        _rightList = GetNode<VBoxContainer>("Margin/VBox/HSplit/RightPanel/RightScroll/RightList");
        _leftValueLabel = GetNode<Label>("Margin/VBox/ValueSection/ValueHBox/LeftValueLabel");
        _rightValueLabel = GetNode<Label>("Margin/VBox/ValueSection/ValueHBox/RightValueLabel");
        _leftBar = GetNode<ProgressBar>("Margin/VBox/ValueSection/BarHBox/LeftBar");
        _rightBar = GetNode<ProgressBar>("Margin/VBox/ValueSection/BarHBox/RightBar");
        _assessmentLabel = GetNode<Label>("Margin/VBox/ValueSection/AssessmentLabel");
        _capImpactLabel = GetNode<Label>("Margin/VBox/ValueSection/CapImpactLabel");
        _statusLabel = GetNode<Label>("Margin/VBox/BottomBar/StatusLabel");
        _proposeBtn = GetNode<Button>("Margin/VBox/BottomBar/ProposeBtn");

        PopulateTeams();
        UpdateValueComparison();
    }

    private void PopulateTeams()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var playerTeam = gm.GetPlayerTeam();
        var targetTeam = gm.GetTeam(_targetTeamId);
        if (playerTeam == null || targetTeam == null) return;

        _leftTeamLabel.Text = playerTeam.FullName;
        _rightTeamLabel.Text = targetTeam.FullName;

        // Populate left side: player's roster + picks
        PopulateTeamAssets(_leftList, playerTeam, gm, true);

        // Populate right side: target team roster + picks
        PopulateTeamAssets(_rightList, targetTeam, gm, false);
    }

    private void PopulateTeamAssets(VBoxContainer list, Team team, GameManager gm, bool isPlayerTeam)
    {
        foreach (var child in list.GetChildren())
            child.QueueFree();

        // Section: Players
        var playersHeader = new Label { Text = "— Players —" };
        playersHeader.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        list.AddChild(playersHeader);

        var teamPlayers = gm.Players
            .Where(p => p.TeamId == team.Id)
            .OrderByDescending(p => p.Overall)
            .ToList();

        foreach (var player in teamPlayers)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);

            var checkBox = new CheckBox
            {
                CustomMinimumSize = new Vector2(20, 0),
            };

            bool hasNTC = player.CurrentContract?.HasNoTradeClause == true;
            if (hasNTC) checkBox.Disabled = true;

            string pid = player.Id;
            checkBox.Toggled += (pressed) => OnPlayerToggled(pid, isPlayerTeam, pressed);
            hbox.AddChild(checkBox);

            string ntcMark = hasNTC ? " [NTC]" : "";
            var nameLabel = new Label
            {
                Text = $"{player.FullName}{ntcMark}",
                CustomMinimumSize = new Vector2(120, 0),
                ClipText = true,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(nameLabel);

            var posLabel = new Label
            {
                Text = player.Position.ToString(),
                CustomMinimumSize = new Vector2(40, 0),
            };
            posLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(posLabel);

            var ovrLabel = new Label
            {
                Text = player.Overall.ToString(),
                CustomMinimumSize = new Vector2(30, 0),
            };
            ovrLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(ovrLabel);

            var ageLabel = new Label
            {
                Text = $"Age {player.Age}",
                CustomMinimumSize = new Vector2(45, 0),
            };
            ageLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(ageLabel);

            list.AddChild(hbox);
        }

        // Section: Draft Picks
        list.AddChild(new HSeparator());
        var picksHeader = new Label { Text = "— Draft Picks —" };
        picksHeader.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        list.AddChild(picksHeader);

        var teamPicks = team.DraftPicks
            .Where(p => !p.IsUsed)
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Round)
            .ToList();

        if (teamPicks.Count == 0)
        {
            var noPicks = new Label { Text = "No available picks" };
            noPicks.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            list.AddChild(noPicks);
        }

        foreach (var pick in teamPicks)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);

            var checkBox = new CheckBox
            {
                CustomMinimumSize = new Vector2(20, 0),
                Disabled = pick.IsCompensatory,
            };

            string pickId = pick.Id;
            checkBox.Toggled += (pressed) => OnPickToggled(pickId, isPlayerTeam, pressed);
            hbox.AddChild(checkBox);

            string overall = pick.OverallNumber.HasValue ? $" #{pick.OverallNumber}" : "";
            string comp = pick.IsCompensatory ? " (Comp)" : "";
            string orig = pick.OriginalTeamId != pick.CurrentTeamId ? " (acquired)" : "";
            var pickLabel = new Label
            {
                Text = $"{pick.Year} Round {pick.Round}{overall}{comp}{orig}",
                CustomMinimumSize = new Vector2(200, 0),
            };
            pickLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(pickLabel);

            int value = gm.Trading.GetPickTradeValue(pick, gm.Calendar.CurrentYear);
            var valLabel = new Label
            {
                Text = $"({value} pts)",
                CustomMinimumSize = new Vector2(60, 0),
            };
            valLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Caption);
            hbox.AddChild(valLabel);

            list.AddChild(hbox);
        }
    }

    // --- Toggle Handlers ---

    private void OnPlayerToggled(string playerId, bool isPlayerTeam, bool selected)
    {
        var set = isPlayerTeam ? _offeredPlayerIds : _requestedPlayerIds;
        if (selected)
            set.Add(playerId);
        else
            set.Remove(playerId);

        UpdateValueComparison();
    }

    private void OnPickToggled(string pickId, bool isPlayerTeam, bool selected)
    {
        var set = isPlayerTeam ? _offeredPickIds : _requestedPickIds;
        if (selected)
            set.Add(pickId);
        else
            set.Remove(pickId);

        UpdateValueComparison();
    }

    // --- Value Comparison ---

    private void UpdateValueComparison()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int currentYear = gm.Calendar.CurrentYear;
        int givenValue = 0;
        int receivedValue = 0;

        foreach (var pid in _offeredPlayerIds)
        {
            var p = gm.GetPlayer(pid);
            if (p != null) givenValue += gm.Trading.CalculatePlayerTradeValue(p, currentYear);
        }
        foreach (var pid in _requestedPlayerIds)
        {
            var p = gm.GetPlayer(pid);
            if (p != null) receivedValue += gm.Trading.CalculatePlayerTradeValue(p, currentYear);
        }
        foreach (var pickId in _offeredPickIds)
        {
            var pick = gm.AllDraftPicks.FirstOrDefault(p => p.Id == pickId);
            if (pick != null) givenValue += gm.Trading.GetPickTradeValue(pick, currentYear);
        }
        foreach (var pickId in _requestedPickIds)
        {
            var pick = gm.AllDraftPicks.FirstOrDefault(p => p.Id == pickId);
            if (pick != null) receivedValue += gm.Trading.GetPickTradeValue(pick, currentYear);
        }

        _leftValueLabel.Text = $"You Give: {givenValue} pts";
        _rightValueLabel.Text = $"You Get: {receivedValue} pts";

        int maxVal = Math.Max(Math.Max(givenValue, receivedValue), 1);
        _leftBar.MaxValue = maxVal;
        _rightBar.MaxValue = maxVal;
        _leftBar.Value = givenValue;
        _rightBar.Value = receivedValue;

        // Assessment
        if (givenValue == 0 && receivedValue == 0)
        {
            _assessmentLabel.Text = "Select assets to trade";
            _assessmentLabel.RemoveThemeColorOverride("font_color");
        }
        else
        {
            float diff = givenValue > 0 ? (float)(receivedValue - givenValue) / givenValue : 0;
            (string text, Color color) = diff switch
            {
                > 0.15f => ("AI Assessment: STEAL (Likely Rejected)", ThemeColors.Danger),
                > 0.05f => ("AI Assessment: FAVORABLE", ThemeColors.Success),
                > -0.05f => ("AI Assessment: FAIR TRADE", ThemeColors.Success),
                > -0.10f => ("AI Assessment: SLIGHT OVERPAY", ThemeColors.Warning),
                _ => ("AI Assessment: BAD DEAL", ThemeColors.Danger),
            };
            _assessmentLabel.Text = text;
            _assessmentLabel.AddThemeColorOverride("font_color", color);
        }

        // Cap impact
        long capImpact = 0;
        foreach (var pid in _offeredPlayerIds)
        {
            var p = gm.GetPlayer(pid);
            if (p?.CurrentContract != null)
                capImpact += p.CurrentContract.GetCapHit(currentYear); // savings from sending
        }
        foreach (var pid in _requestedPlayerIds)
        {
            var p = gm.GetPlayer(pid);
            if (p?.CurrentContract != null)
                capImpact -= p.CurrentContract.GetCapHit(currentYear); // cost of receiving
        }

        if (capImpact != 0)
        {
            string sign = capImpact > 0 ? "+" : "";
            _capImpactLabel.Text = $"Cap Impact: {sign}{GameShell.FormatCurrency(capImpact)}";
        }
        else
        {
            _capImpactLabel.Text = "";
        }

        // Enable/disable propose button
        bool hasAssets = (_offeredPlayerIds.Count > 0 || _offeredPickIds.Count > 0)
            && (_requestedPlayerIds.Count > 0 || _requestedPickIds.Count > 0);
        _proposeBtn.Disabled = !hasAssets;
    }

    // --- Actions ---

    private void OnProposePressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var proposal = gm.Trading.CreatePlayerProposal(
            _targetTeamId,
            _offeredPlayerIds.ToList(),
            _offeredPickIds.ToList(),
            _requestedPlayerIds.ToList(),
            _requestedPickIds.ToList());

        var (success, msg) = gm.Trading.SubmitPlayerProposal(proposal);

        if (success)
        {
            _statusLabel.Text = "Trade accepted! Deal completed.";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
            _proposeBtn.Disabled = true;
        }
        else
        {
            _statusLabel.Text = $"Rejected: {msg}";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
        }
    }

    private void OnCancelPressed()
    {
        QueueFree();
    }
}
