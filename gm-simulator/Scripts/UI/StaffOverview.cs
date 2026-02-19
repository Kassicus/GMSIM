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

    // Market-mode containers (built dynamically)
    private TabContainer? _tabContainer;
    private VBoxContainer? _interviewCandidatesList;
    private VBoxContainer? _interviewTrackerList;
    private VBoxContainer? _incomingRequestsList;

    // Scene path references
    private PackedScene _coachCardScene = null!;

    // Original scene nodes (hidden during market mode)
    private Control _originalScroll = null!;

    public override void _Ready()
    {
        _originalScroll = GetNode<Control>("ScrollContainer");
        _schemeFitLabel = GetNode<Label>("ScrollContainer/MarginContainer/VBox/HeaderHBox/SchemeFitLabel");
        _staffList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/StaffList");
        _marketList = GetNode<VBoxContainer>("ScrollContainer/MarginContainer/VBox/MarketList");

        _coachCardScene = GD.Load<PackedScene>("res://Scenes/Staff/CoachCard.tscn");

        if (EventBus.Instance != null)
        {
            EventBus.Instance.CoachHired += OnCoachChanged;
            EventBus.Instance.CoachFired += OnCoachFired;
            EventBus.Instance.CoachingCarouselCompleted += OnCarouselCompleted;
            EventBus.Instance.CoachingMarketOpened += OnMarketOpened;
            EventBus.Instance.CoachingMarketClosed += OnMarketClosed;
            EventBus.Instance.CoachingMarketWeekProcessed += OnMarketWeekProcessed;
            EventBus.Instance.PlayerCoachTargeted += OnPlayerCoachTargeted;
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
            EventBus.Instance.CoachingMarketOpened -= OnMarketOpened;
            EventBus.Instance.CoachingMarketClosed -= OnMarketClosed;
            EventBus.Instance.CoachingMarketWeekProcessed -= OnMarketWeekProcessed;
            EventBus.Instance.PlayerCoachTargeted -= OnPlayerCoachTargeted;
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.IsCoachingMarketOpen)
            BuildMarketModeUI();
        else
            BuildNormalModeUI();
    }

    // =====================================================
    // NORMAL MODE (no market open)
    // =====================================================

    private void BuildNormalModeUI()
    {
        // Show original scroll, remove tab container if present
        _originalScroll.Visible = true;
        if (_tabContainer != null)
        {
            _tabContainer.QueueFree();
            _tabContainer = null;
        }

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

        AddCoachRow(_staffList, team.HeadCoachId, "Head Coach", CoachRole.HeadCoach, true);
        AddCoachRow(_staffList, team.OffensiveCoordinatorId, "Off. Coordinator", CoachRole.OffensiveCoordinator, true);
        AddCoachRow(_staffList, team.DefensiveCoordinatorId, "Def. Coordinator", CoachRole.DefensiveCoordinator, true);
        AddCoachRow(_staffList, team.SpecialTeamsCoordId, "ST Coordinator", CoachRole.SpecialTeamsCoordinator, false);

        _staffList.AddChild(new HSeparator());

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
            AddCoachRow(_staffList, coachId, label, role, false);
        }
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
            var emptyLabel = new Label { Text = "No coaches available. Market opens during the playoffs." };
            emptyLabel.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);
            _marketList.AddChild(emptyLabel);
            return;
        }

        BuildMarketCoachList(_marketList, market);
    }

    // =====================================================
    // MARKET MODE (tabbed UI when coaching market is open)
    // =====================================================

    private void BuildMarketModeUI()
    {
        // Hide original scroll
        _originalScroll.Visible = false;

        // Create or clear tab container
        if (_tabContainer != null)
            _tabContainer.QueueFree();

        _tabContainer = new TabContainer();
        _tabContainer.LayoutMode = 1;
        _tabContainer.AnchorsPreset = (int)LayoutPreset.FullRect;
        _tabContainer.AnchorRight = 1.0f;
        _tabContainer.AnchorBottom = 1.0f;
        _tabContainer.GrowHorizontal = GrowDirection.Both;
        _tabContainer.GrowVertical = GrowDirection.Both;
        AddChild(_tabContainer);

        // Tab 1: Your Staff
        var staffTab = BuildStaffTab();
        staffTab.Name = "Your Staff";
        _tabContainer.AddChild(staffTab);

        // Tab 2: Coaching Market
        var marketTab = BuildMarketTab();
        marketTab.Name = "Coaching Market";
        _tabContainer.AddChild(marketTab);

        // Tab 3: Interview Tracker
        var trackerTab = BuildInterviewTrackerTab();
        trackerTab.Name = "Interview Tracker";
        _tabContainer.AddChild(trackerTab);
    }

    private ScrollContainer BuildStaffTab()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var gm = GameManager.Instance;
        if (gm == null) return scroll;
        var team = gm.GetPlayerTeam();
        if (team == null) return scroll;

        // Header
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 20);
        var titleLabel = UIFactory.CreateLabel("COACHING STAFF", ThemeFonts.Title);
        headerRow.AddChild(titleLabel);

        float fit = gm.Staff.CalculateSchemeFit(team);
        string fitText = fit > 0.3f ? "GOOD" : fit > -0.1f ? "AVERAGE" : "POOR";
        Color fitColor = fit > 0.3f ? ThemeColors.Success : fit > -0.1f ? ThemeColors.Warning : ThemeColors.Danger;
        var fitLabel = UIFactory.CreateLabel($"Scheme Fit: {fitText} ({fit:+0.00;-0.00})", ThemeFonts.Body, fitColor);
        headerRow.AddChild(fitLabel);

        var marketStatusLabel = UIFactory.CreateLabel(
            $"Market Week {gm.CoachingMarketWeekNumber}", ThemeFonts.Body, ThemeColors.AccentText);
        headerRow.AddChild(marketStatusLabel);
        vbox.AddChild(headerRow);
        vbox.AddChild(new HSeparator());

        // Staff list
        var staffHeader = new HBoxContainer();
        staffHeader.AddThemeConstantOverride("separation", 8);
        AddLabel(staffHeader, "Role", 160, ThemeFonts.Body, true);
        AddLabel(staffHeader, "Name", 160, ThemeFonts.Body, true);
        AddLabel(staffHeader, "Key Rating", 80, ThemeFonts.Body, true);
        AddLabel(staffHeader, "Scheme", 120, ThemeFonts.Body, true);
        AddLabel(staffHeader, "Dev", 40, ThemeFonts.Body, true);
        AddLabel(staffHeader, "", 80, ThemeFonts.Body, false);
        AddLabel(staffHeader, "", 80, ThemeFonts.Body, false); // Extra column for Protect
        vbox.AddChild(staffHeader);
        vbox.AddChild(new HSeparator());

        bool hcVacant = team.HeadCoachId == null;

        AddCoachRowMarketMode(vbox, team.HeadCoachId, "Head Coach", CoachRole.HeadCoach, true, false, team);
        AddCoachRowMarketMode(vbox, team.OffensiveCoordinatorId, "Off. Coordinator", CoachRole.OffensiveCoordinator, true, hcVacant, team);
        AddCoachRowMarketMode(vbox, team.DefensiveCoordinatorId, "Def. Coordinator", CoachRole.DefensiveCoordinator, true, hcVacant, team);
        AddCoachRowMarketMode(vbox, team.SpecialTeamsCoordId, "ST Coordinator", CoachRole.SpecialTeamsCoordinator, false, false, team);

        vbox.AddChild(new HSeparator());

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
            AddCoachRowMarketMode(vbox, coachId, label, role, false, false, team);
        }

        // Incoming Interview Requests section
        var incomingRequests = gm.Staff.GetIncomingInterviewRequests();
        if (incomingRequests.Count > 0)
        {
            vbox.AddChild(new HSeparator());
            var incomingTitle = UIFactory.CreateLabel("INCOMING INTERVIEW REQUESTS", ThemeFonts.Subtitle, ThemeColors.Warning);
            vbox.AddChild(incomingTitle);

            foreach (var req in incomingRequests)
            {
                var coach = gm.GetCoach(req.CoachId);
                var reqTeam = gm.GetTeam(req.RequestingTeamId);
                if (coach == null || reqTeam == null) continue;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                AddLabel(row, $"{reqTeam.FullName} wants {coach.FullName} as {req.TargetRole}", 500, ThemeFonts.Small, false);
                AddLabel(row, req.Status.ToString(), 80, ThemeFonts.Small, false);
                vbox.AddChild(row);
            }
        }

        return scroll;
    }

    private void AddCoachRowMarketMode(VBoxContainer parent, string? coachId, string roleLabel, CoachRole role,
        bool showScheme, bool showProtect, Team team)
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

                string keyRating = role switch
                {
                    CoachRole.HeadCoach => $"GM: {coach.GameManagement}",
                    CoachRole.OffensiveCoordinator => $"OFF: {coach.OffenseRating}",
                    CoachRole.DefensiveCoordinator => $"DEF: {coach.DefenseRating}",
                    CoachRole.SpecialTeamsCoordinator => $"ST: {coach.SpecialTeamsRating}",
                    _ => $"DEV: {coach.PlayerDevelopment}",
                };
                AddLabel(hbox, keyRating, 80, ThemeFonts.Small, false);

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

                AddLabel(hbox, coach.PlayerDevelopment.ToString(), 40, ThemeFonts.Small, false);

                // Fire button
                var fireBtn = new Button
                {
                    Text = "Fire",
                    CustomMinimumSize = new Vector2(80, 0),
                };
                fireBtn.Pressed += () => OnFireCoach(cid);
                hbox.AddChild(fireBtn);

                // Protect toggle (only for coordinators when HC is vacant)
                if (showProtect && role != CoachRole.HeadCoach)
                {
                    var protectBtn = new CheckButton
                    {
                        Text = "Protect",
                        CustomMinimumSize = new Vector2(100, 0),
                        TooltipText = "Declare promotion intent — blocks other teams from interviewing for HC",
                    };

                    // Check current state
                    var outgoing = gm.Staff.GetOutgoingInterviewRequests();
                    bool isProtected = gm.Staff.InterviewRequests
                        .Any(r => r.CoachId == cid && r.BlockReason == BlockReason.PlannedPromotion);
                    // We need to check the promotion intents directly
                    // Since we can't access private fields, use the state
                    var state = gm.Staff.GetState();
                    protectBtn.ButtonPressed = state.PromotionIntents.Contains(cid);

                    string coachIdCopy = cid;
                    protectBtn.Toggled += (toggled) =>
                    {
                        if (toggled)
                            gm.Staff.DeclarePromotionIntent(coachIdCopy);
                        else
                            gm.Staff.RemovePromotionIntent(coachIdCopy);
                    };
                    hbox.AddChild(protectBtn);
                }

                // Promote to HC button (only for coordinators when HC is vacant)
                if (team.HeadCoachId == null && role != CoachRole.HeadCoach
                    && (role == CoachRole.OffensiveCoordinator || role == CoachRole.DefensiveCoordinator))
                {
                    var promoteBtn = new Button
                    {
                        Text = "Promote to HC",
                        CustomMinimumSize = new Vector2(110, 0),
                    };
                    string promoteId = cid;
                    promoteBtn.Pressed += () =>
                    {
                        var (success, msg) = gm.Staff.PromoteCoach(promoteId, CoachRole.HeadCoach);
                        GD.Print($"Promote: {success} — {msg}");
                        Refresh();
                    };
                    hbox.AddChild(promoteBtn);
                }
            }
        }
        else
        {
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

        parent.AddChild(hbox);
    }

    private ScrollContainer BuildMarketTab()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var gm = GameManager.Instance;
        if (gm == null) return scroll;

        // Section 1: Free Agents
        var freeAgentTitle = UIFactory.CreateLabel("FREE AGENT COACHES", ThemeFonts.Subtitle, ThemeColors.AccentText);
        vbox.AddChild(freeAgentTitle);

        var freeAgents = gm.Staff.CoachingMarket;
        if (freeAgents.Count == 0)
        {
            vbox.AddChild(UIFactory.CreateEmptyState("No free agent coaches available."));
        }
        else
        {
            BuildMarketCoachList(vbox, freeAgents);
        }

        vbox.AddChild(new HSeparator());

        // Section 2: Interview Candidates
        var interviewTitle = UIFactory.CreateLabel("INTERVIEW CANDIDATES", ThemeFonts.Subtitle, ThemeColors.AccentText);
        vbox.AddChild(interviewTitle);

        var candidateNote = UIFactory.CreateLabel(
            "Employed coaches on non-playoff teams (excluding HCs). Request interview to hire for your team.",
            ThemeFonts.Small, ThemeColors.TextSecondary);
        vbox.AddChild(candidateNote);

        var candidates = gm.Staff.GetInterviewCandidates();
        if (candidates.Count == 0)
        {
            vbox.AddChild(UIFactory.CreateEmptyState("No interview candidates available."));
        }
        else
        {
            BuildInterviewCandidateList(vbox, candidates);
        }

        return scroll;
    }

    private void BuildInterviewCandidateList(VBoxContainer parent, List<Coach> candidates)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Header
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        AddLabel(header, "Name", 150, ThemeFonts.Body, true);
        AddLabel(header, "Team", 100, ThemeFonts.Body, true);
        AddLabel(header, "Role", 100, ThemeFonts.Body, true);
        AddLabel(header, "OFF", 40, ThemeFonts.Body, true);
        AddLabel(header, "DEF", 40, ThemeFonts.Body, true);
        AddLabel(header, "GM", 40, ThemeFonts.Body, true);
        AddLabel(header, "DEV", 40, ThemeFonts.Body, true);
        AddLabel(header, "", 140, ThemeFonts.Body, false);
        parent.AddChild(header);
        parent.AddChild(new HSeparator());

        // Sort by quality
        var sorted = candidates
            .OrderByDescending(c => c.Prestige * 0.3f + c.GameManagement * 0.3f
                + c.OffenseRating * 0.2f + c.DefenseRating * 0.2f)
            .Take(30); // Limit displayed candidates

        foreach (var coach in sorted)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);

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

            var coachTeam = coach.TeamId != null ? gm.GetTeam(coach.TeamId) : null;
            AddLabel(hbox, coachTeam?.Abbreviation ?? "FA", 100, ThemeFonts.Small, false);
            AddLabel(hbox, coach.Role.ToString(), 100, ThemeFonts.Small, false);
            AddLabel(hbox, coach.OffenseRating.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.DefenseRating.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.GameManagement.ToString(), 40, ThemeFonts.Small, false);
            AddLabel(hbox, coach.PlayerDevelopment.ToString(), 40, ThemeFonts.Small, false);

            // Request Interview button with role dropdown
            var reqBtn = new MenuButton
            {
                Text = "Request Interview",
                CustomMinimumSize = new Vector2(140, 0),
            };

            var popup = reqBtn.GetPopup();
            var team = gm.GetPlayerTeam();
            if (team != null)
            {
                // Only show roles that are vacant on player's team
                AddInterviewRoleOption(popup, team, CoachRole.HeadCoach, "Head Coach");
                AddInterviewRoleOption(popup, team, CoachRole.OffensiveCoordinator, "Off. Coordinator");
                AddInterviewRoleOption(popup, team, CoachRole.DefensiveCoordinator, "Def. Coordinator");
                AddInterviewRoleOption(popup, team, CoachRole.SpecialTeamsCoordinator, "ST Coordinator");
                AddInterviewRoleOption(popup, team, CoachRole.QBCoach, "QB Coach");
                AddInterviewRoleOption(popup, team, CoachRole.RBCoach, "RB Coach");
                AddInterviewRoleOption(popup, team, CoachRole.WRCoach, "WR Coach");
                AddInterviewRoleOption(popup, team, CoachRole.OLineCoach, "OL Coach");
                AddInterviewRoleOption(popup, team, CoachRole.DLineCoach, "DL Coach");
                AddInterviewRoleOption(popup, team, CoachRole.LBCoach, "LB Coach");
                AddInterviewRoleOption(popup, team, CoachRole.DBCoach, "DB Coach");

                string coachId = coach.Id;
                popup.IdPressed += (id) => OnRequestInterview(coachId, (CoachRole)(int)id);
            }

            hbox.AddChild(reqBtn);
            parent.AddChild(hbox);
        }
    }

    private void AddInterviewRoleOption(PopupMenu popup, Team team, CoachRole role, string label)
    {
        bool filled = IsRoleFilled(team, role);
        popup.AddItem(filled ? $"{label} (filled)" : label, (int)role);
        int idx = popup.ItemCount - 1;
        popup.SetItemDisabled(idx, filled);
    }

    private ScrollContainer BuildInterviewTrackerTab()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var gm = GameManager.Instance;
        if (gm == null) return scroll;

        // Section 1: Outgoing requests
        var outTitle = UIFactory.CreateLabel("YOUR INTERVIEW REQUESTS", ThemeFonts.Subtitle, ThemeColors.AccentText);
        vbox.AddChild(outTitle);

        var outgoing = gm.Staff.GetOutgoingInterviewRequests();
        if (outgoing.Count == 0)
        {
            vbox.AddChild(UIFactory.CreateEmptyState("No interview requests sent yet."));
        }
        else
        {
            var outHeader = new HBoxContainer();
            outHeader.AddThemeConstantOverride("separation", 8);
            AddLabel(outHeader, "Coach", 150, ThemeFonts.Body, true);
            AddLabel(outHeader, "Current Team", 120, ThemeFonts.Body, true);
            AddLabel(outHeader, "Target Role", 120, ThemeFonts.Body, true);
            AddLabel(outHeader, "Status", 80, ThemeFonts.Body, true);
            AddLabel(outHeader, "", 100, ThemeFonts.Body, false);
            vbox.AddChild(outHeader);
            vbox.AddChild(new HSeparator());

            foreach (var req in outgoing.OrderByDescending(r => r.RequestWeek))
            {
                var coach = gm.GetCoach(req.CoachId);
                var fromTeam = gm.GetTeam(req.CurrentTeamId);

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                AddLabel(row, coach?.FullName ?? "Unknown", 150, ThemeFonts.Small, false);
                AddLabel(row, fromTeam?.Abbreviation ?? "???", 120, ThemeFonts.Small, false);
                AddLabel(row, req.TargetRole.ToString(), 120, ThemeFonts.Small, false);

                Color statusColor = req.Status switch
                {
                    InterviewStatus.Approved => ThemeColors.Success,
                    InterviewStatus.Blocked => ThemeColors.Danger,
                    InterviewStatus.Hired => ThemeColors.AccentText,
                    InterviewStatus.Expired => ThemeColors.TextTertiary,
                    _ => ThemeColors.Warning,
                };
                string statusText = req.Status.ToString();
                if (req.Status == InterviewStatus.Blocked)
                    statusText += $" ({req.BlockReason})";

                var statusLabel = UIFactory.CreateLabel(statusText, ThemeFonts.Small, statusColor, 80);
                row.AddChild(statusLabel);

                // Hire button for approved interviews
                if (req.Status == InterviewStatus.Approved)
                {
                    var hireBtn = new Button
                    {
                        Text = "Hire",
                        CustomMinimumSize = new Vector2(100, 0),
                    };
                    string reqId = req.Id;
                    hireBtn.Pressed += () => OnHireFromInterview(reqId);
                    row.AddChild(hireBtn);
                }
                else
                {
                    AddLabel(row, "", 100, ThemeFonts.Small, false);
                }

                vbox.AddChild(row);
            }
        }

        vbox.AddChild(new HSeparator());

        // Section 2: League Activity Feed
        var feedTitle = UIFactory.CreateLabel("LEAGUE COACHING ACTIVITY", ThemeFonts.Subtitle, ThemeColors.AccentText);
        vbox.AddChild(feedTitle);

        // Show all hire/fire events from interview requests
        var allRequests = gm.Staff.InterviewRequests;
        var hiredRequests = allRequests
            .Where(r => r.Status == InterviewStatus.Hired && r.RequestingTeamId != gm.PlayerTeamId)
            .OrderByDescending(r => r.RequestWeek)
            .Take(15);

        bool hasActivity = false;
        foreach (var req in hiredRequests)
        {
            var coach = gm.GetCoach(req.CoachId);
            var hiringTeam = gm.GetTeam(req.RequestingTeamId);
            if (coach == null || hiringTeam == null) continue;

            hasActivity = true;
            var feedRow = UIFactory.CreateLabel(
                $"Week {req.RequestWeek}: {hiringTeam.FullName} hired {coach.FullName} as {req.TargetRole}",
                ThemeFonts.Small, ThemeColors.TextSecondary);
            vbox.AddChild(feedRow);
        }

        if (!hasActivity)
            vbox.AddChild(UIFactory.CreateEmptyState("No league coaching activity yet this window."));

        return scroll;
    }

    // =====================================================
    // SHARED UI BUILDERS
    // =====================================================

    private void BuildMarketCoachList(VBoxContainer parent, IReadOnlyList<Coach> market)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

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
        parent.AddChild(header);
        parent.AddChild(new HSeparator());

        var team = gm.GetPlayerTeam();

        foreach (var coach in market.OrderByDescending(c => c.Prestige * 0.3f + c.GameManagement * 0.3f
            + c.OffenseRating * 0.2f + c.DefenseRating * 0.2f))
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);

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
            parent.AddChild(hbox);
        }
    }

    private void AddCoachRow(VBoxContainer parent, string? coachId, string roleLabel, CoachRole role, bool showScheme)
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

                string keyRating = role switch
                {
                    CoachRole.HeadCoach => $"GM: {coach.GameManagement}",
                    CoachRole.OffensiveCoordinator => $"OFF: {coach.OffenseRating}",
                    CoachRole.DefensiveCoordinator => $"DEF: {coach.DefenseRating}",
                    CoachRole.SpecialTeamsCoordinator => $"ST: {coach.SpecialTeamsRating}",
                    _ => $"DEV: {coach.PlayerDevelopment}",
                };
                AddLabel(hbox, keyRating, 80, ThemeFonts.Small, false);

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

                AddLabel(hbox, coach.PlayerDevelopment.ToString(), 40, ThemeFonts.Small, false);

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

        parent.AddChild(hbox);
    }

    // =====================================================
    // HELPERS
    // =====================================================

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

    private void OnRequestInterview(string coachId, CoachRole targetRole)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, msg, req) = gm.Staff.RequestInterview(coachId, targetRole);
        GD.Print($"Request interview: {success} — {msg}");

        if (success && req != null)
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.NotificationCreated,
                "Interview Request",
                msg,
                req.Status == InterviewStatus.Blocked ? 3 : 1);
        }

        Refresh();
    }

    private void OnHireFromInterview(string requestId)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var (success, msg) = gm.Staff.HireFromInterview(requestId);
        GD.Print($"Hire from interview: {success} — {msg}");
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
    private void OnMarketOpened(int year) => Refresh();
    private void OnMarketClosed(int year) => Refresh();
    private void OnMarketWeekProcessed(int weekNumber) => Refresh();
    private void OnPlayerCoachTargeted(string coachId, string requestingTeamId, int targetRole)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var coach = gm.GetCoach(coachId);
        var reqTeam = gm.GetTeam(requestingTeamId);
        if (coach != null && reqTeam != null)
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.NotificationCreated,
                "Coach Targeted",
                $"{reqTeam.FullName} wants to interview {coach.FullName} for {(CoachRole)targetRole}!",
                2);
        }
        Refresh();
    }

    // --- Utility ---

    private static void AddLabel(HBoxContainer parent, string text, int minWidth, int fontSize, bool bold)
    {
        UIFactory.AddCell(parent, text, minWidth, fontSize);
    }
}
