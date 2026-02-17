namespace GMSimulator.Models;

public class GameSettings
{
    // Simulation
    public int SimSpeedMs { get; set; } = 500;
    public bool AutoAdvanceWeek { get; set; }

    // Auto-Save
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalWeeks { get; set; } = 4;

    // Notifications
    public bool ShowInjuryNotifications { get; set; } = true;
    public bool ShowPhaseNotifications { get; set; } = true;
    public bool ShowAwardNotifications { get; set; } = true;
    public float NotificationDuration { get; set; } = 4.0f;

    // Display
    public bool ConfirmCutPlayers { get; set; } = true;
}
