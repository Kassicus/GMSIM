using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class FranchiseTagWindow : Window
{
    private Label _statusLabel = null!;
    private HBoxContainer _columnHeaders = null!;
    private VBoxContainer _playerList = null!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("MarginContainer/VBox/StatusLabel");
        _columnHeaders = GetNode<HBoxContainer>("MarginContainer/VBox/ColumnHeaders");
        _playerList = GetNode<VBoxContainer>("MarginContainer/VBox/ScrollContainer/PlayerList");

        SetupColumnHeaders();
        Refresh();
    }

    private void SetupColumnHeaders()
    {
        AddHeaderCell("Player", 150);
        AddHeaderCell("Pos", 50);
        AddHeaderCell("Age", 40);
        AddHeaderCell("OVR", 45);
        AddHeaderCell("Franchise Tag Cost", 130);
        AddHeaderCell("", 100);
        AddHeaderCell("", 100);
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Show current tag status
        string status = "";
        if (team.FranchiseTagUsed)
        {
            var tagged = gm.GetPlayer(team.TaggedPlayerId ?? "");
            status += $"Franchise tag used on: {tagged?.FullName ?? "Unknown"}. ";
        }
        if (team.TransitionTagUsed)
        {
            var tagged = gm.GetPlayer(team.TransitionTagPlayerId ?? "");
            status += $"Transition tag used on: {tagged?.FullName ?? "Unknown"}.";
        }
        if (string.IsNullOrEmpty(status))
            status = "No tags used this year.";
        _statusLabel.Text = status;

        // Clear list
        foreach (var child in _playerList.GetChildren())
            child.QueueFree();

        // Find players with expiring contracts
        var expiringPlayers = gm.GetTeamPlayers(team.Id)
            .Where(p => p.CurrentContract != null
                       && !p.CurrentContract.Years.Any(y => y.Year >= gm.Calendar.CurrentYear + 1))
            .OrderByDescending(p => p.Overall)
            .ToList();

        if (expiringPlayers.Count == 0)
        {
            var noPlayers = new Label
            {
                Text = "No players with expiring contracts.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            noPlayers.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _playerList.AddChild(noPlayers);
            return;
        }

        foreach (var player in expiringPlayers)
        {
            var row = CreatePlayerRow(player, gm, team);
            _playerList.AddChild(row);
        }
    }

    private HBoxContainer CreatePlayerRow(Player player, GameManager gm, Team team)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        long franchiseTagCost = gm.SalaryCapManager.CalculateFranchiseTagValue(player.Position);
        long transitionTagCost = gm.SalaryCapManager.CalculateTransitionTagValue(player.Position);

        AddCell(hbox, player.FullName, 150, HorizontalAlignment.Left);
        AddCell(hbox, player.Position.ToString(), 50, HorizontalAlignment.Center);
        AddCell(hbox, player.Age.ToString(), 40, HorizontalAlignment.Center);
        AddCell(hbox, player.Overall.ToString(), 45, HorizontalAlignment.Center);
        AddCell(hbox, GameShell.FormatCurrency(franchiseTagCost), 130, HorizontalAlignment.Right);

        // Franchise tag button
        var ftBtn = new Button
        {
            Text = "Franchise",
            CustomMinimumSize = new Vector2(100, 0),
            Disabled = team.FranchiseTagUsed
        };
        string playerId = player.Id;
        ftBtn.Pressed += () => OnApplyTag(playerId, false);
        hbox.AddChild(ftBtn);

        // Transition tag button
        var ttBtn = new Button
        {
            Text = "Transition",
            CustomMinimumSize = new Vector2(100, 0),
            Disabled = team.TransitionTagUsed
        };
        ttBtn.Pressed += () => OnApplyTag(playerId, true);
        hbox.AddChild(ttBtn);

        return hbox;
    }

    private void OnApplyTag(string playerId, bool isTransition)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var result = gm.RosterManager.ApplyFranchiseTag(playerId, gm.PlayerTeamId, isTransition);

        if (result.Success)
        {
            _statusLabel.Text = result.Message;
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
        }
        else
        {
            _statusLabel.Text = result.Message;
            _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
        }

        Refresh();
    }

    private void OnClosePressed() => QueueFree();

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

    private void AddCell(HBoxContainer hbox, string text, int width, HorizontalAlignment align)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 0),
            HorizontalAlignment = align
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        hbox.AddChild(label);
    }
}
