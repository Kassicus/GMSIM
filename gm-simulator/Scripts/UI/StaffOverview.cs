using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;

namespace GMSimulator.UI;

public partial class StaffOverview : Control
{
    private Label _schemeFitLabel = null!;
    private VBoxContainer _staffList = null!;
    private VBoxContainer _marketList = null!;

    private PackedScene _coachCardScene = null!;

    public override void _Ready()
    {
        _schemeFitLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/HeaderHBox/SchemeFitLabel");
        _staffList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/StaffList");
        _marketList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/MarketList");

        _coachCardScene = GD.Load<PackedScene>("res://Scenes/Staff/CoachCard.tscn");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.CoachHired += OnCoachChanged;
            EventBus.Instance.CoachFired += OnCoachFired;
            EventBus.Instance.CoachingCarouselCompleted += OnCarouselCompleted;
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.CoachHired -= OnCoachChanged;
            EventBus.Instance.CoachFired -= OnCoachFired;
            EventBus.Instance.CoachingCarouselCompleted -= OnCarouselCompleted;
        }
    }

    private void Refresh()
    {
        RefreshSchemeFit();
        RefreshStaffList();
        RefreshMarketList();
    }

    private void RefreshSchemeFit()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        float fit = gm.Staff.CalculateSchemeFit(team);
        string fitText;
        Color fitColor;

        if (fit > 0.3f)
        {
            fitText = "GOOD";
            fitColor = ThemeColors.Success;
        }
        else if (fit > -0.1f)
        {
            fitText = "AVERAGE";
            fitColor = ThemeColors.Warning;
        }
        else
        {
            fitText = "POOR";
            fitColor = ThemeColors.Danger;
        }

        _schemeFitLabel.Text = $"Scheme Fit: {fitText} ({fit:+0.00;-0.00})";
        _schemeFitLabel.AddThemeColorOverride("font_color", fitColor);
    }

    private void RefreshStaffList()
    {
        foreach (var child in _staffList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Header row
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        AddLabel(header, "Role", 160, ThemeFonts.Body, true);
        AddLabel(header, "Name", 160, ThemeFonts.Body, true);
        AddLabel(header, "Key Rating", 80, ThemeFonts.Body, true);
        AddLabel(header, "Scheme", 120, ThemeFonts.Body, true);
        AddLabel(header, "Dev", 40, ThemeFonts.Body, true);
        AddLabel(header, "", 80, ThemeFonts.Body, false);
        _staffList.AddChild(header);
        _staffList.AddChild(new HSeparator());

        // HC
        AddCoachRow(team.HeadCoachId, "Head Coach", CoachRole.HeadCoach, true);

        // OC
        AddCoachRow(team.OffensiveCoordinatorId, "Off. Coordinator", CoachRole.OffensiveCoordinator, true);

        // DC
        AddCoachRow(team.DefensiveCoordinatorId, "Def. Coordinator", CoachRole.DefensiveCoordinator, true);

        // STC
        AddCoachRow(team.SpecialTeamsCoordId, "ST Coordinator", CoachRole.SpecialTeamsCoordinator, false);

        _staffList.AddChild(new HSeparator());

        // Position coaches
        var posCoachRoles = new[]
        {
            (CoachRole.QBCoach, "QB Coach"), (CoachRole.RBCoach, "RB Coach"),
            (CoachRole.WRCoach, "WR Coach"), (CoachRole.OLineCoach, "OL Coach"),
            (CoachRole.DLineCoach, "DL Coach"), (CoachRole.LBCoach, "LB Coach"),
            (CoachRole.DBCoach, "DB Coach"),
        };

        foreach (var (role, label) in posCoachRoles)
        {
            string? coachId = FindPositionCoachId(team, role);
            AddCoachRow(coachId, label, role, false);
        }
    }

    private void AddCoachRow(string? coachId, string roleLabel, CoachRole role, bool showScheme)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);

        AddLabel(hbox, roleLabel, 160, ThemeFonts.Small, false);

        if (coachId != null)
        {
            var coach = gm.GetCoach(coachId);
            if (coach != null)
            {
                // Clickable name
                var nameBtn = new Button
                {
                    Text = coach.FullName,
                    CustomMinimumSize = new Vector2(160, 0),
                    Flat = true,
                };
                nameBtn.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
                string cid = coach.Id;
                nameBtn.Pressed += () => ShowCoachCard(cid);
                hbox.AddChild(nameBtn);

                // Key rating based on role
                string keyRating = role switch
                {
                    CoachRole.HeadCoach => $"GM: {coach.GameManagement}",
                    CoachRole.OffensiveCoordinator => $"OFF: {coach.OffenseRating}",
                    CoachRole.DefensiveCoordinator => $"DEF: {coach.DefenseRating}",
                    CoachRole.SpecialTeamsCoordinator => $"ST: {coach.SpecialTeamsRating}",
                    _ => $"DEV: {coach.PlayerDevelopment}",
                };
                AddLabel(hbox, keyRating, 80, ThemeFonts.Small, false);

                // Scheme
                if (showScheme)
                {
                    string scheme = role switch
                    {
                        CoachRole.OffensiveCoordinator or CoachRole.HeadCoach => coach.PreferredOffense.ToString(),
                        CoachRole.DefensiveCoordinator => coach.PreferredDefense.ToString(),
                        _ => "—",
                    };
                    AddLabel(hbox, scheme, 120, ThemeFonts.Small, false);
                }
                else
                {
                    AddLabel(hbox, "—", 120, ThemeFonts.Small, false);
                }

                // Player development
                AddLabel(hbox, coach.PlayerDevelopment.ToString(), 40, ThemeFonts.Small, false);

                // Fire button
                var fireBtn = new Button
                {
                    Text = "Fire",
                    CustomMinimumSize = new Vector2(80, 0),
                };
                fireBtn.Pressed += () => OnFireCoach(cid);
                hbox.AddChild(fireBtn);
            }
        }
        else
        {
            // Vacant position
            var vacantLabel = new Label
            {
                Text = "VACANT",
                CustomMinimumSize = new Vector2(160, 0),
            };
            vacantLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            vacantLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
            hbox.AddChild(vacantLabel);

            AddLabel(hbox, "—", 80, ThemeFonts.Small, false);
            AddLabel(hbox, "—", 120, ThemeFonts.Small, false);
            AddLabel(hbox, "—", 40, ThemeFonts.Small, false);
            AddLabel(hbox, "", 80, ThemeFonts.Small, false);
        }

        _staffList.AddChild(hbox);
    }

    private void RefreshMarketList()
    {
        foreach (var child in _marketList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var market = gm.Staff.CoachingMarket;
        if (market.Count == 0)
        {
            var emptyLabel = new Label { Text = "No coaches available. Market opens during the offseason." };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _marketList.AddChild(emptyLabel);
            return;
        }

        // Header
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        AddLabel(header, "Name", 150, ThemeFonts.Body, true);
        AddLabel(header, "Age", 35, ThemeFonts.Body, true);
        AddLabel(header, "Exp", 35, ThemeFonts.Body, true);
        AddLabel(header, "OFF", 40, ThemeFonts.Body, true);
        AddLabel(header, "DEF", 40, ThemeFonts.Body, true);
        AddLabel(header, "GM", 40, ThemeFonts.Body, true);
        AddLabel(header, "DEV", 40, ThemeFonts.Body, true);
        AddLabel(header, "Prestige", 55, ThemeFonts.Body, true);
        AddLabel(header, "", 100, ThemeFonts.Body, false);
        _marketList.AddChild(header);
        _marketList.AddChild(new HSeparator());

        var team = gm.GetPlayerTeam();

        foreach (var coach in market.OrderByDescending(c => c.Prestige * 0.3f + c.GameManagement * 0.3f
            + c.OffenseRating * 0.2f + c.DefenseRating * 0.2f))
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);

            // Clickable name
            var nameBtn = new Button
            {
                Text = coach.FullName,
                CustomMinimumSize = new Vector2(150, 0),
                Flat = true,
            };
            nameBtn.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            string cid = coach.Id;
            nameBtn.Pressed += () => ShowCoachCard(cid);
            hbox.AddChild(nameBtn);

            AddLabel(hbox, coach.Age.ToString(), 35, ThemeFonts.Small, false);
            AddLabel(hbox, coach.Experience.ToString(), 35, ThemeFonts.Small, false);
            AddLabel(hbox, coach.OffenseRating.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.DefenseRating.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.GameManagement.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.PlayerDevelopment.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.Prestige.ToString(), 55, ThemeFonts.Small, false);

            // Hire button — show dropdown with vacant roles
            var hireBtn = new MenuButton
            {
                Text = "Hire As...",
                CustomMinimumSize = new Vector2(100, 0),
            };

            if (team != null)
            {
                var popup = hireBtn.GetPopup();
                AddHireOption(popup, team, coach, CoachRole.HeadCoach, "Head Coach");
                AddHireOption(popup, team, coach, CoachRole.OffensiveCoordinator, "Off. Coordinator");
                AddHireOption(popup, team, coach, CoachRole.DefensiveCoordinator, "Def. Coordinator");
                AddHireOption(popup, team, coach, CoachRole.SpecialTeamsCoordinator, "ST Coordinator");
                AddHireOption(popup, team, coach, CoachRole.QBCoach, "QB Coach");
                AddHireOption(popup, team, coach, CoachRole.RBCoach, "RB Coach");
                AddHireOption(popup, team, coach, CoachRole.WRCoach, "WR Coach");
                AddHireOption(popup, team, coach, CoachRole.OLineCoach, "OL Coach");
                AddHireOption(popup, team, coach, CoachRole.DLineCoach, "DL Coach");
                AddHireOption(popup, team, coach, CoachRole.LBCoach, "LB Coach");
                AddHireOption(popup, team, coach, CoachRole.DBCoach, "DB Coach");

                string coachId = coach.Id;
                popup.IdPressed += (id) => OnHireCoach(coachId, (CoachRole)(int)id);
            }

            hbox.AddChild(hireBtn);
            _marketList.AddChild(hbox);
        }
    }

    private void AddHireOption(PopupMenu popup, Team team, Coach coach, CoachRole role, string label)
    {
        bool filled = IsRoleFilled(team, role);
        popup.AddItem(filled ? $"{label} (filled)" : label, (int)role);
        int idx = popup.ItemCount - 1;
        popup.SetItemDisabled(idx, filled);
    }

    private bool IsRoleFilled(Team team, CoachRole role)
    {
        var gm = GameManager.Instance;
        if (gm == null) return true;

        return role switch
        {
            CoachRole.HeadCoach => team.HeadCoachId != null,
            CoachRole.OffensiveCoordinator => team.OffensiveCoordinatorId != null,
            CoachRole.DefensiveCoordinator => team.DefensiveCoordinatorId != null,
            CoachRole.SpecialTeamsCoordinator => team.SpecialTeamsCoordId != null,
            _ => team.PositionCoachIds.Any(id =>
            {
                var c = gm.GetCoach(id);
                return c != null && c.Role == role;
            }),
        };
    }

    private string? FindPositionCoachId(Team team, CoachRole role)
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;

        foreach (var id in team.PositionCoachIds)
        {
            var c = gm.GetCoach(id);
            if (c != null && c.Role == role) return id;
        }
        return null;
    }

    // --- Actions ---

    private void OnFireCoach(string coachId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, msg) = gm.Staff.FireCoach(coachId);
        GD.Print($"Fire coach: {success} — {msg}");
        Refresh();
    }

    private void OnHireCoach(string coachId, CoachRole role)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, msg) = gm.Staff.HireCoach(coachId, role);
        GD.Print($"Hire coach: {success} — {msg}");
        Refresh();
    }

    private void ShowCoachCard(string coachId)
    {
        var card = _coachCardScene.Instantiate<CoachCard>();
        card.Initialize(coachId);
        GetTree().Root.AddChild(card);
    }

    // --- Signal Handlers ---

    private void OnCoachChanged(string coachId, string teamId, int role) => Refresh();
    private void OnCoachFired(string coachId, string teamId) => Refresh();
    private void OnCarouselCompleted(int year) => Refresh();

    // --- Utility ---

    private static void AddLabel(HBoxContainer parent, string text, int minWidth, int fontSize, bool bold)
    {
        UIFactory.AddCell(parent, text, minWidth, fontSize);
    }
}
