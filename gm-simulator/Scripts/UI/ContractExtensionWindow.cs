using Godot;
using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Systems;
using GMSimulator.UI.Theme;
using Pos = GMSimulator.Models.Enums.Position;

namespace GMSimulator.UI;

public partial class ContractExtensionWindow : Window
{
    private Label _playerInfoLabel = null!;
    private Label _currentContractLabel = null!;
    private SpinBox _yearsSpinBox = null!;
    private SpinBox _totalSpinBox = null!;
    private SpinBox _guaranteedSpinBox = null!;
    private Label _impactLabel = null!;
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
        _currentContractLabel = GetNode<Label>("MarginContainer/VBox/CurrentContractLabel");
        _yearsSpinBox = GetNode<SpinBox>("MarginContainer/VBox/YearsHBox/YearsSpinBox");
        _totalSpinBox = GetNode<SpinBox>("MarginContainer/VBox/TotalHBox/TotalSpinBox");
        _guaranteedSpinBox = GetNode<SpinBox>("MarginContainer/VBox/GuaranteedHBox/GuaranteedSpinBox");
        _impactLabel = GetNode<Label>("MarginContainer/VBox/ImpactLabel");
        _statusLabel = GetNode<Label>("MarginContainer/VBox/StatusLabel");

        PopulatePlayerInfo();
        UpdateImpact();
    }

    private void PopulatePlayerInfo()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        _player = gm.GetPlayer(_playerId);
        if (_player == null) return;

        _playerInfoLabel.Text = $"{_player.FullName} — {_player.Position} — Age {_player.Age} — {_player.Overall} OVR";

        // Current contract info
        if (_player.CurrentContract != null)
        {
            int remaining = _player.CurrentContract.Years
                .Count(y => y.Year >= gm.Calendar.CurrentYear);
            double totalM = _player.CurrentContract.TotalValue / 100_000_000.0;
            _currentContractLabel.Text = $"Current: {remaining}yr remaining — {GameShell.FormatCurrency(_player.CurrentContract.TotalValue)} total";

            // Pre-fill with reasonable extension values
            long marketAPY = ContractGenerator.GetMarketValue(_player);
            double marketM = marketAPY / 100_000_000.0;
            int addYears = _player.Age >= 30 ? 2 : 3;
            _yearsSpinBox.Value = addYears;
            _totalSpinBox.Value = Math.Round(marketM * (remaining + addYears), 1);
            _guaranteedSpinBox.Value = Math.Round(marketM * (remaining + addYears) * 0.50, 1);
        }
        else
        {
            _currentContractLabel.Text = "No current contract.";
        }
    }

    private void UpdateImpact()
    {
        var gm = GameManager.Instance;
        if (gm == null || _player?.CurrentContract == null) return;

        long currentCapHit = _player.CurrentContract.GetCapHit(gm.Calendar.CurrentYear);
        double currentM = currentCapHit / 100_000_000.0;

        int remaining = _player.CurrentContract.Years.Count(y => y.Year >= gm.Calendar.CurrentYear);
        int addYears = (int)_yearsSpinBox.Value;
        int totalYears = remaining + addYears;
        double totalM = _totalSpinBox.Value;
        double newAPY = totalYears > 0 ? totalM / totalYears : 0;

        _impactLabel.Text = $"Cap Impact: Current {GameShell.FormatCurrency(currentCapHit)}/yr → New ~${newAPY:N1}M/yr ({totalYears} total years)";
    }

    private void OnValueChanged(double _value) => UpdateImpact();

    private void OnExtendPressed()
    {
        var gm = GameManager.Instance;
        if (gm == null || _player == null) return;

        int addYears = (int)_yearsSpinBox.Value;
        long totalValue = (long)(_totalSpinBox.Value * 100_000_000);
        long guaranteed = (long)(_guaranteedSpinBox.Value * 100_000_000);

        if (guaranteed > totalValue)
        {
            _statusLabel.Text = "Guaranteed money cannot exceed total value.";
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
            return;
        }

        var newContract = ContractGenerator.GenerateExtensionContract(
            _player, gm.Calendar.CurrentYear, addYears, totalValue, guaranteed);

        var result = gm.RosterManager.ExtendContract(_playerId, newContract);

        if (result.Success)
        {
            _statusLabel.Text = result.Message;
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Success);
        }
        else
        {
            _statusLabel.Text = result.Message;
            _statusLabel.AddThemeColorOverride("font_color", ThemeColors.Danger);
        }
    }

    private void OnClosePressed() => QueueFree();
}
