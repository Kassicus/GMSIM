using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class TeamHistory : Control
{
    private OptionButton _teamSelector = null!;
    private VBoxContainer _content = null!;

    public override void _Ready()
    {
        _teamSelector = GetNode<OptionButton>("ScrollContainer/MarginContainer/VBox/TeamSelector");
        _content = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/HistoryContent");

        PopulateTeamSelector();

        _teamSelector.ItemSelected += _ => Refresh();
        Refresh();
    }

    private void PopulateTeamSelector()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int defaultIdx = 0;
        int idx = 0;
        foreach (var team in gm.Teams.OrderBy(t => t.FullName))
        {
            _teamSelector.AddItem($"{team.Abbreviation} - {team.FullName}");
            _teamSelector.SetItemMetadata(idx, team.Id);
            if (team.Id == gm.PlayerTeamId)
                defaultIdx = idx;
            idx++;
        }
        _teamSelector.Selected = defaultIdx;
    }

    private string GetSelectedTeamId()
    {
        return _teamSelector.GetItemMetadata(_teamSelector.Selected).AsString();
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        foreach (var child in _content.GetChildren())
            child.QueueFree();

        string teamId = GetSelectedTeamId();

        BuildSeasonRecords(gm, teamId);
        BuildDraftHistory(gm, teamId);
        BuildAwardsHistory(gm, teamId);
    }

    private void BuildSeasonRecords(GameManager gm, string teamId)
    {
        var section = CreateSection("SEASON RECORDS");

        if (gm.SeasonHistory.Count == 0)
        {
            AddEmptyMessage(section, "No completed seasons yet.");
            _content.AddChild(section);
            return;
        }

        // Column headers
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 8);
        AddLabel(headerRow, "Year", 60, ThemeColors.TextTertiary);
        AddLabel(headerRow, "Record", 80, ThemeColors.TextTertiary);
        AddLabel(headerRow, "PF", 60, ThemeColors.TextTertiary, HorizontalAlignment.Right);
        AddLabel(headerRow, "PA", 60, ThemeColors.TextTertiary, HorizontalAlignment.Right);
        AddLabel(headerRow, "", 30, ThemeColors.TextTertiary); // Champion star
        section.AddChild(headerRow);

        foreach (var season in gm.SeasonHistory.OrderByDescending(s => s.Year))
        {
            int wins = 0, losses = 0, ties = 0, pf = 0, pa = 0;
            foreach (var game in season.Games.Where(g => g.IsCompleted && (g.HomeTeamId == teamId || g.AwayTeamId == teamId)))
            {
                bool isHome = game.HomeTeamId == teamId;
                int teamScore = isHome ? game.HomeScore : game.AwayScore;
                int oppScore = isHome ? game.AwayScore : game.HomeScore;
                pf += teamScore;
                pa += oppScore;

                if (teamScore > oppScore) wins++;
                else if (teamScore < oppScore) losses++;
                else ties++;
            }

            bool isChamp = season.ChampionTeamId == teamId;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            AddLabel(row, season.Year.ToString(), 60, ThemeColors.TextSecondary);
            string record = ties > 0 ? $"{wins}-{losses}-{ties}" : $"{wins}-{losses}";
            AddLabel(row, record, 80, ThemeColors.TextPrimary);
            AddLabel(row, pf.ToString(), 60, ThemeColors.TextSecondary, HorizontalAlignment.Right);
            AddLabel(row, pa.ToString(), 60, ThemeColors.TextSecondary, HorizontalAlignment.Right);
            AddLabel(row, isChamp ? "CHAMP" : "", 30, ThemeColors.RatingElite);
            section.AddChild(row);
        }

        _content.AddChild(section);
    }

    private void BuildDraftHistory(GameManager gm, string teamId)
    {
        var section = CreateSection("DRAFT HISTORY");

        var picks = gm.AllDraftPicks
            .Where(dp => dp.OriginalTeamId == teamId && dp.IsUsed && dp.SelectedPlayerId != null)
            .OrderByDescending(dp => dp.Year)
            .ThenBy(dp => dp.Round)
            .ToList();

        if (picks.Count == 0)
        {
            AddEmptyMessage(section, "No draft picks used yet.");
            _content.AddChild(section);
            return;
        }

        // Column headers
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 8);
        AddLabel(headerRow, "Year", 60, ThemeColors.TextTertiary);
        AddLabel(headerRow, "Rd", 30, ThemeColors.TextTertiary);
        AddLabel(headerRow, "Pick", 50, ThemeColors.TextTertiary, HorizontalAlignment.Right);
        AddLabel(headerRow, "Player", 0, ThemeColors.TextTertiary, HorizontalAlignment.Left, true);
        AddLabel(headerRow, "Pos", 40, ThemeColors.TextTertiary);
        section.AddChild(headerRow);

        foreach (var dp in picks)
        {
            var player = gm.GetPlayer(dp.SelectedPlayerId!);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            AddLabel(row, dp.Year.ToString(), 60, ThemeColors.TextSecondary);
            AddLabel(row, $"R{dp.Round}", 30, ThemeColors.TextSecondary);
            AddLabel(row, dp.OverallNumber?.ToString() ?? "-", 50, ThemeColors.TextSecondary, HorizontalAlignment.Right);

            if (player != null)
            {
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
                AddLabel(row, player.Position.ToString(), 40, ThemeColors.TextSecondary);
            }
            else
            {
                AddLabel(row, "Unknown", 0, ThemeColors.TextTertiary, HorizontalAlignment.Left, true);
                AddLabel(row, "-", 40, ThemeColors.TextTertiary);
            }

            section.AddChild(row);
        }

        _content.AddChild(section);
    }

    private void BuildAwardsHistory(GameManager gm, string teamId)
    {
        var section = CreateSection("AWARDS");

        var entries = new List<(int Year, string Award, string PlayerName, string PlayerId)>();

        foreach (var awards in gm.AllAwards)
        {
            CheckAward(gm, awards.Year, "MVP", awards.MvpId, teamId, entries);
            CheckAward(gm, awards.Year, "DPOY", awards.DpoyId, teamId, entries);
            CheckAward(gm, awards.Year, "OROY", awards.OroyId, teamId, entries);
            CheckAward(gm, awards.Year, "DROY", awards.DroyId, teamId, entries);

            foreach (string pid in awards.FirstTeamAllPro.Concat(awards.SecondTeamAllPro))
            {
                var p = gm.GetPlayer(pid);
                if (p?.TeamId == teamId)
                {
                    bool first = awards.FirstTeamAllPro.Contains(pid);
                    entries.Add((awards.Year, first ? "1st Team All-Pro" : "2nd Team All-Pro",
                        $"{p.FirstName} {p.LastName}", pid));
                }
            }

            foreach (string pid in awards.ProBowlIds)
            {
                var p = gm.GetPlayer(pid);
                if (p?.TeamId == teamId)
                    entries.Add((awards.Year, "Pro Bowl", $"{p.FirstName} {p.LastName}", pid));
            }
        }

        if (entries.Count == 0)
        {
            AddEmptyMessage(section, "No awards won yet.");
            _content.AddChild(section);
            return;
        }

        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 8);
        AddLabel(headerRow, "Year", 60, ThemeColors.TextTertiary);
        AddLabel(headerRow, "Award", 140, ThemeColors.TextTertiary);
        AddLabel(headerRow, "Player", 0, ThemeColors.TextTertiary, HorizontalAlignment.Left, true);
        section.AddChild(headerRow);

        foreach (var entry in entries.OrderByDescending(e => e.Year).ThenBy(e => e.Award))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            AddLabel(row, entry.Year.ToString(), 60, ThemeColors.TextSecondary);

            var awardColor = entry.Award is "MVP" or "DPOY" ? ThemeColors.RatingElite : ThemeColors.TextSecondary;
            AddLabel(row, entry.Award, 140, awardColor);

            var nameBtn = new Button
            {
                Text = entry.PlayerName,
                Flat = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            nameBtn.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
            nameBtn.Alignment = HorizontalAlignment.Left;
            string pid = entry.PlayerId;
            nameBtn.Pressed += () => EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSelected, pid);
            row.AddChild(nameBtn);

            section.AddChild(row);
        }

        _content.AddChild(section);
    }

    private static void CheckAward(GameManager gm, int year, string awardName, string? playerId, string teamId,
        List<(int Year, string Award, string PlayerName, string PlayerId)> entries)
    {
        if (string.IsNullOrEmpty(playerId)) return;
        var p = gm.GetPlayer(playerId);
        if (p?.TeamId == teamId)
            entries.Add((year, awardName, $"{p.FirstName} {p.LastName}", playerId));
    }

    private static VBoxContainer CreateSection(string title)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", ThemeSpacing.RowGap);

        var header = UIFactory.CreateSectionTitle(title);
        section.AddChild(header);

        return section;
    }

    private static void AddEmptyMessage(VBoxContainer section, string message)
    {
        var label = UIFactory.CreateEmptyState(message);
        section.AddChild(label);
    }

    private static void AddLabel(HBoxContainer row, string text, int minWidth, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left, bool expand = false)
    {
        UIFactory.AddCell(row, text, minWidth, ThemeFonts.Body, color, align, expand);
    }
}
