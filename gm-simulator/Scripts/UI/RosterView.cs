using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Components;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class RosterView : Control
{
    private OptionButton _posGroupFilter = null!;
    private OptionButton _statusFilter = null!;
    private OptionButton _sortOption = null!;
    private Button _sortDirBtn = null!;
    private Label _rosterCountLabel = null!;
    private VBoxContainer _playerList = null!;

    private enum SortField { Overall, Name, Age, Salary, Position }
    private SortField _currentSort = SortField.Overall;
    private bool _sortDescending = true;

    private static readonly Pos[] OffensePositions =
        { Pos.QB, Pos.HB, Pos.FB, Pos.WR, Pos.TE,
          Pos.LT, Pos.LG, Pos.C, Pos.RG, Pos.RT };
    private static readonly Pos[] DefensePositions =
        { Pos.EDGE, Pos.DT, Pos.MLB, Pos.OLB,
          Pos.CB, Pos.FS, Pos.SS };
    private static readonly Pos[] SpecialTeamsPositions =
        { Pos.K, Pos.P, Pos.LS };

    public override void _Ready()
    {
        _posGroupFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/PosGroupFilter");
        _statusFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/StatusFilter");
        _sortOption = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/SortOption");
        _sortDirBtn = GetNode<Button>("ScrollContainer/MarginContainer/VBox/FilterBar/SortDirBtn");
        _rosterCountLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/RosterCountLabel");
        _playerList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/PlayerList");

        // Populate filter options
        _posGroupFilter.AddItem("All Positions");
        _posGroupFilter.AddItem("Offense");
        _posGroupFilter.AddItem("Defense");
        _posGroupFilter.AddItem("Special Teams");

        _statusFilter.AddItem("All Status");
        _statusFilter.AddItem("Active");
        _statusFilter.AddItem("Practice Squad");
        _statusFilter.AddItem("Injured Reserve");

        _sortOption.AddItem("Overall");
        _sortOption.AddItem("Name");
        _sortOption.AddItem("Age");
        _sortOption.AddItem("Salary");
        _sortOption.AddItem("Position");

        // Connect filter change signals
        _posGroupFilter.ItemSelected += _ => Refresh();
        _statusFilter.ItemSelected += _ => Refresh();
        _sortOption.ItemSelected += idx =>
        {
            _currentSort = (SortField)idx;
            Refresh();
        };
        _sortDirBtn.Pressed += () =>
        {
            _sortDescending = !_sortDescending;
            _sortDirBtn.Text = _sortDescending ? "DESC" : "ASC";
            Refresh();
        };

        // Connect to EventBus
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

        // Get all players on this team
        var players = gm.GetTeamPlayers(team.Id);
        int activeCount = players.Count(p => p.RosterStatus == RosterStatus.Active53);
        int psCount = players.Count(p => p.RosterStatus == RosterStatus.PracticeSquad);
        int irCount = players.Count(p => p.RosterStatus == RosterStatus.InjuredReserve);

        _rosterCountLabel.Text = $"{activeCount}/53 Active | {psCount}/16 PS | {irCount} IR";

        // Apply filters
        players = ApplyFilters(players);

        // Apply sort
        players = ApplySort(players, gm.Calendar.CurrentYear);

        // Clear and repopulate
        foreach (var child in _playerList.GetChildren())
            child.QueueFree();

        foreach (var player in players)
        {
            var row = PlayerRowItem.Create(player, gm.Calendar.CurrentYear);
            _playerList.AddChild(row);
        }
    }

    private List<Player> ApplyFilters(List<Player> players)
    {
        // Position group filter
        int posGroup = _posGroupFilter.Selected;
        if (posGroup == 1) // Offense
            players = players.Where(p => OffensePositions.Contains(p.Position)).ToList();
        else if (posGroup == 2) // Defense
            players = players.Where(p => DefensePositions.Contains(p.Position)).ToList();
        else if (posGroup == 3) // Special Teams
            players = players.Where(p => SpecialTeamsPositions.Contains(p.Position)).ToList();

        // Status filter
        int statusIdx = _statusFilter.Selected;
        if (statusIdx == 1) // Active
            players = players.Where(p => p.RosterStatus == RosterStatus.Active53).ToList();
        else if (statusIdx == 2) // PS
            players = players.Where(p => p.RosterStatus == RosterStatus.PracticeSquad).ToList();
        else if (statusIdx == 3) // IR
            players = players.Where(p => p.RosterStatus == RosterStatus.InjuredReserve).ToList();

        return players;
    }

    private List<Player> ApplySort(List<Player> players, int currentYear)
    {
        IOrderedEnumerable<Player> sorted = _currentSort switch
        {
            SortField.Name => _sortDescending
                ? players.OrderByDescending(p => p.LastName)
                : players.OrderBy(p => p.LastName),
            SortField.Age => _sortDescending
                ? players.OrderByDescending(p => p.Age)
                : players.OrderBy(p => p.Age),
            SortField.Salary => _sortDescending
                ? players.OrderByDescending(p => p.CurrentContract?.GetCapHit(currentYear) ?? 0)
                : players.OrderBy(p => p.CurrentContract?.GetCapHit(currentYear) ?? 0),
            SortField.Position => _sortDescending
                ? players.OrderByDescending(p => p.Position)
                : players.OrderBy(p => p.Position),
            _ => _sortDescending
                ? players.OrderByDescending(p => p.Overall)
                : players.OrderBy(p => p.Overall),
        };

        return sorted.ToList();
    }

    private void OnRosterChanged(string playerId, string teamId) => Refresh();
    private void OnWeekAdvanced(int year, int week) => Refresh();
}
