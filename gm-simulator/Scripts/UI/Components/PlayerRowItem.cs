using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI.Components;

public partial class PlayerRowItem : Button
{
    private string _playerId = string.Empty;

    public static PlayerRowItem Create(Player player, int currentYear)
    {
        var row = new PlayerRowItem();
        row._playerId = player.Id;
        row.Flat = true;
        row.CustomMinimumSize = new Vector2(0, 32);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.Alignment = HorizontalAlignment.Left;

        var hbox = UIFactory.CreateRow(0);
        hbox.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(hbox);

        // Position
        UIFactory.AddCell(hbox, player.Position.ToString(), 55, ThemeFonts.Body, ThemeColors.TextSecondary);

        // Name
        UIFactory.AddCell(hbox, player.FullName, 180, ThemeFonts.Body, ThemeColors.TextPrimary, expandFill: true);

        // Archetype
        UIFactory.AddCell(hbox, player.Archetype.ToString(), 120, ThemeFonts.Body, ThemeColors.TextSecondary);

        // Overall (color-coded)
        UIFactory.AddCell(hbox, player.Overall.ToString(), 45, ThemeFonts.BodyLarge,
            ThemeColors.GetRatingColor(player.Overall));

        // Age
        UIFactory.AddCell(hbox, player.Age.ToString(), 40, ThemeFonts.Body, ThemeColors.TextSecondary);

        // Cap Hit
        string capHit = "--";
        if (player.CurrentContract != null)
            capHit = GameShell.FormatCurrency(player.CurrentContract.GetCapHit(currentYear));
        UIFactory.AddCell(hbox, capHit, 90, ThemeFonts.Body, ThemeColors.TextSecondary);

        // Status
        string status = player.RosterStatus switch
        {
            Models.Enums.RosterStatus.Active53 => "Active",
            Models.Enums.RosterStatus.PracticeSquad => "PS",
            Models.Enums.RosterStatus.InjuredReserve => "IR",
            _ => player.RosterStatus.ToString(),
        };
        Color statusColor = player.RosterStatus switch
        {
            Models.Enums.RosterStatus.InjuredReserve => ThemeColors.Danger,
            Models.Enums.RosterStatus.PracticeSquad => ThemeColors.TextTertiary,
            _ => ThemeColors.TextSecondary,
        };
        UIFactory.AddCell(hbox, status, 60, ThemeFonts.Body, statusColor);

        // Contract year indicator
        if (player.CurrentContract != null)
        {
            var contractYear = player.CurrentContract.Years
                .FirstOrDefault(y => y.Year == currentYear);
            if (contractYear != null)
            {
                int remaining = player.CurrentContract.TotalYears - contractYear.YearNumber + 1;
                string yrText = $"{remaining}yr";
                Color yrColor = remaining switch
                {
                    1 => ThemeColors.Danger,
                    2 => ThemeColors.Warning,
                    _ => ThemeColors.TextTertiary,
                };
                UIFactory.AddCell(hbox, yrText, 40, ThemeFonts.Small, yrColor);
            }
            else
            {
                UIFactory.AddCell(hbox, "", 40, ThemeFonts.Small);
            }
        }
        else
        {
            UIFactory.AddCell(hbox, "", 40, ThemeFonts.Small);
        }

        // Injury indicator
        if (player.CurrentInjury != null)
        {
            UIFactory.AddCell(hbox, "INJ", 30, ThemeFonts.Small, ThemeColors.Danger);
        }

        row.Pressed += () =>
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSelected, row._playerId);
        };

        return row;
    }
}
