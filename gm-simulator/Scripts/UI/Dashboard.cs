using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class Dashboard : Control
{
    // Layout containers
    private ScrollContainer _scroll = null!;
    private VBoxContainer _root = null!;

    // Section 1: Phase Banner
    private PanelContainer _phaseBanner = null!;
    private Label _phaseNameLabel = null!;
    private Label _phaseWeekLabel = null!;
    private Label _phaseDescLabel = null!;

    // Section 2: Season Timeline
    private HBoxContainer _timelineContainer = null!;

    // Section 3: Quick Actions
    private HBoxContainer _quickActionContainer = null!;

    // Section 4: Body columns
    private VBoxContainer _actionItemsList = null!;
    private VBoxContainer _teamSnapshotList = null!;
    private VBoxContainer _infoCardsList = null!;

    public override void _Ready()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced += OnWeekAdvanced;
            EventBus.Instance.PhaseChanged += OnPhaseChanged;
        }

        BuildLayout();
        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.WeekAdvanced -= OnWeekAdvanced;
            EventBus.Instance.PhaseChanged -= OnPhaseChanged;
        }
    }

    // ── Layout Construction ──────────────────────────────────────────

    private void BuildLayout()
    {
        _scroll = new ScrollContainer();
        _scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_scroll);

        var margin = new MarginContainer();
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left", 30);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 30);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        _scroll.AddChild(margin);

        _root = UIFactory.CreateSection(ThemeSpacing.SectionGap);
        _root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddChild(_root);

        BuildPhaseBanner();
        BuildSeasonTimeline();
        BuildQuickActions();
        BuildBodyColumns();
    }

    private void BuildPhaseBanner()
    {
        _phaseBanner = new PanelContainer();
        _root.AddChild(_phaseBanner);

        var bannerMargin = new MarginContainer();
        bannerMargin.AddThemeConstantOverride("margin_left", ThemeSpacing.CardPadding);
        bannerMargin.AddThemeConstantOverride("margin_top", ThemeSpacing.MD);
        bannerMargin.AddThemeConstantOverride("margin_right", ThemeSpacing.CardPadding);
        bannerMargin.AddThemeConstantOverride("margin_bottom", ThemeSpacing.MD);
        bannerMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _phaseBanner.AddChild(bannerMargin);

        var bannerVBox = UIFactory.CreateSection(4);
        bannerVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bannerMargin.AddChild(bannerVBox);

        var topRow = UIFactory.CreateRow(ThemeSpacing.SM);
        topRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bannerVBox.AddChild(topRow);

        _phaseNameLabel = UIFactory.CreateLabel("", ThemeFonts.Display, ThemeColors.TextPrimary,
            expandFill: true);
        topRow.AddChild(_phaseNameLabel);

        _phaseWeekLabel = UIFactory.CreateLabel("", ThemeFonts.Title, ThemeColors.TextSecondary,
            align: HorizontalAlignment.Right);
        topRow.AddChild(_phaseWeekLabel);

        _phaseDescLabel = UIFactory.CreateLabel("", ThemeFonts.BodyLarge, ThemeColors.TextSecondary);
        _phaseDescLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bannerVBox.AddChild(_phaseDescLabel);
    }

    private void BuildSeasonTimeline()
    {
        var timelineCard = UIFactory.CreateCard();
        timelineCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _root.AddChild(timelineCard);

        var timelineMargin = new MarginContainer();
        timelineMargin.AddThemeConstantOverride("margin_left", ThemeSpacing.SM);
        timelineMargin.AddThemeConstantOverride("margin_top", ThemeSpacing.XS);
        timelineMargin.AddThemeConstantOverride("margin_right", ThemeSpacing.SM);
        timelineMargin.AddThemeConstantOverride("margin_bottom", ThemeSpacing.XS);
        timelineMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        timelineCard.AddChild(timelineMargin);

        var timelineVBox = UIFactory.CreateSection(ThemeSpacing.XS);
        timelineVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        timelineMargin.AddChild(timelineVBox);

        timelineVBox.AddChild(UIFactory.CreateLabel("SEASON PROGRESS",
            ThemeFonts.Small, ThemeColors.TextTertiary));

        _timelineContainer = UIFactory.CreateRow(2);
        _timelineContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        timelineVBox.AddChild(_timelineContainer);
    }

    private void BuildQuickActions()
    {
        _quickActionContainer = UIFactory.CreateRow(ThemeSpacing.SM);
        _quickActionContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _root.AddChild(_quickActionContainer);
    }

    private void BuildBodyColumns()
    {
        var bodyColumns = UIFactory.CreateRow(ThemeSpacing.SectionGap);
        bodyColumns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _root.AddChild(bodyColumns);

        // Left column
        var leftColumn = UIFactory.CreateSection(ThemeSpacing.MD);
        leftColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftColumn.SizeFlagsStretchRatio = 1f;
        bodyColumns.AddChild(leftColumn);

        // Action items card
        var actionCard = UIFactory.CreateCard();
        actionCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftColumn.AddChild(actionCard);
        var actionMargin = CreateCardMargin();
        actionCard.AddChild(actionMargin);
        var actionVBox = UIFactory.CreateSection(ThemeSpacing.XS);
        actionVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionMargin.AddChild(actionVBox);
        actionVBox.AddChild(UIFactory.CreateLabel("THINGS TO DO",
            ThemeFonts.Subtitle, ThemeColors.AccentText));
        _actionItemsList = UIFactory.CreateSection(ThemeSpacing.XS);
        actionVBox.AddChild(_actionItemsList);

        // Team snapshot card
        var snapCard = UIFactory.CreateCard();
        snapCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftColumn.AddChild(snapCard);
        var snapMargin = CreateCardMargin();
        snapCard.AddChild(snapMargin);
        var snapVBox = UIFactory.CreateSection(ThemeSpacing.XS);
        snapVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        snapMargin.AddChild(snapVBox);
        snapVBox.AddChild(UIFactory.CreateLabel("TEAM SNAPSHOT",
            ThemeFonts.Subtitle, ThemeColors.AccentText));
        _teamSnapshotList = UIFactory.CreateSection(ThemeSpacing.XS);
        snapVBox.AddChild(_teamSnapshotList);

        // Right column
        var rightColumn = UIFactory.CreateSection(ThemeSpacing.MD);
        rightColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightColumn.SizeFlagsStretchRatio = 1f;
        bodyColumns.AddChild(rightColumn);

        _infoCardsList = UIFactory.CreateSection(ThemeSpacing.MD);
        _infoCardsList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightColumn.AddChild(_infoCardsList);
    }

    private static MarginContainer CreateCardMargin()
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", ThemeSpacing.CardPadding);
        m.AddThemeConstantOverride("margin_top", ThemeSpacing.SM);
        m.AddThemeConstantOverride("margin_right", ThemeSpacing.CardPadding);
        m.AddThemeConstantOverride("margin_bottom", ThemeSpacing.SM);
        m.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return m;
    }

    // ── Refresh Logic ────────────────────────────────────────────────

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsGameActive) return;

        RefreshPhaseBanner(gm);
        RefreshTimeline(gm);
        RefreshQuickActions(gm);
        RefreshActionItems(gm);
        RefreshTeamSnapshot(gm);
        RefreshInfoCards(gm);
    }

    private void OnWeekAdvanced(int year, int week) => Refresh();
    private void OnPhaseChanged(int phase) => Refresh();

    // ── Section 1: Phase Banner ──────────────────────────────────────

    private void RefreshPhaseBanner(GameManager gm)
    {
        var phase = gm.Calendar.CurrentPhase;
        Color accent = ThemeColors.GetPhaseAccentColor(phase);
        Color accentBg = ThemeColors.GetPhaseAccentBg(phase);

        var style = new StyleBoxFlat();
        style.BgColor = accentBg;
        style.BorderWidthLeft = 4;
        style.BorderColor = accent;
        style.SetCornerRadiusAll(ThemeSpacing.RadiusMD);
        _phaseBanner.AddThemeStyleboxOverride("panel", style);

        _phaseNameLabel.Text = gm.Calendar.GetPhaseDisplayName().ToUpper();
        _phaseNameLabel.AddThemeColorOverride("font_color", accent);

        _phaseWeekLabel.Text = $"Week {gm.Calendar.CurrentWeek} of {gm.Calendar.GetTotalWeeksInPhase()}";
        _phaseDescLabel.Text = gm.Calendar.GetPhaseDescription();
    }

    // ── Section 2: Season Timeline ───────────────────────────────────

    private void RefreshTimeline(GameManager gm)
    {
        foreach (var child in _timelineContainer.GetChildren())
            child.QueueFree();

        var phases = (GamePhase[])Enum.GetValues(typeof(GamePhase));
        int currentIdx = Array.IndexOf(phases, gm.Calendar.CurrentPhase);

        foreach (int i in Enumerable.Range(0, phases.Length))
        {
            var phase = phases[i];
            int duration = gm.Calendar.GetPhaseDuration(phase);

            var segment = new PanelContainer();
            segment.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            segment.SizeFlagsStretchRatio = duration;
            segment.CustomMinimumSize = new Vector2(0, 32);
            segment.TooltipText = $"{CalendarSystem.GetPhaseDisplayName(phase)} ({duration} wk)";

            var segStyle = new StyleBoxFlat();
            if (i < currentIdx)
                segStyle.BgColor = ThemeColors.GetPhaseAccentColor(phase).Darkened(0.6f);
            else if (i == currentIdx)
                segStyle.BgColor = ThemeColors.GetPhaseAccentColor(phase);
            else
                segStyle.BgColor = ThemeColors.BgOverlay;
            segStyle.SetCornerRadiusAll(3);
            segment.AddThemeStyleboxOverride("panel", segStyle);

            var abbrev = GetPhaseAbbreviation(phase);
            var label = UIFactory.CreateLabel(abbrev, ThemeFonts.Caption,
                i == currentIdx ? ThemeColors.TextPrimary : ThemeColors.TextTertiary,
                align: HorizontalAlignment.Center, expandFill: true);
            label.VerticalAlignment = VerticalAlignment.Center;
            segment.AddChild(label);

            _timelineContainer.AddChild(segment);
        }
    }

    private static string GetPhaseAbbreviation(GamePhase phase) => phase switch
    {
        GamePhase.PostSeason      => "POST",
        GamePhase.CombineScouting => "COMB",
        GamePhase.FreeAgency      => "FA",
        GamePhase.PreDraft        => "PRE",
        GamePhase.Draft           => "DFT",
        GamePhase.PostDraft       => "OTA",
        GamePhase.Preseason       => "PS",
        GamePhase.RegularSeason   => "REG",
        GamePhase.Playoffs        => "PLY",
        GamePhase.SuperBowl       => "SB",
        _                         => "?",
    };

    // ── Section 3: Quick Actions ─────────────────────────────────────

    private void RefreshQuickActions(GameManager gm)
    {
        foreach (var child in _quickActionContainer.GetChildren())
            child.QueueFree();

        var actions = GetPhaseQuickActions(gm.Calendar.CurrentPhase);
        Color accent = ThemeColors.GetPhaseAccentColor(gm.Calendar.CurrentPhase);

        foreach (var (text, screen) in actions)
        {
            var btn = new Button();
            btn.Text = text;
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            btn.CustomMinimumSize = new Vector2(0, 48);

            var normal = new StyleBoxFlat();
            normal.BgColor = ThemeColors.BgSurface;
            normal.SetBorderWidthAll(2);
            normal.BorderColor = accent;
            normal.SetCornerRadiusAll(ThemeSpacing.RadiusMD);
            normal.SetContentMarginAll(ThemeSpacing.SM);
            btn.AddThemeStyleboxOverride("normal", normal);

            var hover = new StyleBoxFlat();
            hover.BgColor = accent.Darkened(0.6f);
            hover.SetBorderWidthAll(2);
            hover.BorderColor = accent;
            hover.SetCornerRadiusAll(ThemeSpacing.RadiusMD);
            hover.SetContentMarginAll(ThemeSpacing.SM);
            btn.AddThemeStyleboxOverride("hover", hover);

            var pressed = new StyleBoxFlat();
            pressed.BgColor = accent.Darkened(0.4f);
            pressed.SetBorderWidthAll(2);
            pressed.BorderColor = accent;
            pressed.SetCornerRadiusAll(ThemeSpacing.RadiusMD);
            pressed.SetContentMarginAll(ThemeSpacing.SM);
            btn.AddThemeStyleboxOverride("pressed", pressed);

            btn.AddThemeColorOverride("font_color", ThemeColors.TextPrimary);
            btn.AddThemeColorOverride("font_hover_color", ThemeColors.TextPrimary);
            btn.AddThemeFontSizeOverride("font_size", ThemeFonts.BodyLarge);

            string screenCapture = screen;
            btn.Pressed += () => EventBus.Instance?.EmitSignal(
                EventBus.SignalName.NavigationRequested, screenCapture);

            _quickActionContainer.AddChild(btn);
        }
    }

    private static List<(string Label, string Screen)> GetPhaseQuickActions(GamePhase phase) => phase switch
    {
        GamePhase.PostSeason => new()
        {
            ("Coaching Staff", "Staff"),
            ("Team History", "History"),
            ("Standings", "Standings"),
        },
        GamePhase.CombineScouting => new()
        {
            ("Scouting Hub", "Scouting"),
            ("Draft Board", "DraftBoard"),
            ("Roster", "Roster"),
        },
        GamePhase.FreeAgency => new()
        {
            ("Free Agent Market", "FreeAgency"),
            ("Cap Space", "CapSpace"),
            ("Roster", "Roster"),
        },
        GamePhase.PreDraft => new()
        {
            ("Draft Board", "DraftBoard"),
            ("Trade Hub", "Trade"),
            ("Scouting", "Scouting"),
        },
        GamePhase.Draft => new()
        {
            ("Enter Draft Room", "DraftBoard"),
            ("Scouting", "Scouting"),
        },
        GamePhase.PostDraft => new()
        {
            ("Roster", "Roster"),
            ("Depth Chart", "DepthChart"),
            ("Cap Space", "CapSpace"),
        },
        GamePhase.Preseason => new()
        {
            ("Depth Chart", "DepthChart"),
            ("Schedule", "Schedule"),
            ("Roster", "Roster"),
        },
        GamePhase.RegularSeason => new()
        {
            ("Schedule", "Schedule"),
            ("Trade Hub", "Trade"),
            ("Standings", "Standings"),
            ("Depth Chart", "DepthChart"),
        },
        GamePhase.Playoffs => new()
        {
            ("Schedule", "Schedule"),
            ("Standings", "Standings"),
            ("Depth Chart", "DepthChart"),
        },
        GamePhase.SuperBowl => new()
        {
            ("Super Bowl Matchup", "Schedule"),
            ("Depth Chart", "DepthChart"),
        },
        _ => new() { ("Dashboard", "Dashboard") },
    };

    // ── Section 4 Left: Action Items ─────────────────────────────────

    private void RefreshActionItems(GameManager gm)
    {
        foreach (var child in _actionItemsList.GetChildren())
            child.QueueFree();

        var items = GatherActionItems(gm);
        Color accent = ThemeColors.GetPhaseAccentColor(gm.Calendar.CurrentPhase);

        foreach (var item in items)
        {
            var row = UIFactory.CreateRow(ThemeSpacing.XS);
            row.AddChild(UIFactory.CreateLabel("\u2022", ThemeFonts.Body, accent));
            var text = UIFactory.CreateLabel(item, ThemeFonts.Body, ThemeColors.TextSecondary,
                expandFill: true);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(text);
            _actionItemsList.AddChild(row);
        }
    }

    private List<string> GatherActionItems(GameManager gm)
    {
        var items = new List<string>();
        var team = gm.GetPlayerTeam();
        if (team == null) return items;

        switch (gm.Calendar.CurrentPhase)
        {
            case GamePhase.PostSeason:
                if (team.HeadCoachId == null)
                    items.Add("Your Head Coach position is VACANT — review coaching staff");
                else
                    items.Add("Review your coaching staff performance");
                items.Add("Check season award winners");
                items.Add("Begin planning your offseason strategy");
                break;

            case GamePhase.CombineScouting:
                if (gm.CurrentDraftClass.Count > 0)
                {
                    int scouted = gm.CurrentDraftClass.Count(p =>
                        p.ScoutGrade > ScoutingGrade.Unscouted);
                    items.Add($"Scouted {scouted}/{gm.CurrentDraftClass.Count} draft prospects");
                }
                items.Add($"Scouting points: {gm.Scouting.CurrentPoints} / {gm.Scouting.WeeklyPointPool} pts");
                items.Add("Scout prospects to reveal their attributes");
                break;

            case GamePhase.FreeAgency:
                items.Add($"{gm.FreeAgency.FreeAgentPool.Count} free agents available");
                items.Add($"Cap space: {GameShell.FormatCurrency(team.CapSpace)}");
                if (!team.FranchiseTagUsed)
                    items.Add("Franchise tag is available");
                items.Add("Consider extending your key players");
                break;

            case GamePhase.PreDraft:
                var preDraftPicks = team.DraftPicks
                    .Where(p => p.Year == gm.Calendar.CurrentYear && !p.IsUsed)
                    .OrderBy(p => p.Round).ToList();
                items.Add($"You have {preDraftPicks.Count} draft picks this year");
                if (preDraftPicks.Count > 0)
                    items.Add($"First pick: Round {preDraftPicks[0].Round}" +
                        (preDraftPicks[0].OverallNumber.HasValue
                            ? $" (#{preDraftPicks[0].OverallNumber})" : ""));
                items.Add("Finalize your draft board rankings");
                items.Add("Explore trade opportunities for draft capital");
                break;

            case GamePhase.Draft:
                var currentPick = gm.Draft.GetCurrentPick();
                if (currentPick != null && currentPick.CurrentTeamId == team.Id)
                    items.Add($"YOU ARE ON THE CLOCK! Round {currentPick.Round}, Pick {currentPick.OverallNumber ?? 0}");
                else if (currentPick != null)
                    items.Add($"Current pick: Round {currentPick.Round} — waiting...");
                var remaining = team.DraftPicks
                    .Where(p => p.Year == gm.Calendar.CurrentYear && !p.IsUsed).ToList();
                items.Add($"{remaining.Count} pick{(remaining.Count == 1 ? "" : "s")} remaining");
                break;

            case GamePhase.PostDraft:
                int rosterSize = team.PlayerIds.Count;
                if (rosterSize > 53)
                    items.Add($"Roster at {rosterSize}/53 — NEED TO CUT {rosterSize - 53} PLAYERS");
                else if (rosterSize < 53)
                    items.Add($"Roster at {rosterSize}/53 — {53 - rosterSize} spots available");
                else
                    items.Add("Roster is at 53/53");
                items.Add("Organize your depth chart for the season");
                items.Add("Review your rookie class");
                break;

            case GamePhase.Preseason:
                int preRoster = team.PlayerIds.Count;
                if (preRoster > 53)
                    items.Add($"Roster at {preRoster}/53 — CUT {preRoster - 53} before regular season!");
                else
                    items.Add($"Roster at {preRoster}/53");
                items.Add("Set your depth chart for the season");
                items.Add("Evaluate preseason matchups");
                break;

            case GamePhase.RegularSeason:
                var nextGame = gm.CurrentSeason?.Games
                    .Where(g => !g.IsCompleted && !g.IsPlayoff &&
                        (g.HomeTeamId == team.Id || g.AwayTeamId == team.Id))
                    .OrderBy(g => g.Week)
                    .FirstOrDefault();
                if (nextGame != null)
                {
                    var oppId = nextGame.HomeTeamId == team.Id ? nextGame.AwayTeamId : nextGame.HomeTeamId;
                    var opp = gm.GetTeam(oppId);
                    string homeAway = nextGame.HomeTeamId == team.Id ? "vs." : "@";
                    items.Add($"Next game: {homeAway} {opp?.Abbreviation ?? "???"} (Week {nextGame.Week})");
                }
                if (gm.Trading.IsTradeWindowOpen())
                {
                    int weeksLeft = 8 - gm.Calendar.CurrentWeek;
                    if (weeksLeft > 0)
                        items.Add($"Trade deadline in {weeksLeft} week{(weeksLeft == 1 ? "" : "s")}");
                    else if (weeksLeft == 0)
                        items.Add("TRADE DEADLINE IS THIS WEEK!");
                }
                else if (gm.Calendar.CurrentWeek > 8)
                    items.Add("Trade deadline has passed");
                var injured = gm.GetTeamActivePlayers(team.Id)
                    .Where(p => p.CurrentInjury != null).ToList();
                if (injured.Count > 0)
                    items.Add($"{injured.Count} player{(injured.Count == 1 ? "" : "s")} currently injured");
                break;

            case GamePhase.Playoffs:
                var playoffGame = gm.CurrentSeason?.Games
                    .Where(g => g.IsPlayoff && !g.IsCompleted &&
                        (g.HomeTeamId == team.Id || g.AwayTeamId == team.Id))
                    .FirstOrDefault();
                if (playoffGame != null)
                {
                    var oppId = playoffGame.HomeTeamId == team.Id ? playoffGame.AwayTeamId : playoffGame.HomeTeamId;
                    var opp = gm.GetTeam(oppId);
                    items.Add($"Playoff matchup: vs. {opp?.FullName ?? "TBD"}");
                }
                else
                    items.Add("Your season has ended — watch the playoffs unfold");
                items.Add("Win or go home!");
                break;

            case GamePhase.SuperBowl:
                var sbGame = gm.CurrentSeason?.Games
                    .Where(g => g.IsPlayoff && !g.IsCompleted)
                    .FirstOrDefault();
                if (sbGame != null)
                {
                    bool inSB = sbGame.HomeTeamId == team.Id || sbGame.AwayTeamId == team.Id;
                    if (inSB)
                    {
                        var oppId = sbGame.HomeTeamId == team.Id ? sbGame.AwayTeamId : sbGame.HomeTeamId;
                        var opp = gm.GetTeam(oppId);
                        items.Add($"SUPER BOWL: vs. {opp?.FullName ?? "TBD"}");
                    }
                    else
                        items.Add("Watch the Super Bowl unfold");
                }
                break;
        }

        return items;
    }

    // ── Section 4 Left: Team Snapshot ─────────────────────────────────

    private void RefreshTeamSnapshot(GameManager gm)
    {
        foreach (var child in _teamSnapshotList.GetChildren())
            child.QueueFree();

        var team = gm.GetPlayerTeam();
        if (team == null) return;

        AddSnapshotRow(_teamSnapshotList, "Record",
            $"{team.CurrentRecord.Wins}-{team.CurrentRecord.Losses}-{team.CurrentRecord.Ties}");
        AddSnapshotRow(_teamSnapshotList, "Year", gm.Calendar.CurrentYear.ToString());
        AddSnapshotRow(_teamSnapshotList, "Cap Space", GameShell.FormatCurrency(team.CapSpace));
        AddSnapshotRow(_teamSnapshotList, "Roster",
            $"{team.PlayerIds.Count}/53 Active, {team.PracticeSquadIds.Count}/16 PS");

        var headCoach = team.HeadCoachId != null ? gm.GetCoach(team.HeadCoachId) : null;
        AddSnapshotRow(_teamSnapshotList, "Head Coach", headCoach?.FullName ?? "Vacant");
        AddSnapshotRow(_teamSnapshotList, "Fan Satisfaction", $"{team.FanSatisfaction}%");
        AddSnapshotRow(_teamSnapshotList, "Owner Patience", $"{team.OwnerPatience}%");
    }

    private static void AddSnapshotRow(VBoxContainer parent, string label, string value)
    {
        var row = UIFactory.CreateRow(ThemeSpacing.SM);
        row.AddChild(UIFactory.CreateLabel(label, ThemeFonts.Body,
            ThemeColors.TextTertiary, minWidth: 140));
        row.AddChild(UIFactory.CreateLabel(value, ThemeFonts.Body, ThemeColors.TextPrimary));
        parent.AddChild(row);
    }

    // ── Section 4 Right: Phase-Specific Info Cards ───────────────────

    private void RefreshInfoCards(GameManager gm)
    {
        foreach (var child in _infoCardsList.GetChildren())
            child.QueueFree();

        switch (gm.Calendar.CurrentPhase)
        {
            case GamePhase.PostSeason:
                BuildPostSeasonInfoCards(gm);
                break;
            case GamePhase.CombineScouting:
            case GamePhase.PreDraft:
                BuildScoutingInfoCards(gm);
                break;
            case GamePhase.FreeAgency:
                BuildFreeAgencyInfoCards(gm);
                break;
            case GamePhase.Draft:
                BuildDraftInfoCards(gm);
                break;
            case GamePhase.PostDraft:
                BuildPostDraftInfoCards(gm);
                break;
            case GamePhase.Preseason:
            case GamePhase.RegularSeason:
                BuildSeasonInfoCards(gm);
                break;
            case GamePhase.Playoffs:
            case GamePhase.SuperBowl:
                BuildPlayoffInfoCards(gm);
                break;
        }
    }

    private void BuildSeasonInfoCards(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Next matchup
        var nextGame = gm.CurrentSeason?.Games
            .Where(g => !g.IsCompleted && !g.IsPlayoff &&
                (g.HomeTeamId == team.Id || g.AwayTeamId == team.Id))
            .OrderBy(g => g.Week)
            .FirstOrDefault();

        if (nextGame != null)
        {
            var card = CreateInfoCard("NEXT GAME");
            var vbox = GetInfoCardContent(card);

            var oppId = nextGame.HomeTeamId == team.Id ? nextGame.AwayTeamId : nextGame.HomeTeamId;
            var opp = gm.GetTeam(oppId);
            string homeAway = nextGame.HomeTeamId == team.Id ? "HOME vs." : "AWAY @";

            vbox.AddChild(UIFactory.CreateLabel(
                $"{homeAway} {opp?.FullName ?? "???"}", ThemeFonts.Title, ThemeColors.TextPrimary));
            vbox.AddChild(UIFactory.CreateLabel(
                $"Week {nextGame.Week} | Record: {opp?.CurrentRecord.Wins}-{opp?.CurrentRecord.Losses}-{opp?.CurrentRecord.Ties}",
                ThemeFonts.Body, ThemeColors.TextSecondary));

            _infoCardsList.AddChild(card);
        }

        // Recent results
        var recentGames = gm.CurrentSeason?.Games
            .Where(g => g.IsCompleted && !g.IsPlayoff &&
                (g.HomeTeamId == team.Id || g.AwayTeamId == team.Id))
            .OrderByDescending(g => g.Week)
            .Take(5)
            .ToList();

        if (recentGames != null && recentGames.Count > 0)
        {
            var card = CreateInfoCard("RECENT RESULTS");
            var vbox = GetInfoCardContent(card);

            foreach (var game in recentGames)
            {
                bool isHome = game.HomeTeamId == team.Id;
                int myScore = isHome ? game.HomeScore : game.AwayScore;
                int oppScore = isHome ? game.AwayScore : game.HomeScore;
                var oppTeam = gm.GetTeam(isHome ? game.AwayTeamId : game.HomeTeamId);
                string result = myScore > oppScore ? "W" : myScore < oppScore ? "L" : "T";
                Color resultColor = myScore > oppScore ? ThemeColors.Success :
                    myScore < oppScore ? ThemeColors.Danger : ThemeColors.Warning;

                var row = UIFactory.CreateRow(ThemeSpacing.XS);
                row.AddChild(UIFactory.CreateLabel(result, ThemeFonts.Body, resultColor, minWidth: 20));
                row.AddChild(UIFactory.CreateLabel(
                    $"{myScore}-{oppScore} {(isHome ? "vs" : "@")} {oppTeam?.Abbreviation ?? "???"} (Wk {game.Week})",
                    ThemeFonts.Body, ThemeColors.TextSecondary));
                vbox.AddChild(row);
            }

            _infoCardsList.AddChild(card);
        }

        // Division standings
        BuildDivisionStandingsSnippet(gm, team);
    }

    private void BuildFreeAgencyInfoCards(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Top available FAs
        var topFAs = gm.FreeAgency.FreeAgentPool
            .Select(id => gm.GetPlayer(id))
            .Where(p => p != null)
            .OrderByDescending(p => p!.Overall)
            .Take(8)
            .ToList();

        if (topFAs.Count > 0)
        {
            var card = CreateInfoCard("TOP FREE AGENTS");
            var vbox = GetInfoCardContent(card);

            foreach (var p in topFAs)
            {
                if (p == null) continue;
                var row = UIFactory.CreateRow(ThemeSpacing.XS);
                row.AddChild(UIFactory.CreateLabel(p.Position.ToString(), ThemeFonts.Body,
                    ThemeColors.TextTertiary, minWidth: 40));
                row.AddChild(UIFactory.CreateLabel(p.FullName, ThemeFonts.Body,
                    ThemeColors.TextPrimary, expandFill: true));
                row.AddChild(UIFactory.CreateLabel($"{p.Overall}", ThemeFonts.Body,
                    ThemeColors.GetRatingColor(p.Overall)));
                row.AddChild(UIFactory.CreateLabel($"Age {p.Age}", ThemeFonts.Body,
                    ThemeColors.TextTertiary));
                vbox.AddChild(row);
            }

            _infoCardsList.AddChild(card);
        }

        // Recent signings
        var recentSignings = gm.TransactionLog
            .Where(t => t.Type == TransactionType.Signed &&
                t.Phase == GamePhase.FreeAgency &&
                t.Year == gm.Calendar.CurrentYear)
            .OrderByDescending(t => t.Week)
            .Take(5)
            .ToList();

        if (recentSignings.Count > 0)
        {
            var card = CreateInfoCard("RECENT SIGNINGS");
            var vbox = GetInfoCardContent(card);

            foreach (var txn in recentSignings)
            {
                var lbl = UIFactory.CreateLabel(txn.Description, ThemeFonts.Small,
                    ThemeColors.TextSecondary);
                lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                vbox.AddChild(lbl);
            }

            _infoCardsList.AddChild(card);
        }
    }

    private void BuildScoutingInfoCards(GameManager gm)
    {
        // Top scouted prospects
        var topProspects = gm.CurrentDraftClass
            .Where(p => p.ScoutGrade >= ScoutingGrade.Initial)
            .OrderByDescending(p => p.DraftValue)
            .Take(8)
            .ToList();

        if (topProspects.Count > 0)
        {
            var card = CreateInfoCard("TOP SCOUTED PROSPECTS");
            var vbox = GetInfoCardContent(card);

            foreach (var p in topProspects)
            {
                var row = UIFactory.CreateRow(ThemeSpacing.XS);
                row.AddChild(UIFactory.CreateLabel(p.Position.ToString(), ThemeFonts.Body,
                    ThemeColors.TextTertiary, minWidth: 40));
                row.AddChild(UIFactory.CreateLabel(p.FullName, ThemeFonts.Body,
                    ThemeColors.TextPrimary, expandFill: true));
                row.AddChild(UIFactory.CreateLabel(p.ScoutGrade.ToString(), ThemeFonts.Body,
                    ThemeColors.GetScoutGradeColor(p.ScoutGrade)));
                row.AddChild(UIFactory.CreateLabel($"Rd {p.ProjectedRound}", ThemeFonts.Body,
                    ThemeColors.TextTertiary));
                vbox.AddChild(row);
            }

            _infoCardsList.AddChild(card);
        }

        // Your draft picks
        BuildDraftPicksCard(gm);
    }

    private void BuildDraftInfoCards(GameManager gm)
    {
        // Your upcoming picks
        BuildDraftPicksCard(gm);

        // Top remaining prospects
        var remaining = gm.CurrentDraftClass
            .Where(p => !p.IsDrafted)
            .OrderByDescending(p => p.DraftValue)
            .Take(8)
            .ToList();

        if (remaining.Count > 0)
        {
            var card = CreateInfoCard("TOP PROSPECTS ON BOARD");
            var vbox = GetInfoCardContent(card);

            foreach (var p in remaining)
            {
                var row = UIFactory.CreateRow(ThemeSpacing.XS);
                row.AddChild(UIFactory.CreateLabel(p.Position.ToString(), ThemeFonts.Body,
                    ThemeColors.TextTertiary, minWidth: 40));
                row.AddChild(UIFactory.CreateLabel(p.FullName, ThemeFonts.Body,
                    ThemeColors.TextPrimary, expandFill: true));
                row.AddChild(UIFactory.CreateLabel(p.College, ThemeFonts.Small,
                    ThemeColors.TextTertiary));
                vbox.AddChild(row);
            }

            _infoCardsList.AddChild(card);
        }
    }

    private void BuildPostSeasonInfoCards(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Season recap from history
        if (team.SeasonHistory.Count > 0)
        {
            var last = team.SeasonHistory[^1];
            var card = CreateInfoCard("LAST SEASON RECAP");
            var vbox = GetInfoCardContent(card);

            AddSnapshotRow(vbox, "Final Record", $"{last.Wins}-{last.Losses}-{last.Ties}");
            AddSnapshotRow(vbox, "Points For", last.PointsFor.ToString());
            AddSnapshotRow(vbox, "Points Against", last.PointsAgainst.ToString());
            AddSnapshotRow(vbox, "Division Rank", $"#{last.DivisionRank}");
            AddSnapshotRow(vbox, "Made Playoffs", last.MadePlayoffs ? "Yes" : "No");
            if (last.PlayoffResult != null)
                AddSnapshotRow(vbox, "Playoff Result", last.PlayoffResult);

            _infoCardsList.AddChild(card);
        }

        // Awards
        if (gm.AllAwards.Count > 0)
        {
            var latest = gm.AllAwards[^1];
            var card = CreateInfoCard($"{latest.Year} AWARDS");
            var vbox = GetInfoCardContent(card);

            if (latest.MvpId != null)
                AddSnapshotRow(vbox, "MVP", gm.GetPlayer(latest.MvpId)?.FullName ?? "Unknown");
            if (latest.DpoyId != null)
                AddSnapshotRow(vbox, "DPOY", gm.GetPlayer(latest.DpoyId)?.FullName ?? "Unknown");
            if (latest.OroyId != null)
                AddSnapshotRow(vbox, "OROY", gm.GetPlayer(latest.OroyId)?.FullName ?? "Unknown");
            if (latest.DroyId != null)
                AddSnapshotRow(vbox, "DROY", gm.GetPlayer(latest.DroyId)?.FullName ?? "Unknown");

            _infoCardsList.AddChild(card);
        }
    }

    private void BuildPostDraftInfoCards(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Rookie class
        var rookies = gm.GetTeamActivePlayers(team.Id)
            .Where(p => p.DraftYear == gm.Calendar.CurrentYear || p.YearsInLeague == 0)
            .OrderByDescending(p => p.Overall)
            .ToList();

        if (rookies.Count > 0)
        {
            var card = CreateInfoCard("YOUR ROOKIE CLASS");
            var vbox = GetInfoCardContent(card);

            foreach (var p in rookies)
            {
                var row = UIFactory.CreateRow(ThemeSpacing.XS);
                row.AddChild(UIFactory.CreateLabel(p.Position.ToString(), ThemeFonts.Body,
                    ThemeColors.TextTertiary, minWidth: 40));
                row.AddChild(UIFactory.CreateLabel(p.FullName, ThemeFonts.Body,
                    ThemeColors.TextPrimary, expandFill: true));
                row.AddChild(UIFactory.CreateLabel($"{p.Overall}", ThemeFonts.Body,
                    ThemeColors.GetRatingColor(p.Overall)));
                string draftInfo = p.IsUndrafted ? "UDFA" : $"Rd {p.DraftRound} Pk {p.DraftPick}";
                row.AddChild(UIFactory.CreateLabel(draftInfo, ThemeFonts.Small,
                    ThemeColors.TextTertiary));
                vbox.AddChild(row);
            }

            _infoCardsList.AddChild(card);
        }

        // Team needs
        if (team.TeamNeeds.Count > 0)
        {
            var card = CreateInfoCard("TEAM NEEDS");
            var vbox = GetInfoCardContent(card);

            foreach (var need in team.TeamNeeds.Take(5))
            {
                vbox.AddChild(UIFactory.CreateLabel($"\u2022 {need}",
                    ThemeFonts.Body, ThemeColors.Warning));
            }

            _infoCardsList.AddChild(card);
        }
    }

    private void BuildPlayoffInfoCards(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        // Playoff matchup
        var game = gm.CurrentSeason?.Games
            .Where(g => g.IsPlayoff && !g.IsCompleted &&
                (g.HomeTeamId == team.Id || g.AwayTeamId == team.Id))
            .FirstOrDefault();

        if (game != null)
        {
            string title = gm.Calendar.CurrentPhase == GamePhase.SuperBowl ? "SUPER BOWL" : "PLAYOFF MATCHUP";
            var card = CreateInfoCard(title);
            var vbox = GetInfoCardContent(card);

            var oppId = game.HomeTeamId == team.Id ? game.AwayTeamId : game.HomeTeamId;
            var opp = gm.GetTeam(oppId);

            vbox.AddChild(UIFactory.CreateLabel(
                $"vs. {opp?.FullName ?? "TBD"}", ThemeFonts.Title, ThemeColors.TextPrimary));
            vbox.AddChild(UIFactory.CreateLabel(
                $"Record: {opp?.CurrentRecord.Wins}-{opp?.CurrentRecord.Losses}-{opp?.CurrentRecord.Ties}",
                ThemeFonts.Body, ThemeColors.TextSecondary));

            _infoCardsList.AddChild(card);
        }

        // Division standings
        BuildDivisionStandingsSnippet(gm, team);
    }

    // ── Shared Info Card Helpers ──────────────────────────────────────

    private void BuildDraftPicksCard(GameManager gm)
    {
        var team = gm.GetPlayerTeam();
        if (team == null) return;

        var myPicks = team.DraftPicks
            .Where(p => p.Year == gm.Calendar.CurrentYear && !p.IsUsed)
            .OrderBy(p => p.Round)
            .ThenBy(p => p.OverallNumber)
            .ToList();

        if (myPicks.Count == 0) return;

        var card = CreateInfoCard("YOUR DRAFT PICKS");
        var vbox = GetInfoCardContent(card);

        foreach (var pick in myPicks)
        {
            string from = pick.OriginalTeamId != team.Id
                ? $" (from {gm.GetTeam(pick.OriginalTeamId)?.Abbreviation ?? "???"})"
                : "";
            vbox.AddChild(UIFactory.CreateLabel(
                $"Round {pick.Round}{(pick.OverallNumber.HasValue ? $" (#{pick.OverallNumber})" : "")}{from}",
                ThemeFonts.Body, ThemeColors.TextPrimary));
        }

        _infoCardsList.AddChild(card);
    }

    private void BuildDivisionStandingsSnippet(GameManager gm, Team team)
    {
        var card = CreateInfoCard($"{team.Conference} {team.Division} STANDINGS");
        var vbox = GetInfoCardContent(card);

        var divTeams = gm.Teams
            .Where(t => t.Conference == team.Conference && t.Division == team.Division)
            .OrderByDescending(t => t.CurrentRecord.Wins)
            .ThenBy(t => t.CurrentRecord.Losses)
            .ToList();

        foreach (var t in divTeams)
        {
            bool isPlayer = t.Id == team.Id;
            var row = UIFactory.CreateRow(ThemeSpacing.SM);
            row.AddChild(UIFactory.CreateLabel(t.Abbreviation, ThemeFonts.Body,
                isPlayer ? ThemeColors.AccentText : ThemeColors.TextPrimary, minWidth: 50));
            row.AddChild(UIFactory.CreateLabel(
                $"{t.CurrentRecord.Wins}-{t.CurrentRecord.Losses}-{t.CurrentRecord.Ties}",
                ThemeFonts.Body, ThemeColors.TextSecondary));

            if (isPlayer)
            {
                var highlight = new PanelContainer();
                highlight.AddThemeStyleboxOverride("panel", ThemeStyles.HighlightRow());
                highlight.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                highlight.AddChild(row);
                vbox.AddChild(highlight);
            }
            else
            {
                vbox.AddChild(row);
            }
        }

        _infoCardsList.AddChild(card);
    }

    /// <summary>Creates a styled info card with a title label and returns the card PanelContainer.</summary>
    private static PanelContainer CreateInfoCard(string title)
    {
        var card = UIFactory.CreateCard();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var margin = CreateCardMargin();
        card.AddChild(margin);

        var vbox = UIFactory.CreateSection(ThemeSpacing.XS);
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddChild(vbox);

        vbox.AddChild(UIFactory.CreateLabel(title, ThemeFonts.Subtitle, ThemeColors.AccentText));
        return card;
    }

    /// <summary>Gets the VBoxContainer content area inside a card created by CreateInfoCard.</summary>
    private static VBoxContainer GetInfoCardContent(PanelContainer card)
    {
        // Card → MarginContainer → VBoxContainer
        var margin = card.GetChild<MarginContainer>(0);
        return margin.GetChild<VBoxContainer>(0);
    }
}
