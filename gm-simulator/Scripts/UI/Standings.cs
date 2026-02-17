using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

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
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 2);

        // Division header
        var header = new Label { Text = divName };
        header.AddThemeFontSizeOverride("font_size", 18);
        header.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.4f));
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
                var style = new StyleBoxFlat();
                style.BgColor = new Color(0.15f, 0.25f, 0.4f, 0.5f);
                style.SetCornerRadiusAll(3);
                panel.AddThemeStyleboxOverride("panel", style);
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
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        int fontSize = isHeader ? 12 : 14;
        var color = isHeader ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 1f, 1f);

        AddCell(row, team, 70, HorizontalAlignment.Left, fontSize, color);
        AddCell(row, w, 35, HorizontalAlignment.Center, fontSize, color);
        AddCell(row, l, 35, HorizontalAlignment.Center, fontSize, color);
        AddCell(row, t, 25, HorizontalAlignment.Center, fontSize, color);
        AddCell(row, pct, 60, HorizontalAlignment.Center, fontSize, color);
        AddCell(row, pf, 50, HorizontalAlignment.Center, fontSize, color);
        AddCell(row, pa, 50, HorizontalAlignment.Center, fontSize, color);
        AddCell(row, diff, 55, HorizontalAlignment.Center, fontSize,
            isHeader ? color : (diff.StartsWith('+') ? new Color(0.4f, 1f, 0.4f) :
                               diff.StartsWith('-') ? new Color(1f, 0.4f, 0.4f) : color));

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
            var header = new Label { Text = $"{conf} PLAYOFF PICTURE" };
            header.AddThemeFontSizeOverride("font_size", 16);
            header.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.4f));
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
                    var label = new Label
                    {
                        Text = $"  {i + 1}. {team?.Abbreviation ?? "?"} ({team?.CurrentRecord.Wins}-{team?.CurrentRecord.Losses}){marker}"
                    };
                    label.AddThemeFontSizeOverride("font_size", 13);
                    if (team?.Id == gm.PlayerTeamId)
                        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
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
                    var label = new Label
                    {
                        Text = $"  {i + 1}. {team.Abbreviation} ({team.CurrentRecord.Wins}-{team.CurrentRecord.Losses}){marker}"
                    };
                    label.AddThemeFontSizeOverride("font_size", 13);
                    if (team.Id == gm.PlayerTeamId)
                        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
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

    private void AddCell(HBoxContainer row, string text, int minWidth, HorizontalAlignment align,
        int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, 0),
            HorizontalAlignment = align
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        row.AddChild(label);
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
