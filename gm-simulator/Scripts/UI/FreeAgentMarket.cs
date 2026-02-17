using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.Systems;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class FreeAgentMarket : Control
{
    private Label _headerLabel = null!;
    private Label _weekLabel = null!;
    private OptionButton _posFilter = null!;
    private OptionButton _tierFilter = null!;
    private LineEdit _searchEdit = null!;
    private HBoxContainer _columnHeaders = null!;
    private VBoxContainer _playerList = null!;

    private Pos? _selectedPos;
    private FASigningTier? _selectedTier;
    private string _searchText = "";

    public override void _Ready()
    {
        _headerLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/HeaderHBox/HeaderLabel");
        _weekLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/HeaderHBox/WeekLabel");
        _posFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterHBox/PosFilter");
        _tierFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterHBox/TierFilter");
        _searchEdit = GetNode<LineEdit>("ScrollContainer/MarginContainer/VBox/FilterHBox/SearchEdit");
        _columnHeaders = GetNode<HBoxContainer>("ScrollContainer/MarginContainer/VBox/ColumnHeaders");
        _playerList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/PlayerList");

        SetupFilters();
        SetupColumnHeaders();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.FreeAgencyWeekProcessed += OnFAWeekProcessed;
            EventBus.Instance.FreeAgentSigned += OnFreeAgentSigned;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.FreeAgencyWeekProcessed -= OnFAWeekProcessed;
            EventBus.Instance.FreeAgentSigned -= OnFreeAgentSigned;
        }
    }

    private void SetupFilters()
    {
        // Position filter
        _posFilter.AddItem("All Positions", 0);
        int idx = 1;
        foreach (Pos pos in Enum.GetValues<Pos>())
        {
            _posFilter.AddItem(pos.ToString(), idx++);
        }

        // Tier filter
        _tierFilter.AddItem("All Tiers", 0);
        _tierFilter.AddItem("Elite (90+)", 1);
        _tierFilter.AddItem("Starter (80+)", 2);
        _tierFilter.AddItem("Depth (70+)", 3);
        _tierFilter.AddItem("Minimum", 4);
    }

    private void SetupColumnHeaders()
    {
        foreach (var child in _columnHeaders.GetChildren())
            child.QueueFree();

        AddHeaderCell("Player", 160);
        AddHeaderCell("Age", 40);
        AddHeaderCell("Pos", 50);
        AddHeaderCell("OVR", 45);
        AddHeaderCell("Tier", 70);
        AddHeaderCell("Est. Value", 100);
        AddHeaderCell("", 90); // button column
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;

        _weekLabel.Text = $"Week {gm.FreeAgencyWeekNumber}/4";

        // Clear list
        foreach (var child in _playerList.GetChildren())
            child.QueueFree();

        if (gm.Calendar.CurrentPhase != GamePhase.FreeAgency)
        {
            var msgLabel = new Label
            {
                Text = "Free agency is not currently active.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            msgLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _playerList.AddChild(msgLabel);
            return;
        }

        var freeAgents = gm.FreeAgency.GetFreeAgents(_selectedPos, _selectedTier);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            string search = _searchText.ToLowerInvariant();
            freeAgents = freeAgents
                .Where(p => p.FullName.ToLowerInvariant().Contains(search))
                .ToList();
        }

        if (freeAgents.Count == 0)
        {
            var noResults = new Label
            {
                Text = "No free agents match the current filters.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            noResults.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _playerList.AddChild(noResults);
            return;
        }

        // Limit display to 100 for performance
        foreach (var player in freeAgents.Take(100))
        {
            var row = CreatePlayerRow(player, gm);
            _playerList.AddChild(row);
        }

        if (freeAgents.Count > 100)
        {
            var moreLabel = new Label
            {
                Text = $"...and {freeAgents.Count - 100} more. Use filters to narrow results.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            moreLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            moreLabel.AddThemeFontSizeOverride("font_size", 12);
            _playerList.AddChild(moreLabel);
        }
    }

    private HBoxContainer CreatePlayerRow(Player player, GameManager gm)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        var tier = FreeAgencySystem.GetPlayerTier(player);
        long estimatedValue = gm.FreeAgency.GetEstimatedMarketValue(player);

        AddCell(hbox, $"{player.FirstName.Substring(0, 1)}. {player.LastName}", 160, HorizontalAlignment.Left);
        AddCell(hbox, player.Age.ToString(), 40, HorizontalAlignment.Center);
        AddCell(hbox, player.Position.ToString(), 50, HorizontalAlignment.Center);

        var ovrLabel = AddCell(hbox, player.Overall.ToString(), 45, HorizontalAlignment.Center);
        ovrLabel.AddThemeColorOverride("font_color", GetOvrColor(player.Overall));

        AddCell(hbox, tier.ToString(), 70, HorizontalAlignment.Center);
        AddCell(hbox, GameShell.FormatCurrency(estimatedValue) + "/yr", 100, HorizontalAlignment.Right);

        // Make Offer button
        var offerBtn = new Button
        {
            Text = "Make Offer",
            CustomMinimumSize = new Vector2(90, 0)
        };
        string playerId = player.Id;
        offerBtn.Pressed += () => OnMakeOfferPressed(playerId);
        hbox.AddChild(offerBtn);

        return hbox;
    }

    private void OnMakeOfferPressed(string playerId)
    {
        var scene = GD.Load<PackedScene>("res://Scenes/FreeAgency/NegotiationScreen.tscn");
        var screen = scene.Instantiate<NegotiationScreen>();
        screen.Initialize(playerId);
        GetTree().Root.AddChild(screen);
    }

    // --- Filters ---

    private void OnFilterChanged(long _index)
    {
        int posIdx = _posFilter.Selected;
        _selectedPos = posIdx == 0 ? null : (Pos)(posIdx - 1);

        int tierIdx = _tierFilter.Selected;
        _selectedTier = tierIdx == 0 ? null : (FASigningTier)(tierIdx - 1);

        Refresh();
    }

    private void OnSearchChanged(string newText)
    {
        _searchText = newText;
        Refresh();
    }

    // --- Signal Handlers ---

    private void OnFAWeekProcessed(int week) => Refresh();
    private void OnFreeAgentSigned(string playerId, string teamId, int years, long totalValue) => Refresh();

    // --- Helpers ---

    private void AddHeaderCell(string text, int width)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        _columnHeaders.AddChild(label);
    }

    private Label AddCell(HBoxContainer hbox, string text, int width, HorizontalAlignment align)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 0),
            HorizontalAlignment = align
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        hbox.AddChild(label);
        return label;
    }

    private static Color GetOvrColor(int ovr)
    {
        return ovr switch
        {
            >= 90 => new Color(0.3f, 1f, 0.3f),
            >= 80 => new Color(0.5f, 0.9f, 0.3f),
            >= 70 => new Color(0.9f, 0.9f, 0.3f),
            >= 60 => new Color(0.9f, 0.6f, 0.3f),
            _ => new Color(0.9f, 0.3f, 0.3f),
        };
    }
}
