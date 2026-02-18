using System.Text.Json;
using Godot;
using GMSimulator.Core;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class NewGameSetup : Control
{
    private VBoxContainer _teamContainer = null!;
    private LineEdit _seedInput = null!;
    private Button _startButton = null!;

    private List<TeamEntry> _teams = new();
    private string _selectedTeamId = "";
    private Button? _selectedButton;

    private static readonly Color ConfHeaderColor = new(0.85f, 0.75f, 0.35f);
    private static readonly Color DivHeaderColor = new(0.6f, 0.65f, 0.7f);
    private static readonly Color TeamButtonNormal = new(0.12f, 0.14f, 0.18f);
    private static readonly Color TeamButtonHover = new(0.18f, 0.22f, 0.28f);
    private static readonly Color TeamButtonSelected = new(0.15f, 0.35f, 0.55f);

    private record TeamEntry(string Id, string City, string Name, string Conference, string Division, string PrimaryColor);

    private static readonly string[] DivisionOrder = { "East", "North", "South", "West" };

    public override void _Ready()
    {
        _teamContainer = GetNode<VBoxContainer>("MarginContainer/VBox/TeamListScroll/TeamContainer");
        _seedInput = GetNode<LineEdit>("MarginContainer/VBox/BottomBar/SeedInput");
        _startButton = GetNode<Button>("MarginContainer/VBox/BottomBar/StartButton");
        _startButton.Disabled = true;

        LoadTeamList();
        BuildTeamGrid();
    }

    private void LoadTeamList()
    {
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        string teamsJson = System.IO.File.ReadAllText(System.IO.Path.Combine(dataPath, "teams.json"));
        var doc = JsonDocument.Parse(teamsJson);
        var root = doc.RootElement;
        var teamsArray = root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("teams");

        _teams.Clear();

        foreach (var td in teamsArray.EnumerateArray())
        {
            var entry = new TeamEntry(
                td.GetProperty("id").GetString() ?? "",
                td.GetProperty("city").GetString() ?? "",
                td.GetProperty("name").GetString() ?? "",
                td.GetProperty("conference").GetString() ?? "",
                td.GetProperty("division").GetString() ?? "",
                td.GetProperty("primaryColor").GetString() ?? "#333333"
            );
            _teams.Add(entry);
        }
    }

    private void BuildTeamGrid()
    {
        // Group teams: Conference -> Division -> List<TeamEntry>
        var grouped = new Dictionary<string, Dictionary<string, List<TeamEntry>>>();
        foreach (var team in _teams)
        {
            if (!grouped.ContainsKey(team.Conference))
                grouped[team.Conference] = new Dictionary<string, List<TeamEntry>>();
            if (!grouped[team.Conference].ContainsKey(team.Division))
                grouped[team.Conference][team.Division] = new List<TeamEntry>();
            grouped[team.Conference][team.Division].Add(team);
        }

        // Sort teams within each division alphabetically by city
        foreach (var conf in grouped.Values)
            foreach (var divTeams in conf.Values)
                divTeams.Sort((a, b) => string.Compare(a.City, b.City, System.StringComparison.Ordinal));

        // Build UI: two conferences side by side
        var columns = new HBoxContainer();
        columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddThemeConstantOverride("separation", 30);
        _teamContainer.AddChild(columns);

        foreach (var confName in new[] { "AFC", "NFC" })
        {
            if (!grouped.ContainsKey(confName)) continue;

            var confColumn = new VBoxContainer();
            confColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            confColumn.AddThemeConstantOverride("separation", 12);
            columns.AddChild(confColumn);

            // Conference header
            var confLabel = new Label();
            confLabel.Text = confName;
            confLabel.HorizontalAlignment = HorizontalAlignment.Center;
            confLabel.AddThemeFontSizeOverride("font_size", 28);
            confLabel.Modulate = ConfHeaderColor;
            confColumn.AddChild(confLabel);

            var confSep = new HSeparator();
            confColumn.AddChild(confSep);

            foreach (var divName in DivisionOrder)
            {
                if (!grouped[confName].ContainsKey(divName)) continue;

                // Division header
                var divLabel = new Label();
                divLabel.Text = $"{confName} {divName}";
                divLabel.HorizontalAlignment = HorizontalAlignment.Left;
                divLabel.AddThemeFontSizeOverride("font_size", 18);
                divLabel.Modulate = DivHeaderColor;
                confColumn.AddChild(divLabel);

                // Team buttons in a grid (2 columns per division)
                var grid = new GridContainer();
                grid.Columns = 2;
                grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                grid.AddThemeConstantOverride("h_separation", 8);
                grid.AddThemeConstantOverride("v_separation", 6);
                confColumn.AddChild(grid);

                foreach (var team in grouped[confName][divName])
                {
                    var btn = new Button();
                    btn.Text = $"{team.City} {team.Name}";
                    btn.CustomMinimumSize = new Vector2(220, 38);
                    btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                    // Style the button
                    var normalStyle = new StyleBoxFlat();
                    normalStyle.BgColor = TeamButtonNormal;
                    normalStyle.SetCornerRadiusAll(4);
                    normalStyle.SetContentMarginAll(8);

                    // Add a left color accent from team primary color
                    var teamColor = Color.FromHtml(team.PrimaryColor);
                    normalStyle.BorderWidthLeft = 4;
                    normalStyle.BorderColor = teamColor;

                    var hoverStyle = new StyleBoxFlat();
                    hoverStyle.BgColor = TeamButtonHover;
                    hoverStyle.SetCornerRadiusAll(4);
                    hoverStyle.SetContentMarginAll(8);
                    hoverStyle.BorderWidthLeft = 4;
                    hoverStyle.BorderColor = teamColor;

                    btn.AddThemeStyleboxOverride("normal", normalStyle);
                    btn.AddThemeStyleboxOverride("hover", hoverStyle);

                    string teamId = team.Id;
                    btn.Pressed += () => OnTeamButtonPressed(teamId, btn);

                    grid.AddChild(btn);
                }

                // Small spacer after each division
                var spacer = new Control();
                spacer.CustomMinimumSize = new Vector2(0, 4);
                confColumn.AddChild(spacer);
            }
        }
    }

    private void OnTeamButtonPressed(string teamId, Button btn)
    {
        // Deselect previous
        if (_selectedButton != null)
        {
            var prevNormal = _selectedButton.GetThemeStylebox("normal") as StyleBoxFlat;
            if (prevNormal != null)
                prevNormal.BgColor = TeamButtonNormal;
        }

        // Select new
        _selectedTeamId = teamId;
        _selectedButton = btn;
        _startButton.Disabled = false;

        var normalStyle = btn.GetThemeStylebox("normal") as StyleBoxFlat;
        if (normalStyle != null)
            normalStyle.BgColor = TeamButtonSelected;
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
    }

    private void OnStartPressed()
    {
        if (string.IsNullOrEmpty(_selectedTeamId)) return;

        int seed;
        if (string.IsNullOrWhiteSpace(_seedInput.Text))
        {
            seed = new Random().Next();
        }
        else if (!int.TryParse(_seedInput.Text, out seed))
        {
            seed = _seedInput.Text.GetHashCode();
        }

        GD.Print($"Starting new game: Team={_selectedTeamId}, Seed={seed}");
        GameManager.Instance.StartNewGame(_selectedTeamId, seed);
        GetTree().ChangeSceneToFile("res://Scenes/Main/GameShell.tscn");
    }
}
