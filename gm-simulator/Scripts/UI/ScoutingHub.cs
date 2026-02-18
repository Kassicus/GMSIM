using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class ScoutingHub : Control
{
    private Label _budgetLabel = null!;
    private OptionButton _posFilter = null!;
    private OptionButton _gradeFilter = null!;
    private OptionButton _roundFilter = null!;
    private LineEdit _searchField = null!;
    private VBoxContainer _scoutList = null!;
    private VBoxContainer _prospectList = null!;

    private PackedScene _prospectCardScene = null!;
    private const int MaxResults = 100;

    public override void _Ready()
    {
        _budgetLabel = GetNode<Label>("MarginContainer/VBox/HeaderHBox/BudgetLabel");
        _posFilter = GetNode<OptionButton>("MarginContainer/VBox/FilterHBox/PosFilter");
        _gradeFilter = GetNode<OptionButton>("MarginContainer/VBox/FilterHBox/GradeFilter");
        _roundFilter = GetNode<OptionButton>("MarginContainer/VBox/FilterHBox/RoundFilter");
        _searchField = GetNode<LineEdit>("MarginContainer/VBox/FilterHBox/SearchField");
        _scoutList = GetNode<VBoxContainer>("MarginContainer/VBox/HSplit/ScoutPanel/ScoutVBox/ScoutList");
        _prospectList = GetNode<VBoxContainer>("MarginContainer/VBox/HSplit/ProspectPanel/ProspectScroll/ProspectList");

        _prospectCardScene = GD.Load<PackedScene>("res://Scenes/Scouting/ProspectCard.tscn");

        SetupFilters();
        RefreshScoutList();
        RefreshProspectList();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.ProspectScouted += OnProspectScouted;
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
        }
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.ProspectScouted -= OnProspectScouted;
            EventBus.Instance.WeekAdvanced -= OnWeekAdvanced;
        }
    }

    private void SetupFilters()
    {
        _posFilter.AddItem("All Positions", 0);
        int idx = 1;
        foreach (Pos pos in Enum.GetValues<Pos>())
        {
            _posFilter.AddItem(pos.ToString(), idx++);
        }

        _gradeFilter.AddItem("All Grades", 0);
        _gradeFilter.AddItem("Unscouted", 1);
        _gradeFilter.AddItem("Initial", 2);
        _gradeFilter.AddItem("Intermediate", 3);
        _gradeFilter.AddItem("Advanced", 4);
        _gradeFilter.AddItem("Fully Scouted", 5);

        _roundFilter.AddItem("All Rounds", 0);
        for (int r = 1; r <= 7; r++)
            _roundFilter.AddItem($"Round {r}", r);
        _roundFilter.AddItem("UDFA", 8);
    }

    private void RefreshScoutList()
    {
        foreach (var child in _scoutList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        _budgetLabel.Text = $"Points: {gm.Scouting.CurrentPoints} / {gm.Scouting.WeeklyPointPool} (Cost: {Systems.ScoutingSystem.CostPerAction}/scout)";

        foreach (var scout in gm.Scouts)
        {
            var hbox = new HBoxContainer();

            var nameLabel = new Label
            {
                Text = $"{scout.Name} (Acc:{scout.Accuracy} Spd:{scout.Speed})",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(nameLabel);

            int contribution = (scout.Accuracy + scout.Speed) / 2;
            var contribLabel = new Label { Text = $"+{contribution} pts/wk" };
            contribLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            contribLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
            hbox.AddChild(contribLabel);

            var fireBtn = new Button { Text = "Fire", CustomMinimumSize = new Vector2(50, 0) };
            fireBtn.Disabled = gm.Scouts.Count <= 1;
            string sid = scout.Id;
            fireBtn.Pressed += () => OnFireScout(sid);
            hbox.AddChild(fireBtn);

            _scoutList.AddChild(hbox);
        }

        // Scout Market section
        var sep = new HSeparator();
        _scoutList.AddChild(sep);

        var marketHeader = new Label
        {
            Text = $"AVAILABLE SCOUTS ({gm.ScoutMarket.Count})",
        };
        marketHeader.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
        marketHeader.AddThemeColorOverride("font_color", ThemeColors.Warning);
        _scoutList.AddChild(marketHeader);

        if (gm.ScoutMarket.Count == 0)
        {
            var emptyLabel = new Label { Text = "No scouts available. Market refreshes each offseason." };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            emptyLabel.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);
            _scoutList.AddChild(emptyLabel);
        }
        else
        {
            foreach (var scout in gm.ScoutMarket)
            {
                var hbox = new HBoxContainer();

                var nameLabel = new Label
                {
                    Text = $"{scout.Name} (Acc:{scout.Accuracy} Spd:{scout.Speed})",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
                hbox.AddChild(nameLabel);

                var specLabel = new Label
                {
                    Text = scout.Specialty.ToString(),
                    CustomMinimumSize = new Vector2(80, 0),
                };
                specLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
                hbox.AddChild(specLabel);

                var salaryLabel = new Label
                {
                    Text = $"${scout.Salary:N0}/yr",
                    CustomMinimumSize = new Vector2(80, 0),
                };
                salaryLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
                hbox.AddChild(salaryLabel);

                var hireBtn = new Button { Text = "Hire", CustomMinimumSize = new Vector2(50, 0) };
                hireBtn.Disabled = gm.Scouts.Count >= 10;
                string mid = scout.Id;
                hireBtn.Pressed += () => OnHireScout(mid);
                hbox.AddChild(hireBtn);

                _scoutList.AddChild(hbox);
            }
        }
    }

    private void RefreshProspectList()
    {
        foreach (var child in _prospectList.GetChildren())
            child.QueueFree();

        var gm = GameManager.Instance;
        if (gm == null) return;

        var prospects = FilterProspects(gm.CurrentDraftClass);
        int shown = 0;

        foreach (var prospect in prospects)
        {
            if (shown >= MaxResults) break;

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);

            var nameLabel = new Label
            {
                Text = prospect.FullName,
                CustomMinimumSize = new Vector2(140, 0),
            };
            nameLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(nameLabel);

            var posLabel = new Label
            {
                Text = prospect.Position.ToString(),
                CustomMinimumSize = new Vector2(50, 0),
            };
            posLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(posLabel);

            var collegeLabel = new Label
            {
                Text = prospect.College,
                CustomMinimumSize = new Vector2(100, 0),
                ClipText = true,
            };
            collegeLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(collegeLabel);

            var gradeLabel = new Label
            {
                Text = prospect.ScoutGrade.ToString(),
                CustomMinimumSize = new Vector2(80, 0),
            };
            gradeLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            gradeLabel.AddThemeColorOverride("font_color", GetGradeColor(prospect.ScoutGrade));
            hbox.AddChild(gradeLabel);

            var roundLabel = new Label
            {
                Text = $"Rd {prospect.ProjectedRound}",
                CustomMinimumSize = new Vector2(40, 0),
            };
            roundLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            hbox.AddChild(roundLabel);

            var talentLabel = new Label
            {
                CustomMinimumSize = new Vector2(50, 0),
            };
            talentLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.Small);
            if (prospect.ScoutGrade == ScoutingGrade.FullyScouted)
            {
                talentLabel.Text = prospect.TalentRound <= 7 ? $"Tal {prospect.TalentRound}" : "UDFA";
                if (prospect.TalentRound < prospect.ProjectedRound)
                    talentLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
                else if (prospect.TalentRound > prospect.ProjectedRound)
                    talentLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
            }
            else
            {
                talentLabel.Text = "?";
                talentLabel.AddThemeColorOverride("font_color", ThemeColors.TextTertiary);
            }
            hbox.AddChild(talentLabel);

            var viewBtn = new Button { Text = "View", CustomMinimumSize = new Vector2(50, 0) };
            string pid = prospect.Id;
            viewBtn.Pressed += () => OpenProspectCard(pid);
            hbox.AddChild(viewBtn);

            var scoutBtn = new Button { Text = "Scout", CustomMinimumSize = new Vector2(55, 0) };
            scoutBtn.Pressed += () => OnScoutProspect(pid);
            scoutBtn.Disabled = prospect.ScoutGrade == ScoutingGrade.FullyScouted
                || gm.Scouting.CurrentPoints < Systems.ScoutingSystem.CostPerAction;
            hbox.AddChild(scoutBtn);

            bool onBoard = gm.DraftBoardOrder.Contains(pid);
            var boardBtn = new Button { Text = onBoard ? "On Board" : "+ Board", CustomMinimumSize = new Vector2(70, 0) };
            boardBtn.Disabled = onBoard;
            boardBtn.Pressed += () => AddToBoard(pid);
            hbox.AddChild(boardBtn);

            _prospectList.AddChild(hbox);
            shown++;
        }
    }

    private List<Prospect> FilterProspects(List<Prospect> all)
    {
        IEnumerable<Prospect> filtered = all;

        // Position filter
        int posIdx = _posFilter.Selected;
        if (posIdx > 0)
        {
            var positions = Enum.GetValues<Pos>();
            if (posIdx - 1 < positions.Length)
            {
                var targetPos = positions[posIdx - 1];
                filtered = filtered.Where(p => p.Position == targetPos);
            }
        }

        // Grade filter
        int gradeIdx = _gradeFilter.Selected;
        if (gradeIdx > 0)
        {
            var targetGrade = (ScoutingGrade)(gradeIdx - 1);
            filtered = filtered.Where(p => p.ScoutGrade == targetGrade);
        }

        // Round filter (projected round)
        int roundIdx = _roundFilter.Selected;
        if (roundIdx > 0)
        {
            int targetRound = roundIdx; // 1-7 map directly, 8 = UDFA
            filtered = filtered.Where(p => p.ProjectedRound == targetRound ||
                (targetRound == 8 && p.ProjectedRound > 7));
        }

        // Search filter
        string search = _searchField.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(p =>
                p.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.College.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.OrderByDescending(p => p.DraftValue).ToList();
    }

    private void OpenProspectCard(string prospectId)
    {
        var card = _prospectCardScene.Instantiate<ProspectCard>();
        card.Initialize(prospectId);
        GetTree().Root.AddChild(card);
    }

    private void OnScoutProspect(string prospectId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.Scouting.ScoutProspect(prospectId);
        RefreshScoutList();
        RefreshProspectList();
    }

    private void AddToBoard(string prospectId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (!gm.DraftBoardOrder.Contains(prospectId))
        {
            gm.DraftBoardOrder.Add(prospectId);
            RefreshProspectList();
        }
    }

    private Color GetGradeColor(ScoutingGrade grade) => ThemeColors.GetScoutGradeColor(grade);

    private void OnFireScout(string scoutId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, message) = gm.FireScout(scoutId);
        GD.Print($"Fire scout: {message}");
        if (success)
            RefreshScoutList();
    }

    private void OnHireScout(string scoutId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, message) = gm.HireScout(scoutId);
        GD.Print($"Hire scout: {message}");
        if (success)
            RefreshScoutList();
    }

    // Signal handlers
    private void OnFilterChanged(int _idx) => RefreshProspectList();
    private void OnSearchChanged(string _text) => RefreshProspectList();
    private void OnWeekAdvanced(int _year, int _week) { RefreshScoutList(); RefreshProspectList(); }
    private void OnProspectScouted(string _id, int _grade) { RefreshScoutList(); RefreshProspectList(); }
}
