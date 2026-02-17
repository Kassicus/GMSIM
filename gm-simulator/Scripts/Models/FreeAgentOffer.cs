namespace GMSimulator.Models;

public enum FreeAgentOfferStatus { Pending, Accepted, Rejected, Withdrawn, Expired }

public class FreeAgentOffer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = "";
    public string TeamId { get; set; } = "";
    public int Years { get; set; }
    public long TotalValue { get; set; }       // cents
    public long GuaranteedMoney { get; set; }   // cents
    public long AnnualAverage { get; set; }     // cents (TotalValue / Years)
    public long SigningBonus { get; set; }       // cents
    public int OfferWeek { get; set; }          // FA week 1-4
    public bool IsPlayerOffer { get; set; }     // true = from human player
    public FreeAgentOfferStatus Status { get; set; } = FreeAgentOfferStatus.Pending;
}
