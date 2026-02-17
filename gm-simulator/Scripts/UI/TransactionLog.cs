using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class TransactionLog : Control
{
    private OptionButton _typeFilter = null!;
    private OptionButton _teamFilter = null!;
    private OptionButton _seasonFilter = null!;
    private VBoxContainer _transactionList = null!;

    public override void _Ready()
    {
        _typeFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/TypeFilter");
        _teamFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/TeamFilter");
        _seasonFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/SeasonFilter");
        _transactionList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/TransactionList");

        PopulateFilters();

        _typeFilter.ItemSelected += _ => Refresh();
        _teamFilter.ItemSelected += _ => Refresh();
        _seasonFilter.ItemSelected += _ => Refresh();

        Refresh();
    }

    private void PopulateFilters()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Type filter
        _typeFilter.AddItem("All Types", 0);
        foreach (TransactionType t in Enum.GetValues<TransactionType>())
            _typeFilter.AddItem(FormatTypeName(t));

        // Team filter
        _teamFilter.AddItem("All Teams", 0);
        foreach (var team in gm.Teams.OrderBy(t => t.Abbreviation))
            _teamFilter.AddItem(team.Abbreviation);

        // Season filter
        _seasonFilter.AddItem("All Seasons", 0);
        var years = gm.TransactionLog.Select(t => t.Year).Distinct().OrderByDescending(y => y);
        foreach (int year in years)
            _seasonFilter.AddItem(year.ToString());
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Clear existing rows
        foreach (var child in _transactionList.GetChildren())
            child.QueueFree();

        var records = gm.TransactionLog.AsEnumerable();

        // Apply type filter
        if (_typeFilter.Selected > 0)
        {
            var selectedType = Enum.GetValues<TransactionType>()[_typeFilter.Selected - 1];
            records = records.Where(r => r.Type == selectedType);
        }

        // Apply team filter
        if (_teamFilter.Selected > 0)
        {
            string teamAbbr = _teamFilter.GetItemText(_teamFilter.Selected);
            var team = gm.Teams.FirstOrDefault(t => t.Abbreviation == teamAbbr);
            if (team != null)
                records = records.Where(r => r.TeamId == team.Id || r.OtherTeamId == team.Id);
        }

        // Apply season filter
        if (_seasonFilter.Selected > 0)
        {
            string yearText = _seasonFilter.GetItemText(_seasonFilter.Selected);
            if (int.TryParse(yearText, out int year))
                records = records.Where(r => r.Year == year);
        }

        // Sort newest first
        var sorted = records.OrderByDescending(r => r.Year).ThenByDescending(r => r.Week).ToList();

        // Build rows (cap at 200 for performance)
        int count = 0;
        foreach (var txn in sorted)
        {
            if (count++ >= 200) break;
            _transactionList.AddChild(CreateRow(txn, gm));
        }

        if (count == 0)
        {
            var empty = UIFactory.CreateEmptyState("No transactions found.");
            _transactionList.AddChild(empty);
        }
    }

    private static HBoxContainer CreateRow(TransactionRecord txn, GameManager gm)
    {
        var row = UIFactory.CreateRow(ThemeSpacing.XS);

        // Year/Week column
        UIFactory.AddCell(row, $"Y{txn.Year} W{txn.Week}", 70, ThemeFonts.Body, ThemeColors.TextTertiary);

        // Phase column
        UIFactory.AddCell(row, FormatPhaseName(txn.Phase), 90, ThemeFonts.Body, ThemeColors.TextTertiary);

        // Type badge
        UIFactory.AddCell(row, FormatTypeName(txn.Type), 80, ThemeFonts.ColumnHeader,
            ThemeColors.GetTransactionColor(txn.Type), HorizontalAlignment.Center);

        // Description (expands)
        var descLabel = UIFactory.AddCell(row, txn.Description, 0, ThemeFonts.Body,
            expandFill: true);
        descLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

        // Team abbreviation
        var teamAbbr = "";
        if (!string.IsNullOrEmpty(txn.TeamId))
        {
            var team = gm.GetTeam(txn.TeamId);
            if (team != null) teamAbbr = team.Abbreviation;
        }
        UIFactory.AddCell(row, teamAbbr, 50, ThemeFonts.Body, ThemeColors.TextSecondary,
            HorizontalAlignment.Right);

        return row;
    }

    private static Color GetTypeColor(TransactionType type) =>
        ThemeColors.GetTransactionColor(type);

    private static string FormatTypeName(TransactionType type)
    {
        return type switch
        {
            TransactionType.ContractExpired => "Expired",
            _ => type.ToString(),
        };
    }

    private static string FormatPhaseName(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.PostSeason => "Post-Season",
            GamePhase.CombineScouting => "Combine",
            GamePhase.FreeAgency => "Free Agency",
            GamePhase.PreDraft => "Pre-Draft",
            GamePhase.PostDraft => "Post-Draft",
            GamePhase.RegularSeason => "Regular",
            GamePhase.SuperBowl => "Super Bowl",
            _ => phase.ToString(),
        };
    }
}
