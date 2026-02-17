using Godot;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI.Components;

public static class OverallBadge
{
    public static Color GetOverallColor(int overall)
    {
        return ThemeColors.GetRatingColor(overall);
    }

    public static Label CreateBadgeLabel(int overall, int fontSize = ThemeFonts.Subtitle)
    {
        var label = new Label
        {
            Text = overall.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(40, 0),
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", GetOverallColor(overall));
        return label;
    }
}
