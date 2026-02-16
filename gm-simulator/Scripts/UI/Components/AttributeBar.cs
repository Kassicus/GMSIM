using Godot;

namespace GMSimulator.UI.Components;

/// <summary>
/// Creates a horizontal attribute bar showing name, value, and filled bar (0-99 scale).
/// </summary>
public static class AttributeBar
{
    public static HBoxContainer Create(string attrName, int value, float maxWidth = 150f)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);

        // Attribute name
        var nameLabel = new Label
        {
            Text = attrName,
            CustomMinimumSize = new Vector2(130, 0),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        hbox.AddChild(nameLabel);

        // Value label
        var valueLabel = new Label
        {
            Text = value.ToString(),
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(30, 0),
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 13);
        valueLabel.Modulate = OverallBadge.GetOverallColor(value);
        hbox.AddChild(valueLabel);

        // Bar background
        var barContainer = new Control
        {
            CustomMinimumSize = new Vector2(maxWidth, 12),
        };

        var bg = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.25f),
            Size = new Vector2(maxWidth, 12),
            Position = Vector2.Zero,
        };
        barContainer.AddChild(bg);

        // Bar fill
        float fillWidth = (value / 99f) * maxWidth;
        var fill = new ColorRect
        {
            Color = OverallBadge.GetOverallColor(value),
            Size = new Vector2(fillWidth, 12),
            Position = Vector2.Zero,
        };
        barContainer.AddChild(fill);

        hbox.AddChild(barContainer);

        return hbox;
    }
}
