using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;
using GMSimulator.UI.Components;

namespace GMSimulator.UI;

public partial class PlayerCard : Window
{
    private string _playerId = string.Empty;

    private Label _nameLabel = null!;
    private Label _infoLabel = null!;
    private Label _teamCollegeLabel = null!;
    private Label _draftLabel = null!;
    private Label _ovrValue = null!;
    private Label _devValue = null!;
    private Label _moraleValue = null!;
    private Label _contractSummary = null!;
    private Label _contractCapDead = null!;
    private VBoxContainer _contractYearsList = null!;
    private VBoxContainer _attributesList = null!;
    private VBoxContainer _traitsList = null!;
    private Button _cutButton = null!;
    private Button _irButton = null!;
    private Button _psButton = null!;
    private Button _promoteButton = null!;

    public void Initialize(string playerId)
    {
        _playerId = playerId;
    }

    public override void _Ready()
    {
        var vbox = "PanelContainer/ScrollContainer/MarginContainer/VBox";
        _nameLabel = GetNode<Label>($"{vbox}/NameLabel");
        _infoLabel = GetNode<Label>($"{vbox}/InfoLabel");
        _teamCollegeLabel = GetNode<Label>($"{vbox}/TeamCollegeLabel");
        _draftLabel = GetNode<Label>($"{vbox}/DraftLabel");
        _ovrValue = GetNode<Label>($"{vbox}/SummaryHBox/OverallVBox/OvrValue");
        _devValue = GetNode<Label>($"{vbox}/SummaryHBox/DevVBox/DevValue");
        _moraleValue = GetNode<Label>($"{vbox}/SummaryHBox/MoraleVBox/MoraleValue");
        _contractSummary = GetNode<Label>($"{vbox}/ContractSummary");
        _contractCapDead = GetNode<Label>($"{vbox}/ContractCapDead");
        _contractYearsList = GetNode<VBoxContainer>($"{vbox}/ContractYearsList");
        _attributesList = GetNode<VBoxContainer>($"{vbox}/AttributesList");
        _traitsList = GetNode<VBoxContainer>($"{vbox}/TraitsList");
        _cutButton = GetNode<Button>($"{vbox}/ActionHBox/CutButton");
        _irButton = GetNode<Button>($"{vbox}/ActionHBox/IRButton");
        _psButton = GetNode<Button>($"{vbox}/ActionHBox/PSButton");
        _promoteButton = GetNode<Button>($"{vbox}/ActionHBox/PromoteButton");

        CloseRequested += OnClosePressed;

        Refresh();
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var player = gm.GetPlayer(_playerId);
        if (player == null) return;

        int currentYear = gm.Calendar.CurrentYear;

        // Identity
        _nameLabel.Text = player.FullName.ToUpper();
        _infoLabel.Text = $"{player.Position} | {player.Archetype} | Age {player.Age}";

        var team = player.TeamId != null ? gm.GetTeam(player.TeamId) : null;
        string teamName = team?.FullName ?? "Free Agent";
        _teamCollegeLabel.Text = $"{teamName} | {GameShell.FormatHeight(player.HeightInches)} {player.WeightLbs} lbs | {player.College}";

        if (player.IsUndrafted)
            _draftLabel.Text = "Undrafted Free Agent";
        else
            _draftLabel.Text = $"Draft: {player.DraftYear} Rd {player.DraftRound}, Pick {player.DraftPick}";

        // Summary
        _ovrValue.Text = player.Overall.ToString();
        _ovrValue.Modulate = OverallBadge.GetOverallColor(player.Overall);
        _devValue.Text = player.DevTrait.ToString();
        _moraleValue.Text = player.Morale.ToString();

        // Contract
        PopulateContract(player, currentYear);

        // Attributes
        PopulateAttributes(player);

        // Traits
        PopulateTraits(player);

        // Action buttons visibility based on roster status
        bool isPlayerTeam = player.TeamId == gm.PlayerTeamId;
        _cutButton.Visible = isPlayerTeam && player.RosterStatus != RosterStatus.FreeAgent;
        _irButton.Visible = isPlayerTeam && player.RosterStatus == RosterStatus.Active53;
        _psButton.Visible = isPlayerTeam && player.RosterStatus == RosterStatus.Active53;
        _promoteButton.Visible = isPlayerTeam && player.RosterStatus == RosterStatus.PracticeSquad;

        Title = $"Player Card - {player.FullName}";
    }

