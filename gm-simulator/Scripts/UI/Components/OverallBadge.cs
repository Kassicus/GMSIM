using Godot;

namespace GMSimulator.UI.Components;

/// <summary>
/// Static helper for overall rating color coding.
/// </summary>
public static class OverallBadge
{
    public static Color GetOverallColor(int overall)
    {
        return overall switch
        {
            >= 90 => new Color(1.0f, 0.85f, 0.0f),   // Gold
            >= 80 => new Color(0.3f, 0.9f, 0.3f),     // Green
            >= 70 => new Color(0.4f, 0.7f, 1.0f),     // Blue
            >= 60 => new Color(1.0f, 0.6f, 0.2f),     // Orange
            _ => new Color(1.0f, 0.3f, 0.3f),          // Red
        };
    }

    /// <summary>
    /// Creates a styled Label showing the overall rating with appropriate color.
    /// </summary>
    public static Label CreateBadgeLabel(int overall, int fontSize = 16)
    {
        var label = new Label
        {
            Text = overall.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(40, 0),
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.Modulate = GetOverallColor(overall);
        return label;
    }
}
