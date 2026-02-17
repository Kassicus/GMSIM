using Godot;
using GMSimulator.Core;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class Dashboard : Control
{
    private Label _headerLabel = null!;
    private Label _recordValue = null!;
    private Label _phaseValue = null!;
    private Label _yearValue = null!;
    private Label _capValue = null!;
    private Label _rosterValue = null!;
    private Label _coachValue = null!;
    private VBoxContainer _topPlayersList = null!;

    public override void _Ready()
    {
        _headerLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/HeaderLabel");
        _recordValue = GetNode<Label>("ScrollContainer/MarginContainer/VBox/InfoGrid/RecordValue");
        _phaseValue = GetNode<Label>("ScrollContainer/MarginContainer/VBox/InfoGrid/PhaseValue");
        _yearValue = GetNode<Label>("ScrollContainer/MarginContainer/VBox/InfoGrid/YearValue");
        _capValue = GetNode<Label>("ScrollContainer/MarginContainer/VBox/InfoGrid/CapValue");
        _rosterValue = GetNode<Label>("ScrollContainer/MarginContainer/VBox/InfoGrid/RosterValue");
        _coachValue = GetNode<Label>("ScrollContainer/MarginContainer/VBox/InfoGrid/CoachValue");
        _topPlayersList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/TopPlayersList");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
            EventBus.Instance.PhaseChanged += OnPhaseChanged;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced -= OnWeekAdvanced;
            EventBus.Instance.PhaseChanged -= OnPhaseChanged;
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        _headerLabel.Text = team.FullName.ToUpper();

        var record = team.CurrentRecord;
        _recordValue.Text = $"{record.Wins}-{record.Losses}-{record.Ties}";
        _phaseValue.Text = gm.Calendar.GetPhaseDisplayName();
        _yearValue.Text = gm.Calendar.CurrentYear.ToString();
        _capValue.Text = $"{GameShell.FormatCurrency(team.CapSpace)} (Used: {GameShell.FormatCurrency(team.CurrentCapUsed)})";

        int activeCount = team.PlayerIds.Count;
        int psCount = team.PracticeSquadIds.Count;
        _rosterValue.Text = $"{activeCount}/53 Active, {psCount}/16 PS";

        // Head coach
        var headCoach = team.HeadCoachId != null ? gm.GetCoach(team.HeadCoachId) : null;
        _coachValue.Text = headCoach != null ? headCoach.FullName : "Vacant";

        // Top players
        RefreshTopPlayers(gm, team);
    }

    private void RefreshTopPlayers(GameManager gm, Models.Team team)
    {
        // Clear existing
        foreach (var child in _topPlayersList.GetChildren())
        {
            child.QueueFree();
        }

        var topPlayers = gm.GetTeamActivePlayers(team.Id)
            .OrderByDescending(p => p.Overall)
            .Take(10);

        foreach (var player in topPlayers)
        {
            var label = UIFactory.CreateLabel(
                $"{player.Position} {player.FullName} - OVR {player.Overall} ({player.Archetype}) Age {player.Age}",
                ThemeFonts.BodyLarge,
                ThemeColors.GetRatingColor(player.Overall));

            _topPlayersList.AddChild(label);
        }
    }

    private void OnWeekAdvanced(int year, int week) => Refresh();
    private void OnPhaseChanged(int phase) => Refresh();
}
