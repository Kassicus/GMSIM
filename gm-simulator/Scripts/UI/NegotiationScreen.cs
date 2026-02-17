using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class NegotiationScreen : Window
{
    private Label _playerInfoLabel = null!;
    private Label _marketValueLabel = null!;
    private SpinBox _yearsSpinBox = null!;
    private SpinBox _totalSpinBox = null!;
    private SpinBox _guaranteedSpinBox = null!;
    private SpinBox _bonusSpinBox = null!;
    private Label _summaryLabel = null!;
    private Label _capSpaceLabel = null!;
    private Label _statusLabel = null!;

    private string _playerId = "";
    private Player? _player;

    public void Initialize(string playerId)
    {
        _playerId = playerId;
    }

    public override void _Ready()
    {
        _playerInfoLabel = GetNode<Label>("MarginContainer/VBox/PlayerInfoLabel");
        _marketValueLabel = GetNode<Label>("MarginContainer/VBox/MarketValueLabel");
        _yearsSpinBox = GetNode<SpinBox>("MarginContainer/VBox/YearsHBox/YearsSpinBox");
        _totalSpinBox = GetNode<SpinBox>("MarginContainer/VBox/TotalHBox/TotalSpinBox");
        _guaranteedSpinBox = GetNode<SpinBox>("MarginContainer/VBox/GuaranteedHBox/GuaranteedSpinBox");
        _bonusSpinBox = GetNode<SpinBox>("MarginContainer/VBox/BonusHBox/BonusSpinBox");
        _summaryLabel = GetNode<Label>("MarginContainer/VBox/SummaryLabel");
        _capSpaceLabel = GetNode<Label>("MarginContainer/VBox/CapSpaceLabel");
        _statusLabel = GetNode<Label>("MarginContainer/VBox/StatusLabel");

        PopulatePlayerInfo();
        UpdateSummary();
    }

    private void PopulatePlayerInfo()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        _player = gm.GetPlayer(_playerId);
        if (_player == null) return;

        _playerInfoLabel.Text = $"{_player.FullName} — {_player.Position} — Age {_player.Age} — {_player.Overall} OVR";

        long marketValue = gm.FreeAgency.GetEstimatedMarketValue(_player);
        double marketM = marketValue / 100_000_000.0;
        _marketValueLabel.Text = $"Estimated Market Value: ${marketM:N1}M/yr";

        // Pre-fill with market value estimates
        int years = _player.Age >= 33 ? 2 : _player.Age >= 30 ? 3 : _player.Overall >= 85 ? 4 : 3;
        _yearsSpinBox.Value = years;
        _totalSpinBox.Value = Math.Round(marketM * years, 1);
        _guaranteedSpinBox.Value = Math.Round(marketM * years * 0.45, 1);
        _bonusSpinBox.Value = Math.Round(marketM * years * 0.12, 1);

        // Cap space
        var team = gm.GetPlayerTeam();
        if (team != null)
            _capSpaceLabel.Text = $"Available Cap Space: {GameShell.FormatCurrency(team.CapSpace)}";
    }

    private void UpdateSummary()
    {
        double totalM = _totalSpinBox.Value;
        int years = (int)_yearsSpinBox.Value;
        double apyM = years > 0 ? totalM / years : 0;
        double bonusM = _bonusSpinBox.Value;

        // Estimate year 1 cap hit: (total - bonus) / years + bonus / min(years, 5)
        int prorationYears = Math.Min(years, 5);
        double basePerYear = years > 0 ? (totalM - bonusM) / years : 0;
        double proratedBonus = prorationYears > 0 ? bonusM / prorationYears : 0;
        double year1Hit = basePerYear + proratedBonus;

        _summaryLabel.Text = $"Annual Average: ${apyM:N1}M | Year 1 Cap Hit: ~${year1Hit:N1}M";
    }

    private void OnValueChanged(double _value) => UpdateSummary();

    private void OnSubmitPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null || _player == null) return;

        int years = (int)_yearsSpinBox.Value;
        long totalValue = (long)(_totalSpinBox.Value * 100_000_000);
        long guaranteed = (long)(_guaranteedSpinBox.Value * 100_000_000);
        long signingBonus = (long)(_bonusSpinBox.Value * 100_000_000);

        // Validate
        if (guaranteed > totalValue)
        {
            _statusLabel.Text = "Guaranteed money cannot exceed total value.";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
            return;
        }
        if (signingBonus > guaranteed)
        {
            _statusLabel.Text = "Signing bonus cannot exceed guaranteed money.";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
            return;
        }

        // Check cap space (rough year 1 estimate)
        var team = gm.GetPlayerTeam();
        if (team != null)
        {
            int prorationYears = Math.Min(years, 5);
            long basePerYear = years > 0 ? (totalValue - signingBonus) / years : 0;
            long proratedBonus = prorationYears > 0 ? signingBonus / prorationYears : 0;
            long year1Hit = basePerYear + proratedBonus;

            if (!gm.SalaryCapManager.CanAffordContract(team, year1Hit))
            {
                _statusLabel.Text = "Not enough cap space for this contract.";
                _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
                return;
            }
        }

        long apy = years > 0 ? totalValue / years : totalValue;

        var offer = new FreeAgentOffer
        {
            PlayerId = _playerId,
            TeamId = gm.PlayerTeamId,
            Years = years,
            TotalValue = totalValue,
            GuaranteedMoney = guaranteed,
            AnnualAverage = apy,
            SigningBonus = signingBonus,
            OfferWeek = gm.FreeAgencyWeekNumber,
            IsPlayerOffer = true,
        };

        gm.FreeAgency.MakePlayerOffer(offer);

        _statusLabel.Text = "Offer submitted! It will be evaluated when the week advances.";
        _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
    }

    private void OnClosePressed() => QueueFree();
}
