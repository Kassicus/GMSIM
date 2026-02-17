using Godot;

namespace GMSimulator.UI.Theme;

public static class UIFactory
{
    public static Label CreateLabel(
        string text,
        int fontSize = ThemeFonts.Body,
        Color? color = null,
        float minWidth = 0,
        HorizontalAlignment align = HorizontalAlignment.Left,
        bool expandFill = false)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
        };
        if (minWidth > 0)
            label.CustomMinimumSize = new Vector2(minWidth, 0);
        if (expandFill)
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? ThemeColors.TextPrimary);

        return label;
    }

    public static Label CreateColumnHeader(
        string text,
        float minWidth = 0,
        HorizontalAlignment align = HorizontalAlignment.Center)
    {
        return CreateLabel(text, ThemeFonts.ColumnHeader, ThemeColors.TextTertiary, minWidth, align);
    }

    public static Label CreateSectionTitle(string text)
    {
        return CreateLabel(text, ThemeFonts.Title, ThemeColors.AccentText);
    }

    public static Label CreateSubtitle(string text)
    {
        return CreateLabel(text, ThemeFonts.Subtitle, ThemeColors.AccentText);
    }

    public static Label CreateEmptyState(string text)
    {
        return CreateLabel(text, ThemeFonts.BodyLarge, ThemeColors.TextTertiary,
            align: HorizontalAlignment.Center);
    }

    public static Label CreateStatusLabel(string text, bool isSuccess)
    {
        return CreateLabel(text, ThemeFonts.Body,
            isSuccess ? ThemeColors.Success : ThemeColors.Danger);
    }

    public static Label AddCell(
        HBoxContainer parent,
        string text,
        float minWidth,
        int fontSize = ThemeFonts.Body,
        Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left,
        bool expandFill = false)
    {
        var label = CreateLabel(text, fontSize, color, minWidth, align, expandFill);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        parent.AddChild(label);
        return label;
    }

    public static PanelContainer CreateCard()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", ThemeStyles.CardPanel());
        return panel;
    }

    public static HBoxContainer CreateRow(int separation = ThemeSpacing.ColumnGap)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", separation);
        return hbox;
    }

    public static VBoxContainer CreateSection(int separation = ThemeSpacing.RowGap)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", separation);
        return vbox;
    }
}
