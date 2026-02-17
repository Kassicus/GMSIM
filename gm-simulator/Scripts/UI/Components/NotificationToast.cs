using Godot;
using GMSimulator.Core;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI.Components;

public partial class NotificationToast : PanelContainer
{
    private float _timer;
    private bool _fading;

    public static NotificationToast Create(string title, string message, int priority)
    {
        var toast = new NotificationToast();
        toast.CustomMinimumSize = new Vector2(300, 0);
        toast.AddThemeStyleboxOverride("panel", ThemeStyles.NotificationToast(priority));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", ThemeSpacing.XXS);

        var accentColor = priority switch
        {
            3 => ThemeColors.Danger,
            2 => ThemeColors.Warning,
            1 => ThemeColors.Info,
            _ => ThemeColors.TextTertiary,
        };

        var titleLabel = new Label { Text = title };
        titleLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
        titleLabel.AddThemeColorOverride("font_color", accentColor);
        vbox.AddChild(titleLabel);

        var msgLabel = new Label
        {
            Text = message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        msgLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        msgLabel.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);
        vbox.AddChild(msgLabel);

        toast.AddChild(vbox);
        return toast;
    }

    public override void _Process(double delta)
    {
        float duration = SettingsManager.Current.NotificationDuration;
        _timer += (float)delta;

        if (!_fading && _timer >= duration)
            _fading = true;

        if (_fading)
        {
            float fadeProgress = (_timer - duration) / 0.5f;
            Modulate = new Color(1, 1, 1, 1f - fadeProgress);

            if (fadeProgress >= 1f)
                QueueFree();
        }
    }
}
