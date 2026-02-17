using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class Standings : Control
{
    private Button _afcBtn = null!;
    private Button _nfcBtn = null!;
    private Button _bothBtn = null!;
    private VBoxContainer _divisionList = null!;
    private VBoxContainer _playoffPicture = null!;

    private enum ViewMode { AFC, NFC, Both }
    private ViewMode _mode = ViewMode.AFC;

    public override void _Ready()
    {
        _afcBtn = GetNode<Button>("ScrollContainer/MarginContainer/VBox/ConferenceToggle/AFCBtn");
        _nfcBtn = GetNode<Button>("ScrollContainer/MarginContainer/VBox/ConferenceToggle/NFCBtn");
        _bothBtn = GetNode<Button>("ScrollContainer/MarginContainer/VBox/ConferenceToggle/BothBtn");
        _divisionList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/DivisionList");
        _playoffPicture = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/PlayoffPicture");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
            EventBus.Instance.GameCompleted += OnGameCompleted;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced -= OnWeekAdvanced;
            EventBus.Instance.GameCompleted -= OnGameCompleted;
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;

        // Update toggle states
        _afcBtn.ButtonPressed = _mode == ViewMode.AFC;
        _nfcBtn.ButtonPressed = _mode == ViewMode.NFC;
        _bothBtn.ButtonPressed = _mode == ViewMode.Both;

        // Clear divisions
        foreach (var child in _divisionList.GetChildren())
            child.QueueFree();

        // Build division groups
        var conferences = _mode switch
        {
            ViewMode.AFC => new[] { Conference.AFC },
            ViewMode.NFC => new[] { Conference.NFC },
            _ => new[] { Conference.AFC, Conference.NFC }
        };

        foreach (var conf in conferences)
        {
            foreach (Division div in Enum.GetValues<Division>())
            {
                var divTeams = gm.Teams
                    .Where(t => t.Conference == conf && t.Division == div)
                    .OrderByDescending(t => GetWinPct(t))
                    .ThenByDescending(t => t.CurrentRecord.Wins)
                    .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
                    .ToList();

                if (divTeams.Count == 0) continue;

                var section = CreateDivisionSection($"{conf} {div}", divTeams, gm);
                _divisionList.AddChild(section);
            }
        }

        // Playoff picture
        PopulatePlayoffPicture(gm, conferences);
    }

    private VBoxContainer CreateDivisionSection(string divName, List<Team> teams, GameManager gm)
    {
        var section = UIFactory.CreateSection(2);

        // Division header
        var header = UIFactory.CreateSubtitle(divName);
        section.AddChild(header);

        // Column headers
        var colHeader = CreateStandingsRow("Team", "W", "L", "T", "PCT", "PF", "PA", "DIFF", true);
        section.AddChild(colHeader);

        var sep = new HSeparator();
        section.AddChild(sep);

        // Team rows
        foreach (var team in teams)
        {
            var rec = team.CurrentRecord;
            int diff = rec.PointsFor - rec.PointsAgainst;
            string diffStr = diff >= 0 ? $"+{diff}" : diff.ToString();
            string pctStr = GetWinPct(team).ToString("0.000");

            var row = CreateStandingsRow(
                team.Abbreviation,
                rec.Wins.ToString(),
                rec.Losses.ToString(),
                rec.Ties.ToString(),
                pctStr,
                rec.PointsFor.ToString(),
                rec.PointsAgainst.ToString(),
                diffStr,
                false);

            // Highlight player's team
            if (team.Id == gm.PlayerTeamId)
            {
                var panel = new PanelContainer();
                panel.AddThemeStyleboxOverride("panel", ThemeStyles.HighlightRow());
                panel.AddChild(row);
                section.AddChild(panel);
            }
            else
            {
                section.AddChild(row);
            }
        }

        return section;
    }

    private HBoxContainer CreateStandingsRow(string team, string w, string l, string t, string pct,
        string pf, string pa, string diff, bool isHeader)
    {
        var row = UIFactory.CreateRow(ThemeSpacing.XXS);

        int fontSize = isHeader ? ThemeFonts.ColumnHeader : ThemeFonts.BodyLarge;
        var color = isHeader ? ThemeColors.TextTertiary : ThemeColors.TextPrimary;

        UIFactory.AddCell(row, team, 70, fontSize, color, HorizontalAlignment.Left);
        UIFactory.AddCell(row, w, 35, fontSize, color, HorizontalAlignment.Center);
        UIFactory.AddCell(row, l, 35, fontSize, color, HorizontalAlignment.Center);
        UIFactory.AddCell(row, t, 25, fontSize, color, HorizontalAlignment.Center);
        UIFactory.AddCell(row, pct, 60, fontSize, color, HorizontalAlignment.Center);
        UIFactory.AddCell(row, pf, 50, fontSize, color, HorizontalAlignment.Center);
        UIFactory.AddCell(row, pa, 50, fontSize, color, HorizontalAlignment.Center);

        var diffColor = isHeader ? color : (diff.StartsWith('+') ? ThemeColors.Success :
                                            diff.StartsWith('-') ? ThemeColors.Danger : color);
        UIFactory.AddCell(row, diff, 55, fontSize, diffColor, HorizontalAlignment.Center);

        return row;
    }

    private void PopulatePlayoffPicture(GameManager gm, Conference[] conferences)
    {
        foreach (var child in _playoffPicture.GetChildren())
            child.QueueFree();

        // Only show playoff picture if seeds have been set or during regular season
        bool hasSeeds = gm.AFCPlayoffSeeds.Count > 0 || gm.NFCPlayoffSeeds.Count > 0;

        foreach (var conf in conferences)
        {
            var header = UIFactory.CreateSubtitle($"{conf} PLAYOFF PICTURE");
            _playoffPicture.AddChild(header);

            if (hasSeeds)
            {
                // Use actual seeds
                var seeds = conf == Conference.AFC ? gm.AFCPlayoffSeeds : gm.NFCPlayoffSeeds;
                for (int i = 0; i < seeds.Count; i++)
                {
                    var seed = seeds[i];
                    var team = gm.GetTeam(seed.TeamId);
                    string marker = seed.IsDivisionWinner ? " (DIV)" : " (WC)";
                    var seedColor = team?.Id == gm.PlayerTeamId ? ThemeColors.AccentText : (Color?)null;
                    var label = UIFactory.CreateLabel(
                        $"  {i + 1}. {team?.Abbreviation ?? "?"} ({team?.CurrentRecord.Wins}-{team?.CurrentRecord.Losses}){marker}",
                        ThemeFonts.Body, seedColor);
                    _playoffPicture.AddChild(label);
                }
            }
            else
            {
                // Project playoff standings from current records
                var confTeams = gm.Teams.Where(t => t.Conference == conf).ToList();
                var projected = ProjectPlayoffSeeds(confTeams);
                for (int i = 0; i < projected.Count && i < 7; i++)
                {
                    var (team, isDivWinner) = projected[i];
                    string marker = isDivWinner ? " (DIV)" : " (WC)";
                    var projColor = team.Id == gm.PlayerTeamId ? ThemeColors.AccentText : (Color?)null;
                    var label = UIFactory.CreateLabel(
                        $"  {i + 1}. {team.Abbreviation} ({team.CurrentRecord.Wins}-{team.CurrentRecord.Losses}){marker}",
                        ThemeFonts.Body, projColor);
                    _playoffPicture.AddChild(label);
                }
            }

            var spacer = new Control { CustomMinimumSize = new Vector2(0, 10) };
            _playoffPicture.AddChild(spacer);
        }
    }

    private List<(Team Team, bool IsDivWinner)> ProjectPlayoffSeeds(List<Team> confTeams)
    {
        var result = new List<(Team, bool)>();

        // Division winners (best record per division)
        var divWinners = new List<Team>();
        foreach (Division div in Enum.GetValues<Division>())
        {
            var winner = confTeams
                .Where(t => t.Division == div)
                .OrderByDescending(t => GetWinPct(t))
                .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
                .FirstOrDefault();
            if (winner != null)
                divWinners.Add(winner);
        }

        // Sort division winners by record
        divWinners = divWinners
            .OrderByDescending(t => GetWinPct(t))
            .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
            .ToList();

        foreach (var dw in divWinners)
            result.Add((dw, true));

        // Wild cards: best remaining teams
        var divWinnerIds = new HashSet<string>(divWinners.Select(t => t.Id));
        var wildcards = confTeams
            .Where(t => !divWinnerIds.Contains(t.Id))
            .OrderByDescending(t => GetWinPct(t))
            .ThenByDescending(t => t.CurrentRecord.PointsFor - t.CurrentRecord.PointsAgainst)
            .Take(3)
            .ToList();

        foreach (var wc in wildcards)
            result.Add((wc, false));

        return result;
    }

    private static float GetWinPct(Team team)
    {
        int totalGames = team.CurrentRecord.Wins + team.CurrentRecord.Losses + team.CurrentRecord.Ties;
        if (totalGames == 0) return 0f;
        return (team.CurrentRecord.Wins + team.CurrentRecord.Ties * 0.5f) / totalGames;
    }


    // --- Navigation ---

    private void OnAFCPressed()
    {
        _mode = ViewMode.AFC;
        Refresh();
    }

    private void OnNFCPressed()
    {
        _mode = ViewMode.NFC;
        Refresh();
    }

    private void OnBothPressed()
    {
        _mode = ViewMode.Both;
        Refresh();
    }

    // --- Signal Handlers ---

    private void OnWeekAdvanced(int year, int week) => Refresh();
    private void OnGameCompleted(string gameId) => Refresh();
}
