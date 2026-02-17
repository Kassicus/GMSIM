using Godot;
using GMSimulator.Core;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class FreeAgencyFeed : PanelContainer
{
    private VBoxContainer _feedList = null!;
    private const int MaxEntries = 50;

    public override void _Ready()
    {
        _feedList = GetNode<VBoxContainer>("MarginContainer/VBox/ScrollContainer/FeedList");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.FreeAgentSigned += OnFreeAgentSigned;
            EventBus.Instance.FranchiseTagApplied += OnFranchiseTagApplied;
            EventBus.Instance.ContractExtended += OnContractExtended;
        }
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.FreeAgentSigned -= OnFreeAgentSigned;
            EventBus.Instance.FranchiseTagApplied -= OnFranchiseTagApplied;
            EventBus.Instance.ContractExtended -= OnContractExtended;
        }
    }

    private void OnFreeAgentSigned(string playerId, string teamId, int years, long totalValue)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var player = gm.GetPlayer(playerId);
        var team = gm.GetTeam(teamId);
        if (player == null || team == null) return;

        string valueStr = GameShell.FormatCurrency(totalValue);
        AddFeedEntry(
            $"{player.Position} {player.FirstName.Substring(0, 1)}. {player.LastName} signed with {team.Abbreviation} ({years}yr/{valueStr})",
            GetTeamColor(teamId, gm));
    }

    private void OnFranchiseTagApplied(string playerId, string teamId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var player = gm.GetPlayer(playerId);
        var team = gm.GetTeam(teamId);
        if (player == null || team == null) return;

        AddFeedEntry(
            $"{player.Position} {player.FirstName.Substring(0, 1)}. {player.LastName} franchise tagged by {team.Abbreviation}",
            GetTeamColor(teamId, gm));
    }

    private void OnContractExtended(string playerId, string teamId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var player = gm.GetPlayer(playerId);
        var team = gm.GetTeam(teamId);
        if (player == null || team == null) return;

        AddFeedEntry(
            $"{player.Position} {player.FirstName.Substring(0, 1)}. {player.LastName} extended by {team.Abbreviation}",
            GetTeamColor(teamId, gm));
    }

    private void AddFeedEntry(string text, Color color)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        label.AddThemeColorOverride("font_color", color);

        // Add at top
        _feedList.AddChild(label);
        _feedList.MoveChild(label, 0);

        // Trim old entries
        while (_feedList.GetChildCount() > MaxEntries)
        {
            var last = _feedList.GetChild(_feedList.GetChildCount() - 1);
            _feedList.RemoveChild(last);
            last.QueueFree();
        }
    }

    private Color GetTeamColor(string teamId, GameManager gm)
    {
        if (teamId == gm.PlayerTeamId)
            return ThemeColors.Success;

        // Check if division rival
        var playerTeam = gm.GetPlayerTeam();
        var otherTeam = gm.GetTeam(teamId);
        if (playerTeam != null && otherTeam != null
            && playerTeam.Conference == otherTeam.Conference
            && playerTeam.Division == otherTeam.Division)
        {
            return ThemeColors.Danger;
        }

        return ThemeColors.TextPrimary;
    }
}
