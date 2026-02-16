using Godot;
using GMSimulator.Core;
using GMSimulator.Models;

namespace GMSimulator.UI.Components;

/// <summary>
/// A single player row in the roster list. Created programmatically.
/// Click emits EventBus.PlayerSelected.
/// </summary>
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

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);
        hbox.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(hbox);

        // Position
        AddLabel(hbox, player.Position.ToString(), 55, 13);

        // Name
        AddLabel(hbox, player.FullName, 180, 13, expandFill: true);

        // Archetype
        AddLabel(hbox, player.Archetype.ToString(), 120, 13);

        // Overall (color-coded)
        var ovrLabel = AddLabel(hbox, player.Overall.ToString(), 45, 14);
        ovrLabel.Modulate = OverallBadge.GetOverallColor(player.Overall);

        // Age
        AddLabel(hbox, player.Age.ToString(), 40, 13);

        // Cap Hit
        string capHit = "--";
        if (player.CurrentContract != null)
            capHit = GameShell.FormatCurrency(player.CurrentContract.GetCapHit(currentYear));
        AddLabel(hbox, capHit, 90, 13);

        // Status
        string status = player.RosterStatus switch
        {
            Models.Enums.RosterStatus.Active53 => "Active",
            Models.Enums.RosterStatus.PracticeSquad => "PS",
            Models.Enums.RosterStatus.InjuredReserve => "IR",
            _ => player.RosterStatus.ToString(),
        };
        var statusLabel = AddLabel(hbox, status, 60, 13);
        if (player.RosterStatus == Models.Enums.RosterStatus.InjuredReserve)
            statusLabel.Modulate = new Color(1.0f, 0.3f, 0.3f);
        else if (player.RosterStatus == Models.Enums.RosterStatus.PracticeSquad)
            statusLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);

        // Contract year indicator
        string yrText = "";
        if (player.CurrentContract != null)
        {
            var contractYear = player.CurrentContract.Years
                .FirstOrDefault(y => y.Year == currentYear);
            if (contractYear != null)
            {
                int remaining = player.CurrentContract.TotalYears - contractYear.YearNumber + 1;
                yrText = $"{remaining}yr";
                if (remaining == 1) // Final year
                    statusLabel = AddLabel(hbox, yrText, 40, 12, new Color(1.0f, 0.3f, 0.3f));
                else if (remaining == 2) // Penultimate year
                    statusLabel = AddLabel(hbox, yrText, 40, 12, new Color(1.0f, 0.85f, 0.0f));
                else
                    AddLabel(hbox, yrText, 40, 12);
            }
            else
            {
                AddLabel(hbox, "", 40, 12);
            }
        }
        else
        {
            AddLabel(hbox, "", 40, 12);
        }

        // Injury indicator
        if (player.CurrentInjury != null)
        {
            AddLabel(hbox, "INJ", 30, 12, new Color(1.0f, 0.3f, 0.3f));
        }

        row.Pressed += () =>
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSelected, row._playerId);
        };

        return row;
    }

    private static Label AddLabel(HBoxContainer parent, string text, float minWidth, int fontSize,
        Color? color = null, bool expandFill = false)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (expandFill)
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (color.HasValue)
            label.Modulate = color.Value;
        parent.AddChild(label);
        return label;
    }
}
