using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using GMSimulator.Models;
using FileAccess = Godot.FileAccess;

namespace GMSimulator.Core;

public static class SaveLoadManager
{
    private const string SaveDirectory = "user://saves";
    private const int MaxSlots = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void SaveGame(string saveName, int slotIndex)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameActive)
        {
            GD.PrintErr("Cannot save: no active game.");
            return;
        }

        EnsureSaveDirectory();

        var saveData = GameManager.Instance.CreateSaveData(saveName);
        string json = JsonSerializer.Serialize(saveData, JsonOptions);
        string path = GetSlotPath(slotIndex);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"Failed to open save file: {path} — {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(json);
        GD.Print($"Game saved to slot {slotIndex}: {saveName}");
    }

    public static bool LoadGame(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);

        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"Save file not found: {path}");
            return false;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"Failed to open save file: {path} — {FileAccess.GetOpenError()}");
            return false;
        }

        string json = file.GetAsText();
        var saveData = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

        if (saveData == null)
        {
            GD.PrintErr("Failed to deserialize save data.");
            return false;
        }

        GameManager.Instance.LoadFromSave(saveData);
        GD.Print($"Game loaded from slot {slotIndex}: {saveData.SaveName}");
        return true;
    }

    public static List<SaveSlotInfo> GetSaveSlots()
    {
        var slots = new List<SaveSlotInfo>();
        EnsureSaveDirectory();

        for (int i = 0; i < MaxSlots; i++)
        {
            string path = GetSlotPath(i);
            if (!FileAccess.FileExists(path))
            {
                slots.Add(new SaveSlotInfo(i, null, null, 0, default, false));
                continue;
            }

            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null) continue;

                string json = file.GetAsText();
                // Parse just the metadata without deserializing everything
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? saveName = root.TryGetProperty("SaveName", out var sn) ? sn.GetString() : null;
                DateTime saveDate = root.TryGetProperty("SaveDate", out var sd) ? sd.GetDateTime() : default;
                int year = root.TryGetProperty("CurrentYear", out var cy) ? cy.GetInt32() : 0;
                string? phase = root.TryGetProperty("CurrentPhase", out var cp) ? cp.GetString() : null;

                slots.Add(new SaveSlotInfo(i, saveName, phase, year, saveDate, true));
            }
            catch
            {
                slots.Add(new SaveSlotInfo(i, "Corrupted Save", null, 0, default, true));
            }
        }

        return slots;
    }

    public static void DeleteSave(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            GD.Print($"Deleted save slot {slotIndex}");
        }
    }

    public static bool HasAnySave()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (FileAccess.FileExists(GetSlotPath(i)))
                return true;
        }
        return false;
    }

    private static string GetSlotPath(int slotIndex) => $"{SaveDirectory}/slot_{slotIndex}.json";

    private static void EnsureSaveDirectory()
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(SaveDirectory)))
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectory));
        }
    }
}

public record SaveSlotInfo(
    int SlotIndex,
    string? SaveName,
    string? Phase,
    int Year,
    DateTime SaveDate,
    bool Exists
);
