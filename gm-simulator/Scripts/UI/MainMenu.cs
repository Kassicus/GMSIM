using Godot;
using GMSimulator.Core;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class MainMenu : Control
{
    private Button _continueButton = null!;
    private Button _loadGameButton = null!;

    public override void _Ready()
    {
        _continueButton = GetNode<Button>("CenterContainer/VBox/ContinueButton");
        _loadGameButton = GetNode<Button>("CenterContainer/VBox/LoadGameButton");

        bool hasSaves = SaveLoadManager.HasAnySave();
        _continueButton.Disabled = !hasSaves;
        _loadGameButton.Disabled = !hasSaves;
    }

    private void OnNewGamePressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Main/NewGameSetup.tscn");
    }

    private void OnContinuePressed()
    {
        // Load most recent save (slot 0 for now)
        if (SaveLoadManager.LoadGame(0))
        {
            GetTree().ChangeSceneToFile("res://Scenes/Main/GameShell.tscn");
        }
    }

    private void OnLoadGamePressed()
    {
        // For Phase 1, just try slot 0
        if (SaveLoadManager.LoadGame(0))
        {
            GetTree().ChangeSceneToFile("res://Scenes/Main/GameShell.tscn");
        }
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
