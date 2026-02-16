namespace GMSimulator.Models;

public class Injury
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string InjuryType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int WeeksRemaining { get; set; }
    public int WeeksTotal { get; set; }
    public int GameWeekInjured { get; set; }
    public int SeasonInjured { get; set; }
    public bool CanReturn { get; set; }
}
