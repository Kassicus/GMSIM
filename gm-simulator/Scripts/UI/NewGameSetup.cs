using System.Text.Json;
using Godot;
using GMSimulator.Core;

namespace GMSimulator.UI;

public partial class NewGameSetup : Control
{
    private ItemList _teamList = null!;
    private LineEdit _seedInput = null!;
    private Button _startButton = null!;

    private List<TeamEntry> _teams = new();
    private int _selectedIndex = -1;

    private record TeamEntry(string Id, string City, string Name, string Conference, string Division);

    public override void _Ready()
    {
        _teamList = GetNode<ItemList>("MarginContainer/VBox/TeamListScroll/TeamList");
        _seedInput = GetNode<LineEdit>("MarginContainer/VBox/BottomBar/SeedInput");
        _startButton = GetNode<Button>("MarginContainer/VBox/BottomBar/StartButton");
        _startButton.Disabled = true;

        LoadTeamList();
    }

    private void LoadTeamList()
    {
        string dataPath = ProjectSettings.GlobalizePath("res://Resources/Data");
        string teamsJson = System.IO.File.ReadAllText(System.IO.Path.Combine(dataPath, "teams.json"));
        var doc = JsonDocument.Parse(teamsJson);
        var root = doc.RootElement;
        var teamsArray = root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("teams");

        _teamList.Clear();
        _teams.Clear();

        foreach (var td in teamsArray.EnumerateArray())
        {
            var entry = new TeamEntry(
                td.GetProperty("id").GetString() ?? "",
                td.GetProperty("city").GetString() ?? "",
                td.GetProperty("name").GetString() ?? "",
                td.GetProperty("conference").GetString() ?? "",
                td.GetProperty("division").GetString() ?? ""
            );
            _teams.Add(entry);

            string display = $"{entry.City} {entry.Name} ({entry.Conference} {entry.Division})";
            _teamList.AddItem(display);
        }
    }

    private void OnTeamSelected(int index)
    {
        _selectedIndex = index;
        _startButton.Disabled = false;
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
    }

    private void OnStartPressed()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _teams.Count) return;

        string teamId = _teams[_selectedIndex].Id;

        int seed;
        if (string.IsNullOrWhiteSpace(_seedInput.Text))
        {
            seed = new Random().Next();
        }
        else if (!int.TryParse(_seedInput.Text, out seed))
        {
            seed = _seedInput.Text.GetHashCode();
        }

        GD.Print($"Starting new game: Team={teamId}, Seed={seed}");
        GameManager.Instance.StartNewGame(teamId, seed);
        GetTree().ChangeSceneToFile("res://Scenes/Main/GameShell.tscn");
    }
}
