using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class PostGameReport : Window
{
    private Label _awayTeamLabel = null!;
    private Label _awayScoreLabel = null!;
    private Label _homeTeamLabel = null!;
    private Label _homeScoreLabel = null!;
    private GridContainer _quarterScores = null!;
    private VBoxContainer _potgSection = null!;
    private VBoxContainer _teamStatsSection = null!;
    private VBoxContainer _keyPlaysSection = null!;
    private VBoxContainer _passingTab = null!;
    private VBoxContainer _rushingTab = null!;
    private VBoxContainer _receivingTab = null!;
    private VBoxContainer _defenseTab = null!;

    private GameResult? _result;

    public override void _Ready()
    {
        _awayTeamLabel = GetNode<Label>("MarginContainer/VBox/ScoreHeader/AwayTeamLabel");
        _awayScoreLabel = GetNode<Label>("MarginContainer/VBox/ScoreHeader/AwayScoreLabel");
        _homeTeamLabel = GetNode<Label>("MarginContainer/VBox/ScoreHeader/HomeTeamLabel");
        _homeScoreLabel = GetNode<Label>("MarginContainer/VBox/ScoreHeader/HomeScoreLabel");
        _quarterScores = GetNode<GridContainer>("MarginContainer/VBox/ContentScroll/ContentVBox/QuarterScores");
        _potgSection = GetNode<VBoxContainer>("MarginContainer/VBox/ContentScroll/ContentVBox/POTGSection");
        _teamStatsSection = GetNode<VBoxContainer>("MarginContainer/VBox/ContentScroll/ContentVBox/TeamStatsSection");
        _keyPlaysSection = GetNode<VBoxContainer>("MarginContainer/VBox/ContentScroll/ContentVBox/KeyPlaysSection");

        var tabContainer = GetNode<TabContainer>("MarginContainer/VBox/ContentScroll/ContentVBox/TabContainer");
        _passingTab = tabContainer.GetNode<VBoxContainer>("Passing");
        _rushingTab = tabContainer.GetNode<VBoxContainer>("Rushing");
        _receivingTab = tabContainer.GetNode<VBoxContainer>("Receiving");
        _defenseTab = tabContainer.GetNode<VBoxContainer>("Defense");

        if (_result != null)
            PopulateReport();
    }

    public void Initialize(GameResult result)
    {
        _result = result;
    }

    private void PopulateReport()
    {
        var gm = GameManager.Instance;
        if (gm == null || _result == null) return;

        var game = gm.CurrentSeason.Games.FirstOrDefault(g => g.Id == _result.GameId);
        if (game == null) return;

        var awayTeam = gm.GetTeam(game.AwayTeamId);
        var homeTeam = gm.GetTeam(game.HomeTeamId);

        // Score header
        _awayTeamLabel.Text = awayTeam?.Abbreviation ?? "AWAY";
        _awayScoreLabel.Text = _result.AwayScore.ToString();
        _homeTeamLabel.Text = homeTeam?.Abbreviation ?? "HOME";
        _homeScoreLabel.Text = _result.HomeScore.ToString();

        // Highlight winner
        if (_result.AwayScore > _result.HomeScore)
            _awayScoreLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
        else if (_result.HomeScore > _result.AwayScore)
            _homeScoreLabel.AddThemeColorOverride("font_color", ThemeColors.Success);

        PopulateQuarterScores(awayTeam, homeTeam);
        PopulatePlayerOfTheGame(gm);
        PopulateTeamStats(awayTeam, homeTeam);
        PopulateKeyPlays();
        PopulatePassingStats(gm, game);
        PopulateRushingStats(gm, game);
        PopulateReceivingStats(gm, game);
        PopulateDefenseStats(gm, game);
    }

    private void PopulateQuarterScores(Team? awayTeam, Team? homeTeam)
    {
        if (_result == null) return;

        // Header row: blank, Q1, Q2, Q3, Q4, Total
        AddGridLabel(_quarterScores, "Team", 14, true);
        AddGridLabel(_quarterScores, "Q1", 14, true);
        AddGridLabel(_quarterScores, "Q2", 14, true);
        AddGridLabel(_quarterScores, "Q3", 14, true);
        AddGridLabel(_quarterScores, "Q4", 14, true);
        AddGridLabel(_quarterScores, "T", 14, true);

        // Away row
        AddGridLabel(_quarterScores, awayTeam?.Abbreviation ?? "AWAY", 14);
        for (int i = 0; i < 4; i++)
            AddGridLabel(_quarterScores, _result.AwayQuarterScores[i].ToString(), 14);
        AddGridLabel(_quarterScores, _result.AwayScore.ToString(), 16, true);

        // Home row
        AddGridLabel(_quarterScores, homeTeam?.Abbreviation ?? "HOME", 14);
        for (int i = 0; i < 4; i++)
            AddGridLabel(_quarterScores, _result.HomeQuarterScores[i].ToString(), 14);
        AddGridLabel(_quarterScores, _result.HomeScore.ToString(), 16, true);
    }

    private void PopulatePlayerOfTheGame(GameManager gm)
    {
        if (_result == null) return;

        var potgHeader = new Label { Text = "PLAYER OF THE GAME" };
        potgHeader.AddThemeFontSizeOverride("font_size", ThemeFonts.Subtitle);
        _potgSection.AddChild(potgHeader);

        if (_result.PlayerOfTheGameId != null)
        {
            var player = gm.GetPlayer(_result.PlayerOfTheGameId);
            string name = player != null ? $"{player.FirstName} {player.LastName} ({player.Position})" : "Unknown";
            string line = _result.PlayerOfTheGameLine ?? "";

            var potgLabel = new Label { Text = $"{name} — {line}" };
            potgLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _potgSection.AddChild(potgLabel);
        }
    }

    private void PopulateTeamStats(Team? awayTeam, Team? homeTeam)
    {
        if (_result == null) return;

        var header = new Label { Text = "TEAM STATS" };
        header.AddThemeFontSizeOverride("font_size", ThemeFonts.Subtitle);
        _teamStatsSection.AddChild(header);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 4);

        var away = _result.AwayTeamStats;
        var home = _result.HomeTeamStats;

        AddStatRow(grid, awayTeam?.Abbreviation ?? "AWAY", "Stat", homeTeam?.Abbreviation ?? "HOME", true);
        AddStatRow(grid, away.TotalYards.ToString(), "Total Yards", home.TotalYards.ToString());
        AddStatRow(grid, away.PassingYards.ToString(), "Passing Yards", home.PassingYards.ToString());
        AddStatRow(grid, away.RushingYards.ToString(), "Rushing Yards", home.RushingYards.ToString());
        AddStatRow(grid, away.Turnovers.ToString(), "Turnovers", home.Turnovers.ToString());
        AddStatRow(grid, away.FirstDowns.ToString(), "First Downs", home.FirstDowns.ToString());
        AddStatRow(grid, $"{away.ThirdDownConversions}/{away.ThirdDownAttempts}", "3rd Down", $"{home.ThirdDownConversions}/{home.ThirdDownAttempts}");
        AddStatRow(grid, $"{away.Penalties} ({away.PenaltyYards} yds)", "Penalties", $"{home.Penalties} ({home.PenaltyYards} yds)");
        AddStatRow(grid, FormatTOP(away.TimeOfPossessionSeconds), "Time of Possession", FormatTOP(home.TimeOfPossessionSeconds));
        AddStatRow(grid, $"{away.Sacks} ({away.SackYards} yds)", "Sacks", $"{home.Sacks} ({home.SackYards} yds)");

        _teamStatsSection.AddChild(grid);
    }

    private void PopulateKeyPlays()
    {
        if (_result == null || _result.KeyPlays.Count == 0) return;

        var header = new Label { Text = "KEY PLAYS" };
        header.AddThemeFontSizeOverride("font_size", ThemeFonts.Subtitle);
        _keyPlaysSection.AddChild(header);

        foreach (var play in _result.KeyPlays.Take(8))
        {
            var playLabel = new Label { Text = $"  {play}" };
            playLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            playLabel.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);
            _keyPlaysSection.AddChild(playLabel);
        }
    }

    private void PopulatePassingStats(GameManager gm, Game game)
    {
        AddPlayerStatHeader(_passingTab, "Player", "C/ATT", "YDS", "TD", "INT", "SCK");

        var passers = GetPlayerStatsForGame(gm, game)
            .Where(kv => kv.Value.Attempts > 0)
            .OrderByDescending(kv => kv.Value.PassingYards);

        foreach (var (playerId, stats) in passers)
        {
            var player = gm.GetPlayer(playerId);
            if (player == null) continue;
            string teamAbbr = GetTeamAbbr(gm, player);
            AddPlayerStatRow(_passingTab,
                $"{teamAbbr} {player.LastName}",
                $"{stats.Completions}/{stats.Attempts}",
                stats.PassingYards.ToString(),
                stats.PassingTDs.ToString(),
                stats.Interceptions.ToString(),
                stats.Sacked.ToString());
        }
    }

    private void PopulateRushingStats(GameManager gm, Game game)
    {
        AddPlayerStatHeader(_rushingTab, "Player", "ATT", "YDS", "TD", "FUM", "");

        var rushers = GetPlayerStatsForGame(gm, game)
            .Where(kv => kv.Value.RushAttempts > 0)
            .OrderByDescending(kv => kv.Value.RushingYards);

        foreach (var (playerId, stats) in rushers)
        {
            var player = gm.GetPlayer(playerId);
            if (player == null) continue;
            string teamAbbr = GetTeamAbbr(gm, player);
            AddPlayerStatRow(_rushingTab,
                $"{teamAbbr} {player.LastName}",
                stats.RushAttempts.ToString(),
                stats.RushingYards.ToString(),
                stats.RushingTDs.ToString(),
                stats.FumblesLost.ToString(),
                "");
        }
    }

    private void PopulateReceivingStats(GameManager gm, Game game)
    {
        AddPlayerStatHeader(_receivingTab, "Player", "REC", "TGT", "YDS", "TD", "");

        var receivers = GetPlayerStatsForGame(gm, game)
            .Where(kv => kv.Value.Receptions > 0)
            .OrderByDescending(kv => kv.Value.ReceivingYards);

        foreach (var (playerId, stats) in receivers)
        {
            var player = gm.GetPlayer(playerId);
            if (player == null) continue;
            string teamAbbr = GetTeamAbbr(gm, player);
            AddPlayerStatRow(_receivingTab,
                $"{teamAbbr} {player.LastName}",
                stats.Receptions.ToString(),
                stats.Targets.ToString(),
                stats.ReceivingYards.ToString(),
                stats.ReceivingTDs.ToString(),
                "");
        }
    }

    private void PopulateDefenseStats(GameManager gm, Game game)
    {
        AddPlayerStatHeader(_defenseTab, "Player", "TKL", "SACK", "TFL", "INT", "PD");

        var defenders = GetPlayerStatsForGame(gm, game)
            .Where(kv => kv.Value.TotalTackles > 0 || kv.Value.Sacks > 0 || kv.Value.InterceptionsDef > 0)
            .OrderByDescending(kv => kv.Value.TotalTackles + kv.Value.Sacks * 3);

        foreach (var (playerId, stats) in defenders)
        {
            var player = gm.GetPlayer(playerId);
            if (player == null) continue;
            string teamAbbr = GetTeamAbbr(gm, player);
            AddPlayerStatRow(_defenseTab,
                $"{teamAbbr} {player.LastName}",
                stats.TotalTackles.ToString(),
                stats.Sacks.ToString("0.#"),
                stats.TacklesForLoss.ToString(),
                stats.InterceptionsDef.ToString(),
                stats.PassesDefended.ToString());
        }
    }

    private string GetTeamAbbr(GameManager gm, Player player) =>
        gm.GetTeam(player.TeamId ?? "")?.Abbreviation ?? "";

    private IEnumerable<KeyValuePair<string, PlayerGameStats>> GetPlayerStatsForGame(GameManager gm, Game game)
    {
        if (_result == null) return Enumerable.Empty<KeyValuePair<string, PlayerGameStats>>();

        return _result.PlayerStats.Where(kv =>
        {
            var p = gm.GetPlayer(kv.Key);
            return p != null && (p.TeamId == game.HomeTeamId || p.TeamId == game.AwayTeamId);
        });
    }

    // --- UI Helpers ---

    private void AddGridLabel(GridContainer grid, string text, int fontSize, bool bold = false)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(60, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        grid.AddChild(label);
    }

    private void AddStatRow(GridContainer grid, string awayVal, string statName, string homeVal, bool isHeader = false)
    {
        int fontSize = isHeader ? ThemeFonts.BodyLarge : ThemeFonts.Body;

        var awayLabel = new Label
        {
            Text = awayVal,
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(120, 0)
        };
        awayLabel.AddThemeFontSizeOverride("font_size", fontSize);

        var nameLabel = new Label
        {
            Text = statName,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(160, 0)
        };
        nameLabel.AddThemeFontSizeOverride("font_size", fontSize);
        nameLabel.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);

        var homeLabel = new Label
        {
            Text = homeVal,
            HorizontalAlignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(120, 0)
        };
        homeLabel.AddThemeFontSizeOverride("font_size", fontSize);

        grid.AddChild(awayLabel);
        grid.AddChild(nameLabel);
        grid.AddChild(homeLabel);
    }

    private void AddPlayerStatHeader(VBoxContainer tab, string name, string c1, string c2, string c3, string c4, string c5)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        AddStatCell(hbox, name, 150, HorizontalAlignment.Left, true);
        AddStatCell(hbox, c1, 70, HorizontalAlignment.Center, true);
        AddStatCell(hbox, c2, 60, HorizontalAlignment.Center, true);
        AddStatCell(hbox, c3, 50, HorizontalAlignment.Center, true);
        AddStatCell(hbox, c4, 50, HorizontalAlignment.Center, true);
        if (!string.IsNullOrEmpty(c5))
            AddStatCell(hbox, c5, 50, HorizontalAlignment.Center, true);

        tab.AddChild(hbox);

        var sep = new HSeparator();
        tab.AddChild(sep);
    }

    private void AddPlayerStatRow(VBoxContainer tab, string name, string v1, string v2, string v3, string v4, string v5)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        AddStatCell(hbox, name, 150, HorizontalAlignment.Left);
        AddStatCell(hbox, v1, 70, HorizontalAlignment.Center);
        AddStatCell(hbox, v2, 60, HorizontalAlignment.Center);
        AddStatCell(hbox, v3, 50, HorizontalAlignment.Center);
        AddStatCell(hbox, v4, 50, HorizontalAlignment.Center);
        if (!string.IsNullOrEmpty(v5))
            AddStatCell(hbox, v5, 50, HorizontalAlignment.Center);

        tab.AddChild(hbox);
    }

    private void AddStatCell(HBoxContainer hbox, string text, int minWidth, HorizontalAlignment align, bool isHeader = false)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, 0),
            HorizontalAlignment = align
        };
        label.AddThemeFontSizeOverride("font_size", isHeader ? ThemeFonts.Small : ThemeFonts.Body);
        if (isHeader)
            label.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);
        hbox.AddChild(label);
    }

    private static string FormatTOP(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    private void OnClosePressed()
    {
        QueueFree();
    }
}
