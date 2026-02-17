using Godot;
using GMSimulator.Core;
using GMSimulator.Models;

namespace GMSimulator.UI;

public partial class SettingsPanel : Window
{
    private HSlider _simSpeedSlider = null!;
    private Label _simSpeedValue = null!;
    private CheckButton _autoSaveEnabled = null!;
    private SpinBox _autoSaveInterval = null!;
    private CheckButton _showInjuryNotifs = null!;
    private CheckButton _showPhaseNotifs = null!;
    private CheckButton _showAwardNotifs = null!;
    private HSlider _durationSlider = null!;
    private Label _durationValue = null!;
    private CheckButton _confirmCuts = null!;

    public override void _Ready()
    {
        var vbox = "MarginContainer/VBox/";

        _simSpeedSlider = GetNode<HSlider>(vbox + "SimSpeedRow/SimSpeedSlider");
        _simSpeedValue = GetNode<Label>(vbox + "SimSpeedRow/SimSpeedValue");
        _autoSaveEnabled = GetNode<CheckButton>(vbox + "AutoSaveEnabled");
        _autoSaveInterval = GetNode<SpinBox>(vbox + "AutoSaveIntervalRow/IntervalSpinBox");
        _showInjuryNotifs = GetNode<CheckButton>(vbox + "ShowInjuryNotifs");
        _showPhaseNotifs = GetNode<CheckButton>(vbox + "ShowPhaseNotifs");
        _showAwardNotifs = GetNode<CheckButton>(vbox + "ShowAwardNotifs");
        _durationSlider = GetNode<HSlider>(vbox + "NotifDurationRow/DurationSlider");
        _durationValue = GetNode<Label>(vbox + "NotifDurationRow/DurationValue");
        _confirmCuts = GetNode<CheckButton>(vbox + "ConfirmCuts");

        var saveBtn = GetNode<Button>(vbox + "ButtonBar/SaveButton");
        var resetBtn = GetNode<Button>(vbox + "ButtonBar/ResetButton");

        saveBtn.Pressed += OnSavePressed;
        resetBtn.Pressed += OnResetPressed;
        CloseRequested += QueueFree;

        _simSpeedSlider.ValueChanged += v => _simSpeedValue.Text = ((int)v).ToString();
        _durationSlider.ValueChanged += v => _durationValue.Text = v.ToString("F1");

        PopulateFromSettings();
    }

    private void PopulateFromSettings()
    {
        var s = SettingsManager.Current;

        _simSpeedSlider.Value = s.SimSpeedMs;
        _simSpeedValue.Text = s.SimSpeedMs.ToString();
        _autoSaveEnabled.ButtonPressed = s.AutoSaveEnabled;
        _autoSaveInterval.Value = s.AutoSaveIntervalWeeks;
        _showInjuryNotifs.ButtonPressed = s.ShowInjuryNotifications;
        _showPhaseNotifs.ButtonPressed = s.ShowPhaseNotifications;
        _showAwardNotifs.ButtonPressed = s.ShowAwardNotifications;
        _durationSlider.Value = s.NotificationDuration;
        _durationValue.Text = s.NotificationDuration.ToString("F1");
        _confirmCuts.ButtonPressed = s.ConfirmCutPlayers;
    }

    private void OnSavePressed()
    {
        var s = SettingsManager.Current;

        s.SimSpeedMs = (int)_simSpeedSlider.Value;
        s.AutoSaveEnabled = _autoSaveEnabled.ButtonPressed;
        s.AutoSaveIntervalWeeks = (int)_autoSaveInterval.Value;
        s.ShowInjuryNotifications = _showInjuryNotifs.ButtonPressed;
        s.ShowPhaseNotifications = _showPhaseNotifs.ButtonPressed;
        s.ShowAwardNotifications = _showAwardNotifs.ButtonPressed;
        s.NotificationDuration = (float)_durationSlider.Value;
        s.ConfirmCutPlayers = _confirmCuts.ButtonPressed;

        SettingsManager.Save();
        QueueFree();
    }

    private void OnResetPressed()
    {
        // Reset to defaults by creating a new GameSettings
        var fresh = new GameSettings();
        var s = SettingsManager.Current;
        s.SimSpeedMs = fresh.SimSpeedMs;
        s.AutoSaveEnabled = fresh.AutoSaveEnabled;
        s.AutoSaveIntervalWeeks = fresh.AutoSaveIntervalWeeks;
        s.ShowInjuryNotifications = fresh.ShowInjuryNotifications;
        s.ShowPhaseNotifications = fresh.ShowPhaseNotifications;
        s.ShowAwardNotifications = fresh.ShowAwardNotifications;
        s.NotificationDuration = fresh.NotificationDuration;
        s.ConfirmCutPlayers = fresh.ConfirmCutPlayers;

        PopulateFromSettings();
    }
}
