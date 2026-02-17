using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class TradeHub : Control
{
    private Label _deadlineLabel = null!;
    private VBoxContainer _teamList = null!;
    private VBoxContainer _offerList = null!;
    private VBoxContainer _tradeBlockList = null!;
    private VBoxContainer _historyList = null!;
    private TabContainer _tabs = null!;

    private PackedScene _tradeProposalScene = null!;

    public override void _Ready()
    {
        _deadlineLabel = GetNode<Label>("VBox/Header/DeadlineLabel");
        _tabs = GetNode<TabContainer>("VBox/Tabs");
        _teamList = GetNode<VBoxContainer>("VBox/Tabs/Trade Partners/TeamList");
        _offerList = GetNode<VBoxContainer>("VBox/Tabs/Incoming Offers/OfferList");
        _tradeBlockList = GetNode<VBoxContainer>("VBox/Tabs/Trade Block/TradeBlockList");
        _historyList = GetNode<VBoxContainer>("VBox/Tabs/History/HistoryList");

        _tradeProposalScene = GD.Load<PackedScene>("res://Scenes/Trade/TradeProposalScreen.tscn");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.TradeAccepted += OnTradeCompleted;
            EventBus.Instance.TradeRejected += OnTradeRejected;
            EventBus.Instance.TradeProposed += OnTradeProposed;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.TradeAccepted -= OnTradeCompleted;
            EventBus.Instance.TradeRejected -= OnTradeRejected;
            EventBus.Instance.TradeProposed -= OnTradeProposed;
        }
    }

    private void Refresh()
    {
        RefreshDeadline();
        RefreshTeamList();
        RefreshOffers();
        RefreshTradeBlock();
        RefreshHistory();
    }

    // --- Deadline Status ---

    private void RefreshDeadline()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        bool isOpen = gm.Trading.IsTradeWindowOpen();
        if (!isOpen)
        {
            _deadlineLabel.Text = "Trade Window: CLOSED";
            _deadlineLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
        }
        else if (gm.Trading.IsNearDeadline())
        {
            _deadlineLabel.Text = "Trade Deadline Approaching!";
            _deadlineLabel.AddThemeColorOverride("font_color", ThemeColors.Warning);
        }
        else
        {
            _deadlineLabel.Text = "Trade Window: OPEN";
            _deadlineLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
        }
    }

    // --- Tab 1: Trade Partners ---

    private void RefreshTeamList()
    {
        foreach (var child in _teamList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        bool windowOpen = gm.Trading.IsTradeWindowOpen();

        // Header row
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        AddLabel(header, "Team", 180, ThemeFonts.Body, true);
        AddLabel(header, "Record", 70, ThemeFonts.Body, true);
        AddLabel(header, "Cap Space", 100, ThemeFonts.Body, true);
        AddLabel(header, "Strategy", 80, ThemeFonts.Body, true);
        AddLabel(header, "", 100, ThemeFonts.Body, false);
        _teamList.AddChild(header);

        _teamList.AddChild(new HSeparator());

        foreach (var team in gm.Teams.OrderBy(t => t.Division).ThenBy(t => t.FullName))
        {
            if (team.Id == gm.PlayerTeamId) continue;

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);

            AddLabel(hbox, team.FullName, 180, ThemeFonts.Small, false);
            var rec = team.CurrentRecord;
            AddLabel(hbox, $"{rec.Wins}-{rec.Losses}-{rec.Ties}", 70, ThemeFonts.Small, false);
            AddLabel(hbox, GameShell.FormatCurrency(team.CapSpace), 100, ThemeFonts.Small, false);

            // Show AI strategy
            string strategy = gm.AIProfiles.TryGetValue(team.Id, out var profile)
                ? profile.Strategy.ToString() : "—";
            AddLabel(hbox, strategy, 80, ThemeFonts.Small, false);

            var proposeBtn = new Button
            {
                Text = "Propose Trade",
                CustomMinimumSize = new Vector2(100, 0),
                Disabled = !windowOpen,
            };
            string tid = team.Id;
            proposeBtn.Pressed += () => OnProposeTradePressed(tid);
            hbox.AddChild(proposeBtn);

            _teamList.AddChild(hbox);
        }
    }

    // --- Tab 2: Incoming Offers ---

    private void RefreshOffers()
    {
        foreach (var child in _offerList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var pending = gm.Trading.PendingProposals
            .Where(p => p.Status == TradeStatus.Pending && p.ReceivingTeamId == gm.PlayerTeamId)
            .ToList();

        if (pending.Count == 0)
        {
            var emptyLabel = new Label { Text = "No incoming trade offers." };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _offerList.AddChild(emptyLabel);
            return;
        }

        foreach (var proposal in pending)
        {
            var vbox = new VBoxContainer();

            var fromTeam = gm.GetTeam(proposal.ProposingTeamId);
            var titleLabel = new Label
            {
                Text = $"Offer from {fromTeam?.FullName ?? "Unknown"}"
            };
            titleLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            vbox.AddChild(titleLabel);

            // What they offer
            var offerText = "They offer: ";
            foreach (var pid in proposal.ProposingPlayerIds)
            {
                var p = gm.GetPlayer(pid);
                offerText += $"{p?.FullName} ({p?.Position} {p?.Overall} OVR), ";
            }
            foreach (var pickId in proposal.ProposingPickIds)
            {
                var pick = gm.AllDraftPicks.FirstOrDefault(p => p.Id == pickId);
                if (pick != null)
                    offerText += $"{pick.Year} Rd {pick.Round} Pick, ";
            }
            var offerLabel = new Label { Text = offerText.TrimEnd(',', ' ') };
            offerLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            vbox.AddChild(offerLabel);

            // What they want
            var wantText = "They want: ";
            foreach (var pid in proposal.ReceivingPlayerIds)
            {
                var p = gm.GetPlayer(pid);
                wantText += $"{p?.FullName} ({p?.Position} {p?.Overall} OVR), ";
            }
            foreach (var pickId in proposal.ReceivingPickIds)
            {
                var pick = gm.AllDraftPicks.FirstOrDefault(p => p.Id == pickId);
                if (pick != null)
                    wantText += $"{pick.Year} Rd {pick.Round} Pick, ";
            }
            var wantLabel = new Label { Text = wantText.TrimEnd(',', ' ') };
            wantLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            vbox.AddChild(wantLabel);

            // Value comparison
            var valueLabel = new Label
            {
                Text = $"Value: You give {proposal.ReceivingValuePoints} pts, receive {proposal.ProposingValuePoints} pts"
            };
            valueLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            vbox.AddChild(valueLabel);

            // Buttons
            var btnHBox = new HBoxContainer();
            btnHBox.AddThemeConstantOverride("separation", 10);

            var acceptBtn = new Button { Text = "Accept", CustomMinimumSize = new Vector2(80, 0) };
            string propId = proposal.Id;
            acceptBtn.Pressed += () => OnAcceptOffer(propId);
            btnHBox.AddChild(acceptBtn);

            var rejectBtn = new Button { Text = "Reject", CustomMinimumSize = new Vector2(80, 0) };
            rejectBtn.Pressed += () => OnRejectOffer(propId);
            btnHBox.AddChild(rejectBtn);

            vbox.AddChild(btnHBox);
            vbox.AddChild(new HSeparator());
            _offerList.AddChild(vbox);
        }
    }

    // --- Tab 3: Trade Block ---

    private void RefreshTradeBlock()
    {
        foreach (var child in _tradeBlockList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Header
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        AddLabel(header, "Player", 150, ThemeFonts.Body, true);
        AddLabel(header, "Pos", 45, ThemeFonts.Body, true);
        AddLabel(header, "OVR", 40, ThemeFonts.Body, true);
        AddLabel(header, "Age", 35, ThemeFonts.Body, true);
        AddLabel(header, "Cap Hit", 80, ThemeFonts.Body, true);
        AddLabel(header, "Trade Block", 90, ThemeFonts.Body, true);
        _tradeBlockList.AddChild(header);
        _tradeBlockList.AddChild(new HSeparator());

        var teamPlayers = gm.Players
            .Where(p => p.TeamId == team.Id)
            .OrderByDescending(p => p.Overall)
            .ToList();

        foreach (var player in teamPlayers)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);

            AddLabel(hbox, player.FullName, 150, ThemeFonts.Small, false);
            AddLabel(hbox, player.Position.ToString(), 45, ThemeFonts.Small, false);
            AddLabel(hbox, player.Overall.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, player.Age.ToString(), 35, ThemeFonts.Small, false);

            long capHit = player.CurrentContract?.GetCapHit(gm.Calendar.CurrentYear) ?? 0;
            AddLabel(hbox, GameShell.FormatCurrency(capHit), 80, ThemeFonts.Small, false);

            bool onBlock = gm.Trading.IsOnTradeBlock(player.Id);
            var toggleBtn = new Button
            {
                Text = onBlock ? "Remove" : "Add",
                CustomMinimumSize = new Vector2(90, 0),
                Disabled = player.CurrentContract?.HasNoTradeClause == true,
            };
            if (player.CurrentContract?.HasNoTradeClause == true)
                toggleBtn.Text = "NTC";

            string pid = player.Id;
            toggleBtn.Pressed += () => OnToggleTradeBlock(pid);
            hbox.AddChild(toggleBtn);

            _tradeBlockList.AddChild(hbox);
        }
    }

    // --- Tab 4: History ---

    private void RefreshHistory()
    {
        foreach (var child in _historyList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var history = gm.Trading.TradeHistory;
        if (history.Count == 0)
        {
            var emptyLabel = new Label { Text = "No trades completed this season." };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _historyList.AddChild(emptyLabel);
            return;
        }

        foreach (var record in history.Reverse())
        {
            var vbox = new VBoxContainer();

            var team1 = gm.GetTeam(record.Team1Id);
            var team2 = gm.GetTeam(record.Team2Id);

            var titleLabel = new Label
            {
                Text = $"Week {record.Week} — {team1?.FullName ?? "?"} / {team2?.FullName ?? "?"}"
            };
            titleLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
            vbox.AddChild(titleLabel);

            // Team 1 sent
            string t1Sent = $"  {team1?.Abbreviation ?? "?"} sent: ";
            t1Sent += string.Join(", ", record.Team1SentPlayerNames.Concat(record.Team1SentPickDescriptions));
            var t1Label = new Label { Text = t1Sent };
            t1Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            vbox.AddChild(t1Label);

            // Team 2 sent
            string t2Sent = $"  {team2?.Abbreviation ?? "?"} sent: ";
            t2Sent += string.Join(", ", record.Team2SentPlayerNames.Concat(record.Team2SentPickDescriptions));
            var t2Label = new Label { Text = t2Sent };
            t2Label.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            vbox.AddChild(t2Label);

            vbox.AddChild(new HSeparator());
            _historyList.AddChild(vbox);
        }
    }

    // --- Actions ---

    private void OnProposeTradePressed(string targetTeamId)
    {
        var screen = _tradeProposalScene.Instantiate<TradeProposalScreen>();
        screen.Initialize(targetTeamId);
        GetTree().Root.AddChild(screen);
    }

    private void OnAcceptOffer(string proposalId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, msg) = gm.Trading.AcceptAIProposal(proposalId);
        GD.Print($"Trade accept: {success} — {msg}");
        Refresh();
    }

    private void OnRejectOffer(string proposalId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.Trading.RejectAIProposal(proposalId);
        Refresh();
    }

    private void OnToggleTradeBlock(string playerId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.Trading.IsOnTradeBlock(playerId))
            gm.Trading.RemoveFromTradeBlock(playerId);
        else
            gm.Trading.AddToTradeBlock(playerId);

        RefreshTradeBlock();
    }

    // --- Signal Handlers ---

    private void OnTradeCompleted(string tradeId) => Refresh();
    private void OnTradeRejected(string tradeId) => Refresh();
    private void OnTradeProposed(string from, string to) => Refresh();

    // --- Utility ---

    private static void AddLabel(HBoxContainer parent, string text, int minWidth, int fontSize, bool bold)
    {
        UIFactory.AddCell(parent, text, minWidth, fontSize);
    }
}
