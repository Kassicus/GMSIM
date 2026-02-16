using GMSimulator.Models.Enums;

namespace GMSimulator.Models;

public class DepthChart
{
    public Dictionary<Position, List<string>> Chart { get; set; } = new();
    public Dictionary<string, List<DepthChartSlot>> Packages { get; set; } = new();
}

public class DepthChartSlot
{
    public Position Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
}
