using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class LeagueLeaders : Control
{
    private OptionButton _categoryFilter = null!;
    private OptionButton _seasonFilter = null!;
    private VBoxContainer _content = null!;

    private static readonly string[] Categories = { "Passing", "Rushing", "Receiving", "Defense", "Kicking" };

    public override void _Ready()
    {
        _categoryFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/CategoryFilter");
        _seasonFilter = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/FilterBar/SeasonFilter");
        _content = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/LeaderboardContent");

        foreach (string cat in Categories)
            _categoryFilter.AddItem(cat);

        PopulateSeasonFilter();

        _categoryFilter.ItemSelected += _ => Refresh();
        _seasonFilter.ItemSelected += _ => Refresh();

        Refresh();
    }

    private void PopulateSeasonFilter()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var years = gm.Players
            .SelectMany(p => p.CareerStats.Keys)
            .Distinct()
            .OrderByDescending(y => y);

        foreach (int year in years)
            _seasonFilter.AddItem(year.ToString());
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        foreach (var child in _content.GetChildren())
            child.QueueFree();

        string yearText = _seasonFilter.GetItemText(_seasonFilter.Selected);
        if (!int.TryParse(yearText, out int year)) return;

        string category = Categories[_categoryFilter.Selected];

        var playersWithStats = gm.Players
            .Where(p => p.CareerStats.ContainsKey(year))
            .Select(p => (Player: p, Stats: p.CareerStats[year]))
            .ToList();

        switch (category)
        {
            case "Passing":
                AddLeaderboard("Passing Yards", playersWithStats, e => e.Stats.PassingYards, p => IsOffense(p));
                AddLeaderboard("Passing TDs", playersWithStats, e => e.Stats.PassingTDs, p => IsOffense(p));
                AddLeaderboard("Passer Rating", playersWithStats, e => e.Stats.PasserRating, p => IsOffense(p), "F1");
                AddLeaderboard("Completions", playersWithStats, e => e.Stats.Completions, p => IsOffense(p));
                break;
            case "Rushing":
                AddLeaderboard("Rushing Yards", playersWithStats, e => e.Stats.RushingYards);
                AddLeaderboard("Rushing TDs", playersWithStats, e => e.Stats.RushingTDs);
                AddLeaderboard("Rush Attempts", playersWithStats, e => e.Stats.RushAttempts);
                break;
            case "Receiving":
                AddLeaderboard("Receiving Yards", playersWithStats, e => e.Stats.ReceivingYards);
                AddLeaderboard("Receiving TDs", playersWithStats, e => e.Stats.ReceivingTDs);
                AddLeaderboard("Receptions", playersWithStats, e => e.Stats.Receptions);
                break;
            case "Defense":
                AddLeaderboard("Sacks", playersWithStats, e => e.Stats.Sacks, p => IsDefense(p), "F1");
                AddLeaderboard("Interceptions", playersWithStats, e => e.Stats.InterceptionsDef, p => IsDefense(p));
                AddLeaderboard("Total Tackles", playersWithStats, e => e.Stats.TotalTackles, p => IsDefense(p));
                AddLeaderboard("Forced Fumbles", playersWithStats, e => e.Stats.ForcedFumbles, p => IsDefense(p));
                break;
            case "Kicking":
                AddLeaderboard("FG Made", playersWithStats, e => e.Stats.FGMade, p => IsKicker(p));
                AddLeaderboard("FG %", playersWithStats,
                    e => e.Stats.FGAttempted > 0 ? (float)e.Stats.FGMade / e.Stats.FGAttempted * 100f : 0f,
                    p => IsKicker(p) && p.Stats.FGAttempted >= 10, "F1");
                AddLeaderboard("Punt Average", playersWithStats, e => e.Stats.PuntAverage, p => IsPunter(p), "F1");
                break;
        }
    }

    private void AddLeaderboard<T>(
        string title,
        List<(Player Player, SeasonStats Stats)> all,
        Func<(Player Player, SeasonStats Stats), T> valueSelector,
        Func<(Player Player, SeasonStats Stats), bool>? filter = null,
        string format = "N0") where T : IComparable<T>
    {
        var gm = GameManager.Instance!;
        var filtered = filter != null ? all.Where(filter) : all;
        var top10 = filtered
            .OrderByDescending(e => valueSelector(e))
            .Take(10)
            .ToList();

        if (top10.Count == 0) return;

        var section = UIFactory.CreateSection(ThemeSpacing.XXS);

        // Section header
        var header = UIFactory.CreateSubtitle(title);
        section.AddChild(header);

        // Column headers
        var colHeader = CreateHeaderRow();
        section.AddChild(colHeader);

        // Player rows
        int rank = 1;
        foreach (var entry in top10)
        {
            var team = gm.GetTeam(entry.Player.TeamId ?? "");
            string teamAbbr = team?.Abbreviation ?? "FA";
            object val = valueSelector(entry)!;
            string valStr = val is float f ? f.ToString(format) : val is double d ? d.ToString(format) : val.ToString() ?? "";

            var row = CreatePlayerRow(rank, entry.Player, teamAbbr, valStr);
            section.AddChild(row);
            rank++;
        }

        _content.AddChild(section);
    }

    private static HBoxContainer CreateHeaderRow()
    {
        var row = UIFactory.CreateRow(ThemeSpacing.XXS + 2);

        UIFactory.AddCell(row, "#", 30, ThemeFonts.ColumnHeader, ThemeColors.TextTertiary, HorizontalAlignment.Right);
        UIFactory.AddCell(row, "Player", 0, ThemeFonts.ColumnHeader, ThemeColors.TextTertiary, HorizontalAlignment.Left, expandFill: true);
        UIFactory.AddCell(row, "Team", 50, ThemeFonts.ColumnHeader, ThemeColors.TextTertiary, HorizontalAlignment.Center);
        UIFactory.AddCell(row, "Pos", 40, ThemeFonts.ColumnHeader, ThemeColors.TextTertiary, HorizontalAlignment.Center);
        UIFactory.AddCell(row, "Value", 70, ThemeFonts.ColumnHeader, ThemeColors.TextTertiary, HorizontalAlignment.Right);

        return row;
    }

    private static HBoxContainer CreatePlayerRow(int rank, Player player, string teamAbbr, string value)
    {
        var row = UIFactory.CreateRow(ThemeSpacing.XXS + 2);

        UIFactory.AddCell(row, rank.ToString(), 30, ThemeFonts.Body, ThemeColors.TextTertiary, HorizontalAlignment.Right);

        // Player name as clickable button
        var nameBtn = new Button
        {
            Text = $"{player.FirstName} {player.LastName}",
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        nameBtn.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        nameBtn.Alignment = HorizontalAlignment.Left;
        string pid = player.Id;
        nameBtn.Pressed += () => EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSelected, pid);
        row.AddChild(nameBtn);

        UIFactory.AddCell(row, teamAbbr, 50, ThemeFonts.Body, ThemeColors.TextSecondary, HorizontalAlignment.Center);
        UIFactory.AddCell(row, player.Position.ToString(), 40, ThemeFonts.Body, ThemeColors.TextSecondary, HorizontalAlignment.Center);
        UIFactory.AddCell(row, value, 70, ThemeFonts.BodyLarge, ThemeColors.TextPrimary, HorizontalAlignment.Right);

        return row;
    }


    private static bool IsOffense((Player Player, SeasonStats Stats) e) =>
        e.Player.Position is Pos.QB or Pos.HB or Pos.FB or Pos.WR or Pos.TE or Pos.LT or Pos.LG or Pos.C or Pos.RG or Pos.RT;

    private static bool IsDefense((Player Player, SeasonStats Stats) e) =>
        e.Player.Position is Pos.EDGE or Pos.DT or Pos.MLB or Pos.OLB or Pos.CB or Pos.FS or Pos.SS;

    private static bool IsKicker((Player Player, SeasonStats Stats) e) =>
        e.Player.Position == Pos.K;

    private static bool IsPunter((Player Player, SeasonStats Stats) e) =>
        e.Player.Position == Pos.P;
}
