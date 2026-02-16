using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class Scout
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public int Accuracy { get; set; }
    public int Speed { get; set; }
    public ScoutSpecialty Specialty { get; set; }
    public ScoutRegion Region { get; set; }
    public int Salary { get; set; }
    public int Experience { get; set; }
}
