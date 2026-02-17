using Godot;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI.Components;

public static class AttributeBar
{
    public static HBoxContainer Create(string attrName, int value, float maxWidth = 150f)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", ThemeSpacing.XS);

        var nameLabel = new Label
        {
            Text = attrName,
            CustomMinimumSize = new Vector2(130, 0),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        nameLabel.AddThemeColorOverride("font_color", ThemeColors.TextSecondary);
        hbox.AddChild(nameLabel);

        var valueLabel = new Label
        {
            Text = value.ToString(),
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(30, 0),
        };
        valueLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Body);
        valueLabel.AddThemeColorOverride("font_color", ThemeColors.GetRatingColor(value));
        hbox.AddChild(valueLabel);

        var barContainer = new Control
        {
            CustomMinimumSize = new Vector2(maxWidth, 12),
        };

        var bg = new ColorRect
        {
            Color = ThemeColors.BgOverlay,
            Size = new Vector2(maxWidth, 12),
            Position = Vector2.Zero,
        };
        barContainer.AddChild(bg);

        float fillWidth = (value / 99f) * maxWidth;
        var fill = new ColorRect
        {
            Color = ThemeColors.GetRatingColor(value),
            Size = new Vector2(fillWidth, 12),
            Position = Vector2.Zero,
        };
        barContainer.AddChild(fill);

        hbox.AddChild(barContainer);

        return hbox;
    }
}
