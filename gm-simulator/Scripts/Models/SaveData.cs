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

    // Player's Scouting State
    public Dictionary<string, float> ScoutingProgress { get; set; } = new();
    public List<string> DraftBoardOrder { get; set; } = new();
}