    private void PopulateContract(Player player, int currentYear)
    {
        foreach (var child in _contractYearsList.GetChildren())
            child.QueueFree();

        if (player.CurrentContract == null)
        {
            _contractSummary.Text = "No contract";
            _contractCapDead.Text = "";
            return;
        }

        var contract = player.CurrentContract;
        _contractSummary.Text = $"{contract.TotalYears}yr / {GameShell.FormatCurrency(contract.TotalValue)} / {GameShell.FormatCurrency(contract.TotalGuaranteed)} GTD";

        long capHit = contract.GetCapHit(currentYear);
        long deadCap = contract.CalculateDeadCap(currentYear);
        _contractCapDead.Text = $"Cap Hit: {GameShell.FormatCurrency(capHit)} | Dead Cap: {GameShell.FormatCurrency(deadCap)}";

        // Year-by-year breakdown
        foreach (var year in contract.Years)
        {
            string marker = year.Year == currentYear ? " (current)" : "";
            var label = new Label
            {
                Text = $"  Yr {year.YearNumber} ({year.Year}): {GameShell.FormatCurrency(year.CapHit)}{marker}",
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            if (year.Year == currentYear)
                label.Modulate = new Color(0.4f, 0.7f, 1.0f);
            _contractYearsList.AddChild(label);
        }
    }

    private void PopulateAttributes(Player player)
    {
        foreach (var child in _attributesList.GetChildren())
            child.QueueFree();

        var attrs = player.Attributes;
        // Show all attributes, grouped by category
        var attrList = new (string Name, int Value)[]
        {
            // Physical
            ("Speed", attrs.Speed), ("Acceleration", attrs.Acceleration),
            ("Agility", attrs.Agility), ("Strength", attrs.Strength),
            ("Jumping", attrs.Jumping), ("Stamina", attrs.Stamina),
            ("Toughness", attrs.Toughness), ("Injury Resist.", attrs.InjuryResistance),
            // Passing
            ("Throw Power", attrs.ThrowPower), ("Short Accuracy", attrs.ShortAccuracy),
            ("Medium Accuracy", attrs.MediumAccuracy), ("Deep Accuracy", attrs.DeepAccuracy),
            ("Throw on Run", attrs.ThrowOnRun), ("Play Action", attrs.PlayAction),
            // Rushing
            ("Carrying", attrs.Carrying), ("Ball Carrier Vision", attrs.BallCarrierVision),
            ("Break Tackle", attrs.BreakTackle), ("Trucking", attrs.Trucking),
            ("Elusiveness", attrs.Elusiveness), ("Spin Move", attrs.SpinMove),
            ("Juke Move", attrs.JukeMove), ("Stiff Arm", attrs.StiffArm),
            // Receiving
            ("Catching", attrs.Catching), ("Catch in Traffic", attrs.CatchInTraffic),
            ("Spec. Catch", attrs.SpectacularCatch), ("Route Running", attrs.RouteRunning),
            ("Release", attrs.Release),
            // Blocking
            ("Run Block", attrs.RunBlock), ("Pass Block", attrs.PassBlock),
            ("Impact Block", attrs.ImpactBlock), ("Lead Block", attrs.LeadBlock),
            // Defense
            ("Tackle", attrs.Tackle), ("Hit Power", attrs.HitPower),
            ("Power Moves", attrs.PowerMoves), ("Finesse Moves", attrs.FinesseMoves),
            ("Block Shedding", attrs.BlockShedding), ("Pursuit", attrs.Pursuit),
            ("Play Recognition", attrs.PlayRecognition), ("Man Coverage", attrs.ManCoverage),
            ("Zone Coverage", attrs.ZoneCoverage), ("Press", attrs.Press),
            // Special Teams
            ("Kick Power", attrs.KickPower), ("Kick Accuracy", attrs.KickAccuracy),
            // Mental
            ("Awareness", attrs.Awareness), ("Clutch", attrs.Clutch),
            ("Consistency", attrs.Consistency), ("Leadership", attrs.Leadership),
        };

        // Filter to show only relevant attributes (value > 15) to reduce noise
        foreach (var (name, value) in attrList)
        {
            if (value <= 15) continue; // Skip irrelevant attributes (e.g., KickPower for a QB)
            _attributesList.AddChild(AttributeBar.Create(name, value));
        }
    }

    private void PopulateTraits(Player player)
    {
        foreach (var child in _traitsList.GetChildren())
            child.QueueFree();

        var traits = player.Traits;
        var traitEntries = new (string Name, bool Active)[]
        {
            ("Fight for Yards", traits.FightForYards),
            ("High Motor", traits.HighMotor),
            ("Clutch", traits.Clutch),
            ("Big Game Player", traits.BigGamePlayer),
            ("Team Player", traits.TeamPlayer),
            ("Iron Man", traits.IronMan),
            ("Penalty Prone", traits.PenaltyProne),
            ("Locker Room Cancer", traits.LockerRoomCancer),
            ("Glass Body", traits.GlassBody),
        };

        foreach (var (name, active) in traitEntries)
        {
            if (!active) continue;
            var label = new Label
            {
                Text = $"  {name}",
            };
            label.AddThemeFontSizeOverride("font_size", 13);

            // Red for negative traits
            if (name is "Penalty Prone" or "Locker Room Cancer" or "Glass Body")
                label.Modulate = new Color(1.0f, 0.3f, 0.3f);
            else
                label.Modulate = new Color(0.3f, 0.9f, 0.3f);

            _traitsList.AddChild(label);
        }

        // Enum traits
        AddTraitLabel($"Pressure: {traits.SenseOfPressure}");
        AddTraitLabel($"Passes: {traits.ForcePasses}");
        AddTraitLabel($"Ball Security: {traits.CoversBall}");
    }

    private void AddTraitLabel(string text)
    {
        var label = new Label { Text = $"  {text}" };
        label.AddThemeFontSizeOverride("font_size", 13);
        _traitsList.AddChild(label);
    }

    // --- Action Button Handlers ---

    private void OnCutPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var player = gm.GetPlayer(_playerId);
        if (player == null || player.TeamId == null) return;

        var (success, message) = gm.RosterManager.CutPlayer(_playerId, player.TeamId);
        GD.Print($"Cut: {message}");
        if (success)
            OnClosePressed();
    }

    private void OnIRPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var player = gm.GetPlayer(_playerId);
        if (player == null || player.TeamId == null) return;

        var (success, message) = gm.RosterManager.MoveToIR(_playerId, player.TeamId);
        GD.Print($"IR: {message}");
        if (success)
            Refresh();
    }

    private void OnPSPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var player = gm.GetPlayer(_playerId);
        if (player == null || player.TeamId == null) return;

        var (success, message) = gm.RosterManager.MoveToPracticeSquad(_playerId, player.TeamId);
        GD.Print($"PS: {message}");
        if (success)
            Refresh();
    }

    private void OnPromotePressed()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var player = gm.GetPlayer(_playerId);
        if (player == null || player.TeamId == null) return;

        var (success, message) = gm.RosterManager.PromoteFromPracticeSquad(_playerId, player.TeamId);
        GD.Print($"Promote: {message}");
        if (success)
            Refresh();
    }

    private void OnClosePressed()
    {
        QueueFree();
    }
}
