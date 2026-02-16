using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class Contract
{
    public string PlayerId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public int TotalYears { get; set; }
    public long TotalValue { get; set; }
    public long TotalGuaranteed { get; set; }
    public List<ContractYear> Years { get; set; } = new();
    public ContractType Type { get; set; }
    public bool HasNoTradeClause { get; set; }
    public bool HasVoidYears { get; set; }
    public int VoidYearsCount { get; set; }

    public long AveragePerYear => TotalYears > 0 ? TotalValue / TotalYears : 0;

    public long GetCapHit(int currentYear)
    {
        return Years.FirstOrDefault(y => y.Year == currentYear)?.CapHit ?? 0;
    }

    public long CalculateDeadCap(int currentYear)
    {
        // Each year's DeadCap already represents the total dead cap if cut in that year
        // (remaining prorated signing bonus), so just return the current year's value.
        return Years.FirstOrDefault(y => y.Year == currentYear)?.DeadCap ?? 0;
    }
}
