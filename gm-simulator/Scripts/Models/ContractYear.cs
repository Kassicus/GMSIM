namespace GMSimulator.Models;

public class ContractYear
{
    public int Year { get; set; }
    public int YearNumber { get; set; }
    public long BaseSalary { get; set; }
    public long SigningBonus { get; set; }
    public long RosterBonus { get; set; }
    public long OptionBonus { get; set; }
    public long Incentives { get; set; }
    public long Guaranteed { get; set; }
    public long CapHit { get; set; }
    public long DeadCap { get; set; }
    public bool IsVoidYear { get; set; }
    public bool IsTeamOption { get; set; }
    public bool IsPlayerOption { get; set; }
}
