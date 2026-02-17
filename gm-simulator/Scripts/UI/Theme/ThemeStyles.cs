using Godot;

namespace GMSimulator.UI.Theme;

public static class ThemeStyles
{
    // === PANELS / CARDS ===

    public static StyleBoxFlat CardPanel()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.BgSurface;
        s.SetBorderWidthAll(1);
        s.BorderColor = ThemeColors.BorderMuted;
        s.SetCornerRadiusAll(ThemeSpacing.RadiusMD);
        s.SetContentMarginAll(ThemeSpacing.CardPadding);
        return s;
    }

    public static StyleBoxFlat ElevatedPanel()
    {
        var s = CardPanel();
        s.BgColor = ThemeColors.BgElevated;
        s.BorderColor = ThemeColors.Border;
        return s;
    }

    public static StyleBoxFlat SurfacePanel()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.BgSurface;
        s.SetCornerRadiusAll(ThemeSpacing.RadiusSM);
        s.SetContentMarginAll(ThemeSpacing.XS);
        return s;
    }

    // === TOP BAR ===

    public static StyleBoxFlat TopBar()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.TopBarBg;
        s.BorderWidthBottom = 1;
        s.BorderColor = ThemeColors.Border;
        s.ContentMarginLeft = ThemeSpacing.TopBarPadding;
        s.ContentMarginRight = ThemeSpacing.TopBarPadding;
        s.ContentMarginTop = ThemeSpacing.XS;
        s.ContentMarginBottom = ThemeSpacing.XS;
        return s;
    }

    // === NAV BAR ===

    public static StyleBoxFlat NavBar()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.NavBg;
        s.BorderWidthBottom = 1;
        s.BorderColor = ThemeColors.BorderMuted;
        s.ContentMarginLeft = ThemeSpacing.SM;
        s.ContentMarginRight = ThemeSpacing.SM;
        s.ContentMarginTop = ThemeSpacing.XXS;
        s.ContentMarginBottom = ThemeSpacing.XXS;
        return s;
    }

    public static StyleBoxFlat NavItemNormal()
    {
        var s = new StyleBoxFlat();
        s.BgColor = Colors.Transparent;
        s.SetCornerRadiusAll(ThemeSpacing.RadiusPill);
        s.ContentMarginLeft = ThemeSpacing.SM;
        s.ContentMarginRight = ThemeSpacing.SM;
        s.ContentMarginTop = ThemeSpacing.XXS;
        s.ContentMarginBottom = ThemeSpacing.XXS;
        return s;
    }

    public static StyleBoxFlat NavItemHover()
    {
        var s = NavItemNormal();
        s.BgColor = ThemeColors.BgSurfaceHover;
        return s;
    }

    public static StyleBoxFlat NavItemActive()
    {
        var s = NavItemNormal();
        s.BgColor = ThemeColors.AccentMuted;
        s.SetBorderWidthAll(1);
        s.BorderColor = ThemeColors.Accent;
        return s;
    }

    // === BUTTONS ===

    public static StyleBoxFlat PrimaryButton()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.Accent;
        s.SetCornerRadiusAll(ThemeSpacing.RadiusSM);
        s.ContentMarginLeft = ThemeSpacing.MD;
        s.ContentMarginRight = ThemeSpacing.MD;
        s.ContentMarginTop = ThemeSpacing.XS;
        s.ContentMarginBottom = ThemeSpacing.XS;
        return s;
    }

    public static StyleBoxFlat PrimaryButtonHover()
    {
        var s = PrimaryButton();
        s.BgColor = ThemeColors.AccentHover;
        return s;
    }

    public static StyleBoxFlat SecondaryButton()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.BgSurface;
        s.SetBorderWidthAll(1);
        s.BorderColor = ThemeColors.Border;
        s.SetCornerRadiusAll(ThemeSpacing.RadiusSM);
        s.ContentMarginLeft = ThemeSpacing.SM;
        s.ContentMarginRight = ThemeSpacing.SM;
        s.ContentMarginTop = ThemeSpacing.XXS;
        s.ContentMarginBottom = ThemeSpacing.XXS;
        return s;
    }

    public static StyleBoxFlat SecondaryButtonHover()
    {
        var s = SecondaryButton();
        s.BgColor = ThemeColors.BgSurfaceHover;
        return s;
    }

    public static StyleBoxFlat DangerButton()
    {
        var s = PrimaryButton();
        s.BgColor = ThemeColors.DangerMuted;
        s.SetBorderWidthAll(1);
        s.BorderColor = ThemeColors.Danger;
        return s;
    }

    // === HIGHLIGHT ROW ===

    public static StyleBoxFlat HighlightRow()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.PlayerHighlight;
        s.SetCornerRadiusAll(ThemeSpacing.RadiusSM);
        return s;
    }

    // === NOTIFICATION TOAST ===

    public static StyleBoxFlat NotificationToast(int priority)
    {
        var s = new StyleBoxFlat();
        s.BgColor = priority switch
        {
            3 => ThemeColors.NotifAlertBg,
            2 => ThemeColors.NotifAwardBg,
            1 => ThemeColors.NotifInfoBg,
            _ => ThemeColors.NotifDefaultBg,
        };
        s.BorderWidthLeft = 4;
        s.BorderColor = priority switch
        {
            3 => ThemeColors.Danger,
            2 => ThemeColors.Warning,
            1 => ThemeColors.Info,
            _ => ThemeColors.TextTertiary,
        };
        s.SetCornerRadiusAll(ThemeSpacing.RadiusMD);
        s.ContentMarginLeft = ThemeSpacing.SM;
        s.ContentMarginRight = ThemeSpacing.SM;
        s.ContentMarginTop = ThemeSpacing.XS;
        s.ContentMarginBottom = ThemeSpacing.XS;
        return s;
    }

    // === ATTRIBUTE BAR ===

    public static StyleBoxFlat AttributeBarBg()
    {
        var s = new StyleBoxFlat();
        s.BgColor = ThemeColors.BgOverlay;
        s.SetCornerRadiusAll(2);
        return s;
    }

    public static StyleBoxFlat ProgressFill(Color color)
    {
        var s = new StyleBoxFlat();
        s.BgColor = color;
        s.SetCornerRadiusAll(2);
        return s;
    }
}
