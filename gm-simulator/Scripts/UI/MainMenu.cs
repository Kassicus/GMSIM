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

        LoadBackgroundImage();
        StyleButtons();
    }

    private void StyleButtons()
    {
        var buttons = new[] { "NewGameButton", "ContinueButton", "LoadGameButton", "QuitButton" };
        foreach (var name in buttons)
        {
            var btn = GetNode<Button>($"CenterContainer/VBox/{name}");

            var normal = new StyleBoxFlat();
            normal.BgColor = new Color(0.12f, 0.15f, 0.22f, 0.92f);
            normal.SetCornerRadiusAll(6);
            normal.SetContentMarginAll(10);

            var hover = new StyleBoxFlat();
            hover.BgColor = new Color(0.18f, 0.25f, 0.38f, 0.95f);
            hover.SetCornerRadiusAll(6);
            hover.SetContentMarginAll(10);

            var pressed = new StyleBoxFlat();
            pressed.BgColor = new Color(0.1f, 0.2f, 0.35f, 1.0f);
            pressed.SetCornerRadiusAll(6);
            pressed.SetContentMarginAll(10);

            var disabled = new StyleBoxFlat();
            disabled.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.7f);
            disabled.SetCornerRadiusAll(6);
            disabled.SetContentMarginAll(10);

            btn.AddThemeStyleboxOverride("normal", normal);
            btn.AddThemeStyleboxOverride("hover", hover);
            btn.AddThemeStyleboxOverride("pressed", pressed);
            btn.AddThemeStyleboxOverride("disabled", disabled);
        }
    }

    private void LoadBackgroundImage()
    {
        var bgRect = GetNode<TextureRect>("Background");
        var image = new Image();
        string path = ProjectSettings.GlobalizePath("res://Assets/Images/menu_background.png");
        var err = image.Load(path);
        if (err == Error.Ok)
        {
            var texture = ImageTexture.CreateFromImage(image);
            bgRect.Texture = texture;
        }
        else
        {
            GD.PrintErr($"Failed to load menu background: {err}");
        }
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
