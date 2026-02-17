using System.Text.Json;
using Godot;
using GMSimulator.Models;
using FileAccess = Godot.FileAccess;

namespace GMSimulator.Core;

public static class SettingsManager
{
    private const string SettingsPath = "user://settings.json";

    public static GameSettings Current { get; private set; } = new();

    public static void Load()
    {
        if (!FileAccess.FileExists(SettingsPath))
            return;

        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        string json = file.GetAsText();
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var settings = JsonSerializer.Deserialize<GameSettings>(json);
            if (settings != null)
                Current = settings;
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"Failed to load settings: {ex.Message}");
        }
    }

    public static void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(Current, options);

        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"Failed to save settings: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(json);
    }
}
