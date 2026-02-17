using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class CoachCard : Window
{
    private Label _nameLabel = null!;
    private Label _infoLabel = null!;
    private Label _teamLabel = null!;
    private Label _schemeLabel = null!;
    private VBoxContainer _ratingsList = null!;
    private Label _recordLabel = null!;
    private Label _prestigeLabel = null!;

    private string _coachId = string.Empty;

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("PanelContainer/ScrollContainer/MarginContainer/VBox/NameLabel");
        _infoLabel = GetNode<Label>("PanelContainer/ScrollContainer/MarginContainer/VBox/InfoLabel");
        _teamLabel = GetNode<Label>("PanelContainer/ScrollContainer/MarginContainer/VBox/TeamLabel");
        _schemeLabel = GetNode<Label>("PanelContainer/ScrollContainer/MarginContainer/VBox/SchemeLabel");
        _ratingsList = GetNode<VBoxContainer>("PanelContainer/ScrollContainer/MarginContainer/VBox/RatingsList");
        _recordLabel = GetNode<Label>("PanelContainer/ScrollContainer/MarginContainer/VBox/RecordLabel");
        _prestigeLabel = GetNode<Label>("PanelContainer/ScrollContainer/MarginContainer/VBox/PrestigeLabel");

        CloseRequested += OnClosePressed;
        PopulateCard();
    }

    public void Initialize(string coachId)
    {
        _coachId = coachId;
    }

    private void PopulateCard()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var coach = gm.GetCoach(_coachId);
        if (coach == null) return;

        _nameLabel.Text = coach.FullName;
        _infoLabel.Text = $"{FormatRole(coach.Role)} | Age {coach.Age} | {coach.Personality}";

        if (coach.TeamId != null)
        {
            var team = gm.GetTeam(coach.TeamId);
            _teamLabel.Text = $"Team: {team?.FullName ?? "Unknown"}";
        }
        else
        {
            _teamLabel.Text = "Team: Free Agent";
        }

        _schemeLabel.Text = $"Schemes: {coach.PreferredOffense} / {coach.PreferredDefense}";

        // Ratings
        AddRatingBar("Offense", coach.OffenseRating);
        AddRatingBar("Defense", coach.DefenseRating);
        AddRatingBar("Special Teams", coach.SpecialTeamsRating);
        AddRatingBar("Game Management", coach.GameManagement);
        AddRatingBar("Player Development", coach.PlayerDevelopment);
        AddRatingBar("Motivation", coach.Motivation);
        AddRatingBar("Adaptability", coach.Adaptability);
        AddRatingBar("Recruiting", coach.Recruiting);

        // Career record
        _recordLabel.Text = $"W-L: {coach.CareerWins}-{coach.CareerLosses} | " +
            $"Playoffs: {coach.PlayoffAppearances} | Super Bowls: {coach.SuperBowlWins}";
        _prestigeLabel.Text = $"Prestige: {coach.Prestige} | Experience: {coach.Experience} yrs";
    }

    private void AddRatingBar(string name, int value)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label
        {
            Text = name,
            CustomMinimumSize = new Vector2(140, 0),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        hbox.AddChild(nameLabel);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 99,
            Value = value,
            CustomMinimumSize = new Vector2(200, 20),
            ShowPercentage = false,
        };

        // Color code using theme
        Color fillColor = ThemeColors.GetRatingColor(value);
        bar.AddThemeStyleboxOverride("fill", ThemeStyles.ProgressFill(fillColor));
        hbox.AddChild(bar);

        var valLabel = new Label { Text = value.ToString() };
        valLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        hbox.AddChild(valLabel);

        _ratingsList.AddChild(hbox);
    }

    private static string FormatRole(CoachRole role)
    {
        return role switch
        {
            CoachRole.HeadCoach => "Head Coach",
            CoachRole.OffensiveCoordinator => "Off. Coordinator",
            CoachRole.DefensiveCoordinator => "Def. Coordinator",
            CoachRole.SpecialTeamsCoordinator => "ST Coordinator",
            CoachRole.QBCoach => "QB Coach",
            CoachRole.RBCoach => "RB Coach",
            CoachRole.WRCoach => "WR Coach",
            CoachRole.OLineCoach => "OL Coach",
            CoachRole.DLineCoach => "DL Coach",
            CoachRole.LBCoach => "LB Coach",
            CoachRole.DBCoach => "DB Coach",
            _ => role.ToString(),
        };
    }

    private void OnClosePressed()
    {
        QueueFree();
    }
}
