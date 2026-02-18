using GMSimulator.Models.Enums;
using GMSimulator.Systems;

namespace GMSimulator.Models;

public class SaveData
{
    // Meta
    public string SaveId { get; set; } = Guid.NewGuid().ToString();
    public string SaveName { get; set; } = string.Empty;
    public DateTime SaveDate { get; set; }
    public string GameVersion { get; set; } = "0.1.0";
    public int Seed { get; set; }

    // Game State
    public int CurrentYear { get; set; }
    public GamePhase CurrentPhase { get; set; }
    public int CurrentWeek { get; set; }
    public string PlayerTeamId { get; set; } = string.Empty;

    // All Entities
    public List<Team> Teams { get; set; } = new();
    public List<Player> Players { get; set; } = new();
    public List<Coach> Coaches { get; set; } = new();
    public List<Scout> Scouts { get; set; } = new();
    public List<Scout> ScoutMarket { get; set; } = new();
    public List<Prospect> CurrentDraftClass { get; set; } = new();
    public List<DraftPick> AllDraftPicks { get; set; } = new();

    // History
    public List<Season> SeasonHistory { get; set; } = new();
    public List<TransactionRecord> TransactionLog { get; set; } = new();

    // Season State (Phase 3)
    public Season? CurrentSeason { get; set; }
    public List<PlayoffSeed>? AFCPlayoffSeeds { get; set; }
    public List<PlayoffSeed>? NFCPlayoffSeeds { get; set; }

    // Free Agency State (Phase 4)
    public List<FreeAgentOffer> PendingOffers { get; set; } = new();
    public List<string> FreeAgentPool { get; set; } = new();
    public int FreeAgencyWeek { get; set; }

    // AI State
    public Dictionary<string, AIGMProfile> AIProfiles { get; set; } = new();

    // Scouting & Draft State (Phase 5)
    public Dictionary<string, float> ScoutingProgress { get; set; } = new();
    public List<string> DraftBoardOrder { get; set; } = new();
    public int ScoutingWeeklyPool { get; set; }
    public int ScoutingCurrentPoints { get; set; }
    public int DraftCurrentRound { get; set; }
    public int DraftCurrentPick { get; set; }
    public Dictionary<string, int> DraftBoardTags { get; set; } = new();

    // Trading State (Phase 6)
    public List<TradeRecord> TradeHistory { get; set; } = new();
    public List<string> TradeBlockPlayerIds { get; set; } = new();
    public List<TradeProposal> PendingTradeProposals { get; set; } = new();
    public Dictionary<string, float> TradeRelationships { get; set; } = new();

    // Staff & Coaching State (Phase 7)
    public List<string> CoachingMarketIds { get; set; } = new();

    // Progression & AI State (Phase 8)
    public List<SeasonAwards> AllAwards { get; set; } = new();
    public List<string> RetiredPlayerIds { get; set; } = new();
    public List<string> HallOfFameIds { get; set; } = new();
}
