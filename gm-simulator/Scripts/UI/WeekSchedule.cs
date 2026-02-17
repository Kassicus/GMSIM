using Godot;
using GMSimulator.Core;
using GMSimulator.Models;

namespace GMSimulator.UI;

public partial class WeekSchedule : Control
{
    private Label _headerLabel = null!;
    private Label _weekLabel = null!;
    private Button _prevBtn = null!;
    private Button _nextBtn = null!;
    private VBoxContainer _matchupList = null!;

    private int _displayWeek = 1;
    private int _maxWeek = 18;

    public override void _Ready()
    {
        _headerLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/HeaderLabel");
        _weekLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/WeekNav/WeekLabel");
        _prevBtn = GetNode<Button>("ScrollContainer/MarginContainer/VBox/WeekNav/PrevBtn");
        _nextBtn = GetNode<Button>("ScrollContainer/MarginContainer/VBox/WeekNav/NextBtn");
        _matchupList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/MatchupList");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
            EventBus.Instance.GameCompleted += OnGameCompleted;
        }

        // Start on current week
        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameActive)
        {
            _displayWeek = gm.Calendar.CurrentWeek;
            UpdateMaxWeek();
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

    private void UpdateMaxWeek()
    {
        var gm = GameManager.Instance;
        if (gm?.CurrentSeason == null) return;

        _maxWeek = gm.CurrentSeason.Games.Count > 0
            ? gm.CurrentSeason.Games.Max(g => g.Week)
            : 18;
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;

        UpdateMaxWeek();
        _displayWeek = Math.Clamp(_displayWeek, 1, Math.Max(1, _maxWeek));

        // Update header for playoffs
        bool isPlayoffWeek = gm.CurrentSeason.Games
            .Any(g => g.Week == _displayWeek && g.IsPlayoff);
        _headerLabel.Text = isPlayoffWeek ? "PLAYOFF SCHEDULE" : "WEEK SCHEDULE";

        // Update week label
        if (isPlayoffWeek)
        {
            string roundName = GetPlayoffRoundName(gm, _displayWeek);
            _weekLabel.Text = roundName;
        }
        else
        {
            _weekLabel.Text = $"Week {_displayWeek}";
        }

        _prevBtn.Disabled = _displayWeek <= 1;
        _nextBtn.Disabled = _displayWeek >= _maxWeek;

        // Clear matchup list
        foreach (var child in _matchupList.GetChildren())
            child.QueueFree();

        // Get games for this week
        var weekGames = gm.CurrentSeason.Games
            .Where(g => g.Week == _displayWeek)
            .OrderBy(g => g.IsCompleted ? 0 : 1) // completed first
            .ToList();

        if (weekGames.Count == 0)
        {
            var noGames = new Label
            {
                Text = "No games scheduled this week",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            noGames.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _matchupList.AddChild(noGames);
            return;
        }

        // Find teams with a bye this week (regular season only)
        if (!isPlayoffWeek)
        {
            var teamsPlaying = new HashSet<string>();
            foreach (var g in weekGames)
            {
                teamsPlaying.Add(g.HomeTeamId);
                teamsPlaying.Add(g.AwayTeamId);
            }

            var byeTeams = gm.Teams
                .Where(t => !teamsPlaying.Contains(t.Id))
                .Select(t => t.Abbreviation)
                .OrderBy(a => a)
                .ToList();

            if (byeTeams.Count > 0)
            {
                var byeLabel = new Label
                {
                    Text = $"BYE: {string.Join(", ", byeTeams)}",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                byeLabel.AddThemeFontSizeOverride("font_size", 14);
                byeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.5f));
                _matchupList.AddChild(byeLabel);

                var sep = new HSeparator();
                _matchupList.AddChild(sep);
            }
        }

        // Add matchup rows
        foreach (var game in weekGames)
        {
            var row = CreateMatchupRow(game, gm);
            _matchupList.AddChild(row);
        }
    }

    private HBoxContainer CreateMatchupRow(Game game, GameManager gm)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var awayTeam = gm.GetTeam(game.AwayTeamId);
        var homeTeam = gm.GetTeam(game.HomeTeamId);
        bool isPlayerGame = game.HomeTeamId == gm.PlayerTeamId || game.AwayTeamId == gm.PlayerTeamId;

        // Background panel for highlighting player's team
        var panel = new PanelContainer();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (isPlayerGame)
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.15f, 0.25f, 0.4f, 0.5f);
            style.SetCornerRadiusAll(4);
            panel.AddThemeStyleboxOverride("panel", style);
        }

        var innerHBox = new HBoxContainer();
        innerHBox.AddThemeConstantOverride("separation", 8);
        innerHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Away team
        var awayLabel = new Label
        {
            Text = awayTeam?.Abbreviation ?? "???",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        awayLabel.AddThemeFontSizeOverride("font_size", 16);
        if (game.IsCompleted && game.AwayScore > game.HomeScore)
            awayLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));

        var awayRecord = new Label
        {
            Text = awayTeam != null ? $"({awayTeam.CurrentRecord.Wins}-{awayTeam.CurrentRecord.Losses})" : "",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        awayRecord.AddThemeFontSizeOverride("font_size", 12);
        awayRecord.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));

        // Score or "vs"
        Label centerLabel;
        if (game.IsCompleted)
        {
            centerLabel = new Label
            {
                Text = $"{game.AwayScore}  -  {game.HomeScore}",
                CustomMinimumSize = new Vector2(100, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            centerLabel.AddThemeFontSizeOverride("font_size", 18);
        }
        else
        {
            centerLabel = new Label
            {
                Text = " @ ",
                CustomMinimumSize = new Vector2(100, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            centerLabel.AddThemeFontSizeOverride("font_size", 16);
            centerLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        }

        // Home team
        var homeLabel = new Label
        {
            Text = homeTeam?.Abbreviation ?? "???",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        homeLabel.AddThemeFontSizeOverride("font_size", 16);
        if (game.IsCompleted && game.HomeScore > game.AwayScore)
            homeLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));

        var homeRecord = new Label
        {
            Text = homeTeam != null ? $"({homeTeam.CurrentRecord.Wins}-{homeTeam.CurrentRecord.Losses})" : "",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        homeRecord.AddThemeFontSizeOverride("font_size", 12);
        homeRecord.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));

        // Spacer
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };

        innerHBox.AddChild(awayLabel);
        innerHBox.AddChild(awayRecord);
        innerHBox.AddChild(centerLabel);
        innerHBox.AddChild(homeLabel);
        innerHBox.AddChild(homeRecord);
        innerHBox.AddChild(spacer);

        // Box Score button for completed games
        if (game.IsCompleted)
        {
            var boxScoreBtn = new Button
            {
                Text = "Box Score",
                CustomMinimumSize = new Vector2(90, 0)
            };
            string gameId = game.Id;
            boxScoreBtn.Pressed += () => OnBoxScorePressed(gameId);
            innerHBox.AddChild(boxScoreBtn);
        }

        panel.AddChild(innerHBox);
        row.AddChild(panel);
        return row;
    }

    private string GetPlayoffRoundName(GameManager gm, int week)
    {
        // Playoff games are added with week 1, 2, 3 within the Playoffs phase
        // and week 1 within SuperBowl phase
        var playoffGames = gm.CurrentSeason.Games
            .Where(g => g.IsPlayoff && g.Week == week)
            .ToList();

        if (playoffGames.Count == 0) return $"Playoff Week {week}";

        // Determine round by number of games
        int gameCount = playoffGames.Count;
        if (gameCount == 1) return "Super Bowl";
        if (gameCount == 2) return "Conference Championships";
        if (gameCount == 4) return "Divisional Round";
        return "Wild Card Round";
    }

    private void OnBoxScorePressed(string gameId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Find the GameResult for this game
        var result = gm.RecentGameResults.FirstOrDefault(r => r.GameId == gameId);
        if (result == null) return;

        // Instantiate PostGameReport
        var scene = GD.Load<PackedScene>("res://Scenes/GameDay/PostGameReport.tscn");
        var report = scene.Instantiate<PostGameReport>();
        report.Initialize(result);
        GetTree().Root.AddChild(report);
    }

    // --- Navigation ---

    private void OnPrevWeek()
    {
        if (_displayWeek > 1)
        {
            _displayWeek--;
            Refresh();
        }
    }

    private void OnNextWeek()
    {
        if (_displayWeek < _maxWeek)
        {
            _displayWeek++;
            Refresh();
        }
    }

    // --- Signal Handlers ---

    private void OnWeekAdvanced(int year, int week)
    {
        _displayWeek = week;
        Refresh();
    }

    private void OnGameCompleted(string gameId) => Refresh();
}
