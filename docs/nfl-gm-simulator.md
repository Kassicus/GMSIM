# NFL GM Simulator — Game Design & Technical Specification

## For Godot 4.x with .NET (C#)

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture & Project Structure](#2-architecture--project-structure)
3. [Core Data Models](#3-core-data-models)
4. [Game Loop & Season Structure](#4-game-loop--season-structure)
5. [Roster & Lineup Management](#5-roster--lineup-management)
6. [Scouting System](#6-scouting-system)
7. [NFL Draft](#7-nfl-draft)
8. [Free Agency](#8-free-agency)
9. [Contract System & Salary Cap](#9-contract-system--salary-cap)
10. [Trading System](#10-trading-system)
11. [Staff & Coaching](#11-staff--coaching)
12. [Game Simulation Engine](#12-game-simulation-engine)
13. [Player Progression & Regression](#13-player-progression--regression)
14. [Injuries](#14-injuries)
15. [AI GM Behavior](#15-ai-gm-behavior)
16. [UI/UX Design](#16-uiux-design)
17. [Save/Load System](#17-saveload-system)
18. [Data Seeding & League Generation](#18-data-seeding--league-generation)
19. [Implementation Phases](#19-implementation-phases)
20. [File Manifest](#20-file-manifest)

---

## 1. Project Overview

### Concept

A deep NFL General Manager simulation where the player never touches the field. You build rosters, manage the salary cap, scout and draft prospects, negotiate contracts, hire coaching staffs, and watch your decisions play out through simulated games. The gameplay emphasis is on the offseason — free agency, the draft, scouting — with the regular season serving as the proving ground for your roster construction.

### Design Pillars

- **Authenticity**: Mirror real NFL structures — 32 teams, 17-game seasons, salary cap, compensatory picks, franchise tags, practice squads, IR, the full works.
- **Depth Over Flash**: Every decision has downstream consequences. A bad contract haunts you for years. A great draft pick transforms a franchise.
- **Readability**: Complex data presented cleanly. The UI should feel like a modern sports management tool — think Madden's roster screens meets Football Manager's depth.
- **Replayability**: Procedurally generated draft classes, dynamic free agent markets, coach AI that varies game-to-game.

### Tech Stack

- **Engine**: Godot 4.x
- **Language**: C# (.NET)
- **Data Format**: JSON for static data, Godot Resource (.tres) for runtime objects, JSON serialization for saves
- **UI Framework**: Godot Control nodes with custom themes

---

## 2. Architecture & Project Structure

### High-Level Architecture

```
┌─────────────────────────────────────────────┐
│                  GameManager                │
│  (Singleton — owns the master game state)   │
├─────────────────────────────────────────────┤
│  LeagueState    │  CalendarSystem           │
│  SalaryCapMgr   │  SimulationEngine         │
│  DraftSystem     │  FreeAgencySystem         │
│  TradeSystem     │  ScoutingSystem           │
│  StaffSystem     │  ProgressionSystem        │
│  InjurySystem    │  AIGMController           │
└─────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────┐
│               UI Layer (Scenes)             │
│  MainMenu │ Dashboard │ Roster │ Draft      │
│  FreeAgency │ Scouting │ TradeBlock │ Staff │
│  GameDay │ Standings │ PlayerCard │ Settings │
└─────────────────────────────────────────────┘
```

### Directory Structure

```
res://
├── project.godot
├── Scenes/
│   ├── Main/
│   │   ├── MainMenu.tscn
│   │   ├── NewGameSetup.tscn
│   │   └── GameShell.tscn              # persistent HUD/nav wrapper
│   ├── Dashboard/
│   │   └── Dashboard.tscn              # team overview home screen
│   ├── Roster/
│   │   ├── RosterView.tscn             # full 53-man + PS roster
│   │   ├── DepthChart.tscn             # Madden-style position grid
│   │   ├── PlayerCard.tscn             # detailed player info popup
│   │   └── LineupEditor.tscn           # drag-drop lineup management
│   ├── Scouting/
│   │   ├── ScoutingHub.tscn
│   │   ├── ProspectCard.tscn
│   │   └── CombineView.tscn
│   ├── Draft/
│   │   ├── DraftRoom.tscn
│   │   ├── DraftBoard.tscn
│   │   └── DraftResults.tscn
│   ├── FreeAgency/
│   │   ├── FreeAgentMarket.tscn
│   │   ├── NegotiationScreen.tscn
│   │   └── FranchiseTagView.tscn
│   ├── Trade/
│   │   ├── TradeHub.tscn
│   │   ├── TradeProposal.tscn
│   │   └── TradeBlock.tscn
│   ├── Staff/
│   │   ├── StaffOverview.tscn
│   │   ├── CoachCard.tscn
│   │   └── HiringScreen.tscn
│   ├── GameDay/
│   │   ├── WeekSchedule.tscn
│   │   ├── GameSimView.tscn            # live sim ticker / box score
│   │   └── PostGameReport.tscn
│   └── League/
│       ├── Standings.tscn
│       ├── LeagueLeaders.tscn
│       ├── ScheduleView.tscn
│       └── TransactionLog.tscn
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs              # singleton, master state
│   │   ├── CalendarSystem.cs           # drives the game clock
│   │   ├── SaveLoadManager.cs
│   │   └── EventBus.cs                 # global signal/event system
│   ├── Models/
│   │   ├── Player.cs
│   │   ├── Contract.cs
│   │   ├── DraftPick.cs
│   │   ├── Team.cs
│   │   ├── Coach.cs
│   │   ├── Scout.cs
│   │   ├── Prospect.cs
│   │   ├── Game.cs
│   │   ├── Season.cs
│   │   ├── Injury.cs
│   │   └── Enums/
│   │       ├── Position.cs
│   │       ├── Conference.cs
│   │       ├── Division.cs
│   │       ├── Phase.cs
│   │       └── Archetype.cs
│   ├── Systems/
│   │   ├── SimulationEngine.cs
│   │   ├── DraftSystem.cs
│   │   ├── FreeAgencySystem.cs
│   │   ├── TradeSystem.cs
│   │   ├── ScoutingSystem.cs
│   │   ├── SalaryCapManager.cs
│   │   ├── StaffSystem.cs
│   │   ├── ProgressionSystem.cs
│   │   ├── InjurySystem.cs
│   │   ├── ScheduleGenerator.cs
│   │   └── AIGMController.cs
│   └── UI/
│       ├── PlayerCardUI.cs
│       ├── DepthChartUI.cs
│       ├── DraftBoardUI.cs
│       ├── ContractNegotiationUI.cs
│       ├── TradeProposalUI.cs
│       └── Components/
│           ├── AttributeBar.cs
│           ├── OverallBadge.cs
│           ├── CapSpaceIndicator.cs
│           └── DraftPickChip.cs
├── Resources/
│   ├── Data/
│   │   ├── teams.json
│   │   ├── firstnames.json
│   │   ├── lastnames.json
│   │   ├── colleges.json
│   │   ├── coach_names.json
│   │   ├── archetypes.json
│   │   └── salary_cap_rules.json
│   └── Themes/
│       ├── MainTheme.tres
│       └── TeamThemes/                 # per-team color schemes
├── Assets/
│   ├── Fonts/
│   ├── Icons/
│   ├── TeamLogos/                      # 32 team logos
│   └── UI/
└── addons/
```

### Singleton Registration

Register `GameManager` as an autoload in `project.godot`:

```
[autoload]
GameManager="*res://Scripts/Core/GameManager.cs"
EventBus="*res://Scripts/Core/EventBus.cs"
```

---

## 3. Core Data Models

### 3.1 Enums

```csharp
// Position.cs
public enum Position
{
    // Offense
    QB, HB, FB, WR, TE, LT, LG, C, RG, RT,
    // Defense
    EDGE, DT, MLB, OLB, CB, FS, SS,
    // Special Teams
    K, P, LS
}

// Archetype.cs — sub-roles within a position
public enum Archetype
{
    // QB
    PocketPasser, Scrambler, FieldGeneral,
    // HB
    PowerBack, SpeedBack, EllusiveBack, ReceivingBack,
    // WR
    DeepThreat, PossessionReceiver, SlotReceiver, RouteRunner,
    // TE
    BlockingTE, ReceivingTE, Versatile,
    // OL
    PassProtector, RunBlocker, Balanced,
    // EDGE
    SpeedRusher, PowerRusher, RunStopper,
    // DT
    NoseTackle, PassRushDT, ThreeDown,
    // LB
    RunStuffer, CoverageLB, Blitzer,
    // CB
    ManCoverage, ZoneCoverage, SlotCorner,
    // S
    CenterFielder, BoxSafety, Hybrid,
    // K/P
    Accurate, BigLeg,
    // LS
    Standard
}

// Phase.cs — the game calendar phases
public enum GamePhase
{
    // Offseason
    PostSeason,         // Super Bowl → coaching carousel
    CombineScouting,    // Combine + Pro Days
    FreeAgency,         // FA opens, signing period
    PreDraft,           // final board adjustments
    Draft,              // 7-round draft
    PostDraft,          // UDFA signings, OTAs
    // Season
    Preseason,
    RegularSeason,
    Playoffs,
    SuperBowl
}

// Conference.cs / Division.cs
public enum Conference { AFC, NFC }
public enum Division { North, South, East, West }
```

### 3.2 Player Model

```csharp
public class Player
{
    // Identity
    public string Id { get; set; }              // GUID
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public int Age { get; set; }
    public int YearsInLeague { get; set; }
    public string College { get; set; }
    public int DraftYear { get; set; }
    public int DraftRound { get; set; }
    public int DraftPick { get; set; }
    public bool IsUndrafted { get; set; }

    // Football
    public Position Position { get; set; }
    public Archetype Archetype { get; set; }
    public int Overall { get; set; }            // 40–99, calculated from attributes
    public int PotentialCeiling { get; set; }    // hidden unless fully scouted
    public PlayerAttributes Attributes { get; set; }
    public PlayerTraits Traits { get; set; }

    // Status
    public string TeamId { get; set; }           // null = free agent
    public Contract CurrentContract { get; set; }
    public InjuryStatus InjuryStatus { get; set; }
    public RosterStatus RosterStatus { get; set; } // Active53, PracticeSquad, IR, PUP, Exempt
    public int Morale { get; set; }              // 0–100
    public int Fatigue { get; set; }             // 0–100

    // Career Stats
    public Dictionary<int, SeasonStats> CareerStats { get; set; }

    // Progression
    public DevelopmentTrait DevTrait { get; set; } // Normal, Star, Superstar, XFactor
    public int TrajectoryModifier { get; set; }    // -3 to +3, affects progression curve
}

public enum RosterStatus
{
    Active53, PracticeSquad, InjuredReserve, PUP, NFI, Exempt, Suspended, FreeAgent, Retired
}

public enum DevelopmentTrait { Normal, Star, Superstar, XFactor }
```

### 3.3 Player Attributes

Attributes are grouped by category. Every attribute is an `int` from 0–99.

```csharp
public class PlayerAttributes
{
    // Physical
    public int Speed { get; set; }
    public int Acceleration { get; set; }
    public int Agility { get; set; }
    public int Strength { get; set; }
    public int Jumping { get; set; }
    public int Stamina { get; set; }
    public int Toughness { get; set; }
    public int InjuryResistance { get; set; }

    // Passing (primarily QB)
    public int ThrowPower { get; set; }
    public int ShortAccuracy { get; set; }
    public int MediumAccuracy { get; set; }
    public int DeepAccuracy { get; set; }
    public int ThrowOnRun { get; set; }
    public int PlayAction { get; set; }

    // Rushing
    public int Carrying { get; set; }
    public int BallCarrierVision { get; set; }
    public int BreakTackle { get; set; }
    public int Trucking { get; set; }
    public int Elusiveness { get; set; }
    public int SpinMove { get; set; }
    public int JukeMove { get; set; }
    public int StiffArm { get; set; }

    // Receiving
    public int Catching { get; set; }
    public int CatchInTraffic { get; set; }
    public int SpectacularCatch { get; set; }
    public int RouteRunning { get; set; }
    public int Release { get; set; }

    // Blocking
    public int RunBlock { get; set; }
    public int PassBlock { get; set; }
    public int ImpactBlock { get; set; }
    public int LeadBlock { get; set; }

    // Defense
    public int Tackle { get; set; }
    public int HitPower { get; set; }
    public int PowerMoves { get; set; }
    public int FinesseMoves { get; set; }
    public int BlockShedding { get; set; }
    public int Pursuit { get; set; }
    public int PlayRecognition { get; set; }
    public int ManCoverage { get; set; }
    public int ZoneCoverage { get; set; }
    public int Press { get; set; }

    // Special Teams
    public int KickPower { get; set; }
    public int KickAccuracy { get; set; }

    // Mental
    public int Awareness { get; set; }
    public int Clutch { get; set; }            // performance in high-leverage moments
    public int Consistency { get; set; }        // variance in week-to-week output
    public int Leadership { get; set; }         // locker room impact
}
```

### 3.4 Player Traits

Boolean or enum-based traits that modify simulation behavior:

```csharp
public class PlayerTraits
{
    public bool FightForYards { get; set; }
    public bool HighMotor { get; set; }
    public bool Clutch { get; set; }
    public bool PenaltyProne { get; set; }
    public bool BigGamePlayer { get; set; }
    public bool TeamPlayer { get; set; }
    public bool LockerRoomCancer { get; set; }
    public bool IronMan { get; set; }          // lower injury rates
    public bool GlassBody { get; set; }        // higher injury rates
    public SenseOfPressure SenseOfPressure { get; set; } // Trigger Happy, Ideal, Oblivious
    public ForcePasses ForcePasses { get; set; }         // Conservative, Balanced, Aggressive
    public CoversBall CoversBall { get; set; }           // Always, Situational, Never
}
```

### 3.5 Contract Model

```csharp
public class Contract
{
    public string PlayerId { get; set; }
    public string TeamId { get; set; }
    public int TotalYears { get; set; }
    public long TotalValue { get; set; }        // total $ value
    public long TotalGuaranteed { get; set; }
    public List<ContractYear> Years { get; set; }
    public ContractType Type { get; set; }
    public bool HasNoTradeClause { get; set; }
    public bool HasVoidYears { get; set; }
    public int VoidYearsCount { get; set; }

    // Computed
    public long AveragePerYear => TotalValue / TotalYears;
    public long CurrentYearCapHit => Years?.FirstOrDefault(y => y.Year == GameManager.Instance.CurrentYear)?.CapHit ?? 0;
    public long DeadCapIfCut => CalculateDeadCap();
}

public class ContractYear
{
    public int Year { get; set; }               // calendar year
    public int YearNumber { get; set; }         // 1, 2, 3...
    public long BaseSalary { get; set; }
    public long SigningBonus { get; set; }       // prorated across years
    public long RosterBonus { get; set; }
    public long OptionBonus { get; set; }
    public long Incentives { get; set; }         // likely-to-be-earned vs not
    public long Guaranteed { get; set; }
    public long CapHit { get; set; }             // calculated
    public long DeadCap { get; set; }            // calculated
    public bool IsVoidYear { get; set; }
    public bool IsTeamOption { get; set; }
    public bool IsPlayerOption { get; set; }
}

public enum ContractType
{
    Rookie, Veteran, Extension, FranchiseTag, TransitionTag, MinimumSalary, PracticeSquad, UDFA
}
```

### 3.6 Team Model

```csharp
public class Team
{
    // Identity
    public string Id { get; set; }
    public string City { get; set; }
    public string Name { get; set; }
    public string Abbreviation { get; set; }     // e.g. "KC"
    public string FullName => $"{City} {Name}";
    public Conference Conference { get; set; }
    public Division Division { get; set; }

    // Branding
    public Color PrimaryColor { get; set; }
    public Color SecondaryColor { get; set; }
    public string LogoPath { get; set; }

    // Roster
    public List<string> PlayerIds { get; set; }   // refs to Player.Id
    public DepthChart DepthChart { get; set; }
    public List<string> PracticeSquadIds { get; set; }
    public List<string> IRPlayerIds { get; set; }

    // Front Office
    public string HeadCoachId { get; set; }
    public string OffensiveCoordinatorId { get; set; }
    public string DefensiveCoordinatorId { get; set; }
    public string SpecialTeamsCoordId { get; set; }
    public List<string> PositionCoachIds { get; set; }
    public List<string> ScoutIds { get; set; }

    // Financials
    public long SalaryCap { get; set; }
    public long CurrentCapUsed { get; set; }
    public long DeadCapTotal { get; set; }
    public long CapSpace => SalaryCap - CurrentCapUsed;
    public long CarryoverCap { get; set; }       // rolled from previous year

    // Draft Capital
    public List<DraftPick> DraftPicks { get; set; }

    // Record
    public TeamRecord CurrentRecord { get; set; }
    public List<TeamRecord> SeasonHistory { get; set; }

    // Team Needs & Settings
    public SchemeType OffensiveScheme { get; set; }
    public SchemeType DefensiveScheme { get; set; }
    public List<Position> TeamNeeds { get; set; }  // AI-determined or player-set
    public int FanSatisfaction { get; set; }        // 0–100
    public int OwnerPatience { get; set; }          // 0–100, low = hot seat
}

public class TeamRecord
{
    public int Season { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Ties { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }
    public int DivisionRank { get; set; }
    public bool MadePlayoffs { get; set; }
    public string PlayoffResult { get; set; }     // "Wild Card Loss", "Super Bowl Champion", etc.
}
```

### 3.7 Coach Model

```csharp
public class Coach
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public CoachRole Role { get; set; }
    public string TeamId { get; set; }

    // Ratings 0–99
    public int OffenseRating { get; set; }
    public int DefenseRating { get; set; }
    public int SpecialTeamsRating { get; set; }
    public int GameManagement { get; set; }      // clock, challenges, 4th down decisions
    public int PlayerDevelopment { get; set; }   // affects progression rates
    public int Motivation { get; set; }          // morale impact
    public int Adaptability { get; set; }        // adjusts scheme to roster
    public int Recruiting { get; set; }          // FA interest modifier

    // Scheme Preferences
    public SchemeType PreferredOffense { get; set; }
    public SchemeType PreferredDefense { get; set; }

    // Personality
    public CoachPersonality Personality { get; set; }
    public int Prestige { get; set; }            // 0–100, affects FA pull
    public int Experience { get; set; }          // years coaching

    // Track Record
    public int CareerWins { get; set; }
    public int CareerLosses { get; set; }
    public int PlayoffAppearances { get; set; }
    public int SuperBowlWins { get; set; }
}

public enum CoachRole
{
    HeadCoach, OffensiveCoordinator, DefensiveCoordinator,
    SpecialTeamsCoordinator, QBCoach, RBCoach, WRCoach,
    OLineCoach, DLineCoach, LBCoach, DBCoach
}

public enum SchemeType
{
    // Offense
    WestCoast, AirRaid, SpreadOption, ProStyle, RunHeavy, RPO,
    // Defense
    Cover3, Cover2Tampa, Cover1ManPress, ThreeFour, FourThree, Hybrid, MultipleDefense
}

public enum CoachPersonality
{
    PlayersCoach, Disciplinarian, Innovator, OldSchool, FireBrand
}
```

### 3.8 Scout Model

```csharp
public class Scout
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Accuracy { get; set; }            // 50–99, how close scouted ratings are to true ratings
    public int Speed { get; set; }               // how fast scouting completes
    public ScoutSpecialty Specialty { get; set; } // position group expertise
    public ScoutRegion Region { get; set; }       // regional expertise bonus
    public int Salary { get; set; }
    public int Experience { get; set; }
}

public enum ScoutSpecialty { Offense, Defense, SpecialTeams, AllAround }
public enum ScoutRegion { Northeast, Southeast, Midwest, West, National }
```

### 3.9 Draft Pick Model

```csharp
public class DraftPick
{
    public string Id { get; set; }
    public int Year { get; set; }
    public int Round { get; set; }
    public string OriginalTeamId { get; set; }   // team that originally held the pick
    public string CurrentTeamId { get; set; }     // team that currently owns it
    public int? OverallNumber { get; set; }       // set when draft order is finalized
    public bool IsCompensatory { get; set; }
    public bool IsConditional { get; set; }
    public string Condition { get; set; }         // e.g. "becomes 2nd if player makes Pro Bowl"

    // After the pick is used
    public string SelectedPlayerId { get; set; }
    public bool IsUsed { get; set; }
}
```

### 3.10 Prospect Model

```csharp
public class Prospect
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public string College { get; set; }
    public Position Position { get; set; }
    public Archetype Archetype { get; set; }

    // True ratings (hidden from player until scouted)
    public PlayerAttributes TrueAttributes { get; set; }
    public int TruePotential { get; set; }
    public DevelopmentTrait TrueDevTrait { get; set; }
    public PlayerTraits TrueTraits { get; set; }

    // Scouted ratings (what the player sees)
    public PlayerAttributes ScoutedAttributes { get; set; }
    public int? ScoutedPotential { get; set; }
    public float ScoutingProgress { get; set; }  // 0.0–1.0
    public ScoutingGrade ScoutGrade { get; set; }

    // Combine / Pro Day
    public CombineResults CombineResults { get; set; }
    public bool AttendedCombine { get; set; }
    public bool HadProDay { get; set; }

    // Draft
    public int ProjectedRound { get; set; }      // consensus mock draft range
    public float DraftValue { get; set; }         // internal trade chart value
    public List<string> RedFlags { get; set; }    // "Character Concerns", "Injury History", etc.
    public List<string> Strengths { get; set; }
    public List<string> Weaknesses { get; set; }

    // After Draft
    public bool IsDrafted { get; set; }
    public int? DraftedRound { get; set; }
    public int? DraftedPick { get; set; }
    public string DraftedByTeamId { get; set; }
}

public class CombineResults
{
    public float? FortyYardDash { get; set; }     // seconds
    public int? BenchPress { get; set; }          // reps at 225 lbs
    public float? VerticalJump { get; set; }      // inches
    public float? BroadJump { get; set; }         // inches
    public float? ThreeConeDrill { get; set; }    // seconds
    public float? ShuttleRun { get; set; }        // seconds
    public int? WonderlicScore { get; set; }      // 0–50 (or S2 equivalent)
    public string MedicalGrade { get; set; }      // "Pass", "Flag", "Fail"
}

public enum ScoutingGrade
{
    Unscouted,          // 0%
    Initial,            // ~25% — rough position/size/speed
    Intermediate,       // ~50% — key attributes revealed
    Advanced,           // ~75% — most attributes + some traits
    FullyScounted       // 100% — all attributes, traits, potential visible
}
```

### 3.11 Depth Chart

```csharp
public class DepthChart
{
    // Key: Position enum, Value: ordered list of player IDs (index 0 = starter)
    public Dictionary<Position, List<string>> Chart { get; set; }

    // Offensive formation slots (maps to positions for specific packages)
    // e.g., 11 Personnel: 1 RB, 1 TE, 3 WR
    // e.g., 12 Personnel: 1 RB, 2 TE, 2 WR
    public Dictionary<string, List<DepthChartSlot>> Packages { get; set; }
}

public class DepthChartSlot
{
    public Position Position { get; set; }
    public string Label { get; set; }        // "WR1", "Slot WR", "Nickel CB"
    public string PlayerId { get; set; }
}
```

---

## 4. Game Loop & Season Structure

### Calendar System

The game operates on a phase-based calendar. Each phase contains a set number of "action days" or events. The player advances through the calendar by completing actions or pressing "Advance."

```
NFL CALENDAR (simplified)

OFFSEASON:
  Week 1-2:   PostSeason
               → Coaching carousel (firings/hirings)
               → End-of-season awards
               → Retirement announcements

  Week 3-4:   CombineScouting
               → NFL Combine (auto-generated results for prospects)
               → Pro Days (assign scouts to attend)
               → Begin building draft board

  Week 5-8:   FreeAgency
               → Franchise/Transition tag window (1 week)
               → Free agency opens (legal tampering period → full FA)
               → Sign, negotiate, lose players
               → Compensatory pick formula runs

  Week 9-10:  PreDraft
               → Private workouts
               → Final draft board arrangements
               → Trade up/down negotiations

  Week 11:    Draft
               → 7 rounds over 3 days
               → UDFA signing period after draft

  Week 12-14: PostDraft
               → OTAs, minicamp (attribute reveals for rookies)
               → Final roster cuts incoming

SEASON:
  Week 15-18: Preseason
               → 3 preseason games
               → Roster cuts: 90 → 53 + 16 practice squad

  Week 19-35: RegularSeason (17 games, 1 bye)
               → Weekly game simulation
               → Trade deadline (Week 8 of regular season)
               → Waiver wire
               → Injury management
               → Bye week scouting bonus

  Week 36-39: Playoffs
               → Wild Card, Divisional, Conference Championship, Super Bowl
               → No roster moves (except injury replacements)

  Week 40:    SuperBowl → loop back to PostSeason
```

### CalendarSystem.cs (Core Logic)

```csharp
public class CalendarSystem
{
    public int CurrentYear { get; set; }
    public GamePhase CurrentPhase { get; set; }
    public int CurrentWeek { get; set; }         // week within current phase
    public int DaysInCurrentPhase { get; set; }

    // Signals
    [Signal] public delegate void PhaseChangedEventHandler(GamePhase newPhase);
    [Signal] public delegate void WeekAdvancedEventHandler(int week);
    [Signal] public delegate void SeasonEndedEventHandler(int year);

    public void AdvanceDay() { /* process daily events */ }
    public void AdvanceWeek() { /* process weekly events, sim games if in-season */ }
    public void AdvanceToNextPhase() { /* skip to next major phase */ }
    public bool CanAdvance() { /* check for blocking events requiring player input */ }
}
```

### Phase Transition Events

Each phase transition triggers specific system events:

| Transition | Events Triggered |
|---|---|
| PostSeason → CombineScouting | Generate draft class, fire underperforming coaches, process retirements |
| CombineScouting → FreeAgency | Generate combine results, tag window opens |
| FreeAgency → PreDraft | FA market settles, compensatory picks calculated |
| PreDraft → Draft | Lock draft order, finalize boards |
| Draft → PostDraft | Rookie contracts assigned, UDFA pool created |
| PostDraft → Preseason | Generate schedule, roster at 90 |
| Preseason → RegularSeason | Final cuts to 53 + PS, set opening depth chart |
| RegularSeason → Playoffs | Seed playoff bracket |
| Playoffs → SuperBowl | Conference champions set |
| SuperBowl → PostSeason | Award ceremony, year increments, age all players |

---

## 5. Roster & Lineup Management

### Roster Rules (mirroring NFL)

- **Active Roster**: 53 players max
- **Practice Squad**: 16 players (max 6 with more than 2 accrued seasons)
- **Injured Reserve**: Unlimited, player must miss minimum 4 games; 8 IR-return designations per season
- **PUP List**: Physically Unable to Perform — player misses first 4 weeks minimum
- **Game Day Roster**: 48 active on game day (5 inactives from 53)
- **Offseason Roster**: 90 players during OTAs/minicamp

### Depth Chart Structure

The depth chart uses NFL standard positions with the starter at index 0:

```
OFFENSE:
  QB:   [Starter, Backup, 3rd String]
  HB:   [Starter, Backup]
  FB:   [Starter]
  WR:   [WR1, WR2, Slot WR, WR4, WR5, WR6]
  TE:   [TE1, TE2, TE3]
  LT:   [Starter, Backup]
  LG:   [Starter, Backup]
  C:    [Starter, Backup]
  RG:   [Starter, Backup]
  RT:   [Starter, Backup]

DEFENSE (Base 4-3):
  EDGE: [LE, RE, Backup]
  DT:   [DT1, DT2, Backup]
  MLB:  [Starter, Backup]
  OLB:  [LOLB, ROLB, Backup]
  CB:   [CB1, CB2, Nickel, Dime, Backup]
  FS:   [Starter, Backup]
  SS:   [Starter, Backup]

SPECIAL TEAMS:
  K:    [Starter]
  P:    [Starter]
  LS:   [Starter]
  KR:   [Starter, Backup]     (references existing player)
  PR:   [Starter, Backup]     (references existing player)
```

### Lineup Editor (Madden-Style)

The lineup editor displays a visual grid where each slot shows a player "card" with:

- Player name and number
- Position and archetype
- Overall rating (large, color-coded badge: 90+ = elite gold, 80+ = green, 70+ = blue, 60+ = orange, <60 = red)
- Key attributes (top 3 for that position)
- Contract year indicator (🔴 final year, 🟡 2nd to last)
- Injury indicator if applicable

**Interactions:**
- Click a card → opens full PlayerCard view
- Drag a card to swap positions in depth chart
- Right-click → context menu: Trade, Cut, Extend, Move to IR, Set as Captain
- Filter by: Position Group (OFF/DEF/ST), Roster Status, Overall Range
- Sort by: Overall, Age, Salary, Name

### PlayerCard View

Full player detail popup:

```
┌──────────────────────────────────────────────────┐
│  ┌──────┐  PATRICK MAHOMES            #15       │
│  │      │  QB | Field General | Age 29          │
│  │ PHOTO│  Kansas City Chiefs                    │
│  │ AREA │  6'3" 230 lbs | Texas Tech            │
│  └──────┘  Draft: 2017 Rd 1, Pick 10            │
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │  OVERALL    │  DEV TRAIT   │  MORALE     │    │
│  │    97       │  X-Factor    │   92        │    │
│  └──────────────────────────────────────────┘    │
│                                                  │
│  CONTRACT                                        │
│  4yr / $180M / $141M GTD                        │
│  Cap Hit: $46.8M | Dead Cap: $82.3M             │
│  ─────────────────────────────────────────       │
│  Year 1: $46.8M ██████████████████░░ (current)  │
│  Year 2: $48.2M ████████████████████░           │
│  Year 3: $42.5M ████████████████░░░░            │
│  Year 4: $42.5M ████████████████░░░░            │
│                                                  │
│  ATTRIBUTES                     TRAITS           │
│  Throw Power:    97 █████████▓  ✓ Clutch        │
│  Short Acc:      96 █████████▓  ✓ Big Game      │
│  Medium Acc:     94 █████████▒  ✓ Fight for Yds │
│  Deep Acc:       90 █████████░  ✗ Penalty Prone │
│  Throw on Run:   98 ██████████                   │
│  Speed:          78 ███████▓░░                   │
│  Awareness:      99 ██████████                   │
│  ... (expandable)                                │
│                                                  │
│  SEASON STATS                                    │
│  GP: 14 | Comp: 312 | Att: 468 | Yds: 4,183    │
│  TD: 32 | INT: 8 | Rtg: 105.2 | QBR: 72.4     │
│                                                  │
│  CAREER STATS          ACTIONS                   │
│  [Career Table]        [Extend] [Trade] [Cut]    │
│                        [Restructure] [Tag]       │
└──────────────────────────────────────────────────┘
```

---

## 6. Scouting System

### Overview

Scouting is how the player uncovers prospect ratings before the draft. Without scouting, prospects show only basic measurables (height, weight, 40 time, position) and a generic projected round. Deeper scouting reveals true attributes, potential, traits, and red flags.

### Scout Staff

The player manages a scouting department:
- **Director of Scouting** (1) — boosts all scouts' accuracy by a percentage
- **Area Scouts** (4–8) — assigned to regions or specific prospects
- Each scout has `Accuracy`, `Speed`, `Specialty`, and `Region`

### Scouting Process

1. **Auto-Scouting (Combine/Pro Day)**: All prospects get baseline scouting (~15%) from public combine data. Physical attributes partially revealed.

2. **Assigned Scouting**: Player assigns scouts to specific prospects. Each week of scouting adds progress based on scout speed. A scout with Speed 80 might add 15% per week; Speed 50 adds 8%.

3. **Scouting Accuracy**: When attributes are "revealed," they aren't shown as true values. They're shown as the true value ± an error margin based on scout accuracy. A 90 accuracy scout might show a 85 Speed attribute as "83–87 Speed." A 60 accuracy scout might show it as "78–92 Speed."

4. **Scouting Points (Optional Budget)**: The player has a scouting budget of ~1500 points per draft cycle. Each prospect costs points to scout based on depth:
   - Initial Report: 10 points (25% scouted)
   - Intermediate: 25 points (50%)
   - Advanced: 50 points (75%)
   - Full Workup: 100 points (100%)
   - Private Workout: 75 points (provides combine-like data for players who skipped combine)

### Scouting Reveal Tiers

| Tier | Progress | What's Revealed |
|---|---|---|
| Unscouted (0%) | 0% | Name, position, college, height/weight, projected round |
| Initial (25%) | 25% | Physical attributes (Speed, Strength, Agility), combine results, archetype |
| Intermediate (50%) | 50% | Primary position attributes (e.g., ThrowPower for QB), 1–2 traits, strengths list |
| Advanced (75%) | 75% | All attributes (with accuracy variance), most traits, weaknesses, red flags |
| Full (100%) | 100% | True potential ceiling, development trait, all traits, full comparison to NFL players |

### Draft Board

The player's personal draft board:
- Rank prospects in order of preference (drag-and-drop)
- Tag prospects with custom labels: "Must Have", "Good Value", "Reach", "Do Not Draft"
- Filter by position, round projection, scouting tier
- Compare up to 4 prospects side-by-side
- AI will suggest its own rankings based on team needs + BPA (Best Player Available)

---

## 7. NFL Draft

### Structure

- **7 Rounds**, 32 picks per round (+ compensatory picks in rounds 3–7)
- Total picks: ~256–262 per draft
- **Day 1**: Round 1 (each pick gets individual attention)
- **Day 2**: Rounds 2–3
- **Day 3**: Rounds 4–7

### Draft Day Experience

The Draft Room scene shows:
- Current pick number and team on the clock
- A countdown timer (team has X seconds — accelerated for AI teams)
- The player's draft board on the left
- Team needs summary on the right
- Trade offer panel (incoming/outgoing trade proposals)
- Pick history ticker at the bottom

### Player Actions During Draft

- **Select a player** from the board when it's your pick
- **Trade up**: Propose a trade to a team picking before you. Must include draft pick compensation.
- **Trade down**: Field or propose trades to teams picking after you.
- **Auto-pick**: Let the AI pick for you based on the board + needs.
- **Simulate to next pick**: Fast-forward through AI picks until your next selection.

### AI Draft Behavior

AI teams draft using a weighted algorithm:
- **Team Need Weight**: 40% — does this fill a hole?
- **BPA Weight**: 35% — is this the best available talent?
- **Value Weight**: 15% — is this good value at this pick number?
- **Scheme Fit**: 10% — does the prospect's archetype match the team's scheme?

AI teams may also trade up or down during the draft using the trade value chart.

### UDFA Signing

After the draft, all remaining prospects become UDFAs. The player can sign UDFAs to practice squad or minimum contracts. Competition with other teams — some UDFAs will choose other teams based on roster depth and opportunity.

### Compensatory Picks

Calculated based on net free agent losses from the previous year using a formula that considers: contract value of lost FAs vs. signed FAs, playing time of lost players.

---

## 8. Free Agency

### Free Agency Timeline

1. **Franchise Tag Window** (1 week before FA opens)
   - Apply Exclusive or Non-Exclusive Franchise Tag to 1 player
   - Apply Transition Tag to 1 player
   - Tag costs based on position averages (top 5 salaries at position)

2. **Legal Tampering Period** (3 days before FA opens)
   - Negotiate with pending FAs but can't sign
   - AI teams also negotiate; you may lose targets

3. **Free Agency Opens**
   - All unsigned players hit the market
   - Initial frenzy: top FAs sign within first 2–3 days
   - Market settles over 2–3 weeks
   - Remaining FAs available through training camp

### Free Agent Tiers

| Tier | Description | Signing Speed |
|---|---|---|
| Elite | Top 10 at position, All-Pro level | Signs day 1–2 |
| Quality Starter | Above average, proven starter | Signs week 1 |
| Solid Veteran | Reliable, not elite | Signs week 1–2 |
| Depth / Rotational | Backup or specialist | Signs week 2–4 |
| Camp Body | Low overall, long shot | Signs during camp or never |

### Negotiation System

When pursuing a free agent:

```
Negotiation Screen:
┌─────────────────────────────────────────────┐
│  SIGNING: Marcus Williams (FS, 28)          │
│  Overall: 86 | Market Value: ~$14M/yr       │
│                                              │
│  Player Priorities:                          │
│  1. 💰 Total Guaranteed Money               │
│  2. 🏆 Championship Contender               │
│  3. 📍 Preferred Location (warm weather)    │
│                                              │
│  YOUR OFFER:           COMPETING OFFERS:     │
│  Years:    [4]         Team A: 4yr/$52M     │
│  Total:    [$56M]      Team B: 3yr/$45M     │
│  Gtd:     [$38M]      (details hidden)      │
│  Bonus:    [$16M]                            │
│  Structure: [Front/Back/Even]               │
│                                              │
│  Interest Level: ██████▓░░░ 68%             │
│                                              │
│  [Submit Offer] [Withdraw] [Best Offer]      │
└─────────────────────────────────────────────┘
```

**Interest Level** is influenced by:
- Money offered (40%)
- Team competitiveness / roster quality (20%)
- Coach prestige (10%)
- Scheme fit (10%)
- Player priorities (location, role, etc.) (20%)

The player can see competing offers exist but not exact details (just team + vague range).

### Restricted Free Agents (RFA)

- Team can tender RFA at 1st round, 2nd round, or Original Round tender
- Other teams can sign the RFA but must give up the corresponding pick
- Original team has right to match any offer sheet

---

## 9. Contract System & Salary Cap

### Salary Cap Rules

- **Hard Cap**: Set per year (starts ~$255M, increases ~4-7% annually)
- **Cap Floor**: Teams must spend at least 89% of cap over a rolling 4-year period
- **Rollover**: Unused cap space rolls to the next year
- **Dead Cap**: Remaining prorated bonuses accelerate when a player is cut or traded

### Contract Components

| Component | Description |
|---|---|
| Base Salary | Annual pay, counts fully against cap that year |
| Signing Bonus | Paid upfront, prorated evenly over contract length (max 5 years) |
| Roster Bonus | Paid if player is on roster on a certain date |
| Option Bonus | Team can exercise; prorates like signing bonus |
| Incentives (LTBE) | Likely To Be Earned — counts against current cap |
| Incentives (NLTBE) | Not Likely To Be Earned — counts against next year's cap if earned |
| Void Years | Fake years added to spread bonus proration; accelerate as dead cap when they void |

### Contract Actions

1. **Sign**: New contract with a free agent
2. **Extend**: Add years/money to a player already under contract
3. **Restructure**: Convert base salary to signing bonus to create current-year cap space (pushes money to future years)
4. **Cut (Pre-June 1)**: Release player; dead cap = remaining prorated bonuses hit immediately
5. **Cut (Post-June 1)**: Dead cap split across 2 years; current year = current year's prorated amount, next year = remainder
6. **Trade**: Receiving team takes on remaining contract; trading team eats remaining prorated bonuses as dead cap
7. **Franchise Tag**: 1-year guaranteed contract at top-5 position average
8. **Transition Tag**: 1-year tender; team has right of first refusal

### Rookie Contracts

Rookie contracts are slotted based on draft position:
- **Round 1**: 4 years, fully guaranteed, 5th year team option
- **Rounds 2–7**: 4 years, partially guaranteed (year 1 fully, year 2 partial)
- **UDFA**: 3 years, minimal guarantees

Slot values scale with pick number (pick 1 gets ~$40M/4yr, pick 32 gets ~$14M/4yr, etc.).

### Cap Management UI

```
CAP OVERVIEW:
┌──────────────────────────────────────────┐
│  2025 Salary Cap:     $255,400,000       │
│  Rollover from 2024:  $12,300,000        │
│  Adjusted Cap:        $267,700,000       │
│  ──────────────────────────────────────  │
│  Active Contracts:    $231,450,000       │
│  Dead Cap:            $8,200,000         │
│  Total Committed:     $239,650,000       │
│  ──────────────────────────────────────  │
│  AVAILABLE CAP SPACE: $28,050,000        │
│  ██████████████████████░░░░░░░░░░        │
│                                          │
│  Top 5 Cap Hits:                         │
│  1. QB J. Doe      $46,800,000          │
│  2. EDGE M. Smith  $28,300,000          │
│  3. WR T. Jones    $24,100,000          │
│  4. CB R. Brown    $18,500,000          │
│  5. LT A. White    $16,200,000          │
│                                          │
│  [View All Contracts] [Cap Projections]  │
│  [Restructure Options] [Cut Candidates]  │
└──────────────────────────────────────────┘
```

**Cap Projections**: Show projected cap space for the next 3–4 years based on current contracts, estimated cap growth, and upcoming free agents.

---

## 10. Trading System

### Trade Structure

Trades can include any combination of:
- Players (with their contracts)
- Draft picks (current year + up to 3 future years)
- Conditional picks (conditions must be defined)

### Trade Value System

Use a modified version of the Jimmy Johnson draft value chart for picks:

```
Pick 1:   3000 points
Pick 5:   1700
Pick 10:  1300
Pick 15:  1050
Pick 20:  850
Pick 32:  590
Pick 33:  580 (Round 2 start)
Pick 64:  270 (Round 3 start)
Pick 100: 120 (Round 4)
Pick 135: 55  (Round 5)
Pick 170: 27  (Round 6)
Pick 210: 10  (Round 7)
```

Player trade value is calculated from:
- Overall rating (major factor)
- Age and years remaining on contract
- Position value (QB > EDGE > CB > WR > OT > ... > FB > LS)
- Contract favorability (team-friendly deals increase value)
- Development trait

### Trade Proposal Screen

```
┌────────────────────── TRADE ──────────────────────┐
│                                                    │
│   YOUR TEAM (Eagles)    ←→    OTHER TEAM (Bears)  │
│   ┌─────────────────┐       ┌──────────────────┐  │
│   │ Player A (WR)   │       │ 2025 1st Rd Pick │  │
│   │ Cap: $4.2M/yr   │       │ (projected #8)   │  │
│   │                 │       │                  │  │
│   │ 2026 3rd Rd Pick│       │ Player B (CB)    │  │
│   │                 │       │ Cap: $2.1M/yr    │  │
│   └─────────────────┘       └──────────────────┘  │
│                                                    │
│   Value Given: 1,450        Value Received: 1,520  │
│   ████████████████████      █████████████████████  │
│                                                    │
│   AI Assessment: FAIR TRADE ✓                      │
│   Cap Impact: +$2.1M this year                     │
│                                                    │
│   [Propose Trade]  [Modify]  [Cancel]              │
└────────────────────────────────────────────────────┘
```

### Trade Deadline

- Occurs at Week 8 of the regular season
- After the deadline, no trades until the new league year
- AI teams become more aggressive buyers/sellers near the deadline
- Contenders look to add; rebuilding teams look to sell veterans for picks

### AI Trade Logic

AI teams evaluate trades based on:
- Net value comparison (must be within ~10% or favoring them)
- Team needs — will they trade away a starter at a need position?
- Competitive window — contending teams value picks less, rebuilding teams value picks more
- Relationship — repeated fair deals make future trades easier; lowball offers reduce willingness

---

## 11. Staff & Coaching

### Coaching Staff Hierarchy

```
Head Coach
├── Offensive Coordinator
│   ├── QB Coach
│   ├── RB Coach
│   ├── WR Coach
│   └── OL Coach
├── Defensive Coordinator
│   ├── DL Coach
│   ├── LB Coach
│   └── DB Coach
└── Special Teams Coordinator
```

### Coaching Impact on Gameplay

| Coach Attribute | Effect |
|---|---|
| `OffenseRating` | Modifies team offensive output in simulation |
| `DefenseRating` | Modifies team defensive output in simulation |
| `GameManagement` | Affects 4th down decisions, clock management, challenge success |
| `PlayerDevelopment` | Multiplier on player progression during offseason |
| `Motivation` | Affects team morale, especially after losses |
| `Adaptability` | How well the coach adjusts scheme to available talent |
| `Recruiting` | Modifier on free agent interest in the team |

### Position Coach Effects

Position coaches provide a development bonus to their position group:
- A QB Coach with 90 `PlayerDevelopment` gives QBs a +15% progression bonus
- A bad OL Coach (50 `PlayerDevelopment`) gives OL a -10% penalty
- Position coaches also affect attribute accuracy in scouting for their position group

### Coaching Carousel

After each season:
- Teams that underperform may fire their HC (based on `OwnerPatience` vs. record)
- Fired coaches enter the coaching market
- The player (and AI teams) can hire from the available pool
- Coordinators may be promoted to HC (their ratings shift)
- New coaches are procedurally generated each year to fill the market

### Scheme Fit

Each coach has preferred schemes. When a coach's scheme matches the team's roster strengths:
- +5% simulation bonus
- Better player development in scheme-relevant attributes

When mismatched:
- -5% simulation penalty
- Players may request trades due to poor scheme fit (low morale)

---

## 12. Game Simulation Engine

### Philosophy

The player never controls on-field action. Games are simulated using a stats-driven engine that produces realistic box scores, play-by-play summaries, and outcomes.

### Simulation Flow

```
For each game:
1. Determine team power ratings
   - Aggregate player overalls weighted by position importance
   - Apply coaching modifiers
   - Apply home field advantage (+3 equivalent points)
   - Apply injury impact (missing starters)
   - Apply fatigue / bye week freshness
   - Apply weather effects (cold, rain, dome)

2. Determine game outcome
   - Use power ratings to calculate win probability
   - Add variance based on Consistency/Clutch traits
   - Determine final score using points distribution model

3. Generate individual stats
   - Distribute passing/rushing/receiving yards based on player ratings
   - Higher rated players get proportionally more production
   - Apply game script (leading team runs more, trailing team passes more)
   - Generate defensive stats (sacks, INTs, TFL)

4. Process game events
   - Injuries (check each player against injury probability)
   - Penalties
   - Turnovers
   - Big plays
   - Key moments (4th down decisions, 2-minute drill)

5. Generate summary
   - Box score
   - Player of the game
   - Key plays narrative
   - Updated standings
```

### Power Rating Formula

```csharp
float CalculateTeamPower(Team team)
{
    float power = 0;

    // Position weights (how much each position contributes to team strength)
    // QB is king
    Dictionary<Position, float> weights = new()
    {
        { Position.QB, 0.18f },
        { Position.EDGE, 0.08f },
        { Position.CB, 0.07f },
        { Position.WR, 0.07f },
        { Position.LT, 0.06f },
        { Position.RT, 0.05f },
        { Position.DT, 0.05f },
        { Position.HB, 0.05f },
        { Position.TE, 0.04f },
        { Position.FS, 0.04f },
        { Position.SS, 0.04f },
        { Position.MLB, 0.04f },
        { Position.LG, 0.03f },
        { Position.RG, 0.03f },
        { Position.C, 0.03f },
        { Position.OLB, 0.03f },
        { Position.K, 0.02f },
        { Position.P, 0.015f },
        { Position.FB, 0.01f },
        { Position.LS, 0.005f },
    };

    // Sum weighted overall of starters
    foreach (var kvp in weights)
    {
        var starter = GetStarter(team, kvp.Key);
        if (starter != null)
            power += starter.Overall * kvp.Value;
    }

    // Depth bonus (quality backups matter)
    power += CalculateDepthBonus(team) * 0.05f;

    // Coaching modifier (-5 to +5 points)
    power += GetCoachingModifier(team);

    return power;  // roughly 40–95 range
}
```

### Game Sim Presentation

The player can choose how to experience each game:

1. **Instant Sim**: Skip to final score and box score
2. **Quick Sim**: Watch a play-by-play ticker with key moments highlighted (30–60 seconds)
3. **Sim Week**: Simulate all games in the week at once, see all scores
4. **Sim to Date**: Fast-forward multiple weeks

### Box Score / Post-Game

```
┌─────────────────────────────────────────────┐
│  FINAL                    Week 6            │
│  Eagles    27  ████████████                 │
│  Cowboys   24  ███████████                  │
│                                             │
│  TEAM STATS        PHI         DAL          │
│  Total Yards       385         342          │
│  Passing           268         287          │
│  Rushing           117         55           │
│  Time of Poss.     32:14       27:46        │
│  Turnovers         1           2            │
│                                             │
│  PLAYER OF THE GAME                         │
│  ⭐ Jalen Hurts — 24/32, 268 yds, 3 TD    │
│                                             │
│  KEY PLAYS:                                 │
│  • Q2 4:32 - Hurts 42yd TD pass to Smith   │
│  • Q3 8:15 - Brown forces fumble, recovered│
│  • Q4 2:01 - Hurts scrambles for 1st down  │
│              on 3rd & 7 to seal the game    │
│                                             │
│  [Full Box Score] [Play-by-Play] [Dismiss]  │
└─────────────────────────────────────────────┘
```

---

## 13. Player Progression & Regression

### When Progression Runs

- **Major Progression**: Runs once per year during PostSeason phase
- **Minor Adjustments**: Small boosts during the season based on game performance

### Progression Factors

```csharp
int CalculateAttributeChange(Player player, string attribute)
{
    int change = 0;

    // Age curve (peak years vary by position)
    int peakAge = GetPeakAge(player.Position);  // QB:28-34, HB:24-28, WR:25-30, etc.
    int ageDelta = player.Age - peakAge;

    if (ageDelta < -2)
        change += Random(1, 4);      // young, improving
    else if (ageDelta <= 2)
        change += Random(-1, 2);     // peak, stable
    else if (ageDelta <= 5)
        change += Random(-3, 0);     // declining
    else
        change += Random(-5, -1);    // steep decline

    // Development trait modifier
    change += player.DevTrait switch
    {
        DevelopmentTrait.XFactor => 2,
        DevelopmentTrait.Superstar => 1,
        DevelopmentTrait.Star => 0,
        DevelopmentTrait.Normal => -1,
        _ => 0
    };

    // Coach development bonus
    change += GetCoachDevBonus(player);  // -2 to +3

    // Playing time bonus (starters improve faster)
    if (player.IsStarter) change += 1;

    // Trajectory modifier (set at player creation, adds long-term direction)
    change += player.TrajectoryModifier;  // -3 to +3

    return change;
}
```

### Age Curves by Position

| Position | Growth Phase | Peak Years | Decline Onset | Sharp Decline |
|---|---|---|---|---|
| QB | 22–26 | 27–34 | 35–37 | 38+ |
| HB | 22–23 | 24–27 | 28–29 | 30+ |
| WR | 22–24 | 25–30 | 31–33 | 34+ |
| TE | 22–24 | 25–30 | 31–33 | 34+ |
| OL | 22–24 | 25–32 | 33–35 | 36+ |
| EDGE | 22–24 | 25–29 | 30–32 | 33+ |
| DT | 22–24 | 25–30 | 31–33 | 34+ |
| LB | 22–24 | 25–29 | 30–32 | 33+ |
| CB | 22–24 | 25–28 | 29–31 | 32+ |
| S | 22–24 | 25–30 | 31–33 | 34+ |
| K/P | 22–24 | 25–36 | 37–40 | 41+ |

### Development Trait Changes

Players can upgrade or downgrade their development trait:
- Exceptional season (Pro Bowl, All-Pro) → chance to upgrade (Normal → Star → Superstar)
- Terrible season or major injury → chance to downgrade
- X-Factor is extremely rare and almost never gained mid-career

### Retirement

Players consider retirement based on:
- Age (higher chance as they get older)
- Overall rating dropping below a threshold
- Injury history
- Contract status (unsigned FAs more likely to retire)
- Random chance (some players just hang it up)

---

## 14. Injuries

### Injury System

- **Pre-game injury check**: Each player has a small chance of being ruled out before the game
- **In-game injuries**: During simulation, players can get injured based on position risk and `InjuryResistance` attribute
- **Severity**: Minor (1–2 weeks), Moderate (3–6 weeks), Severe (6–16 weeks), Season-ending
- **Types**: Hamstring, ACL, MCL, Ankle, Shoulder, Concussion, Broken Bone, etc.

### Injury Probability by Position

| Position | Base Injury Rate (per game) |
|---|---|
| QB | 3.5% |
| HB | 5.0% |
| WR | 3.5% |
| TE | 3.5% |
| OL | 3.0% |
| DL/EDGE | 3.5% |
| LB | 4.0% |
| CB/S | 3.5% |
| K/P | 0.5% |

Modified by:
- `InjuryResistance` attribute (high = lower chance)
- `IronMan` / `GlassBody` traits (±50%)
- Previous injuries (re-injury risk is higher)
- Fatigue level

### Injury Management

The player must manage injuries through:
- Placing players on IR (frees roster spot, minimum 4-game absence)
- Signing free agents or promoting practice squad players as replacements
- Managing the 8 IR-return designations per season
- Deciding whether to rush players back (risk of re-injury)

---

## 15. AI GM Behavior

### AI Team Management

Each AI-controlled team operates with its own "personality" and strategy:

```csharp
public class AIGMProfile
{
    public string TeamId { get; set; }
    public AIStrategy Strategy { get; set; }        // Contend, Rebuild, Retool
    public float RiskTolerance { get; set; }         // 0.0–1.0, affects trade aggression
    public float DraftPreference { get; set; }       // 0.0 = all BPA, 1.0 = all need
    public float FreeAgencyAggression { get; set; }  // spending willingness
    public float TradeFrequency { get; set; }        // how often they propose trades
    public int CompetitiveWindowYears { get; set; }  // how far ahead they plan
}

public enum AIStrategy
{
    WinNow,       // spend aggressively, trade picks for players
    Contend,      // balanced spending, keep core
    Retool,       // selective spending, target value
    Rebuild,      // accumulate picks, sign cheap vets, develop youth
    TankMode      // strip assets, maximize draft capital (rare)
}
```

### AI Decision-Making Areas

1. **Free Agency**: AI teams set budgets, prioritize positions, and bid on FAs using their team needs + scheme fit + budget
2. **Draft**: AI uses weighted BPA/Need formula (see Section 7)
3. **Trades**: AI proposes and evaluates trades weekly during trade windows
4. **Cuts**: AI cuts players to manage cap; prioritizes cutting overpaid declining players
5. **Extensions**: AI extends core players before they hit free agency
6. **Coaching**: AI fires underperforming coaches and hires from the market
7. **Depth Chart**: AI sets optimal lineups based on overall ratings

### AI Trade Proposals to Player

AI teams will occasionally propose trades to the human player:
- Frequency depends on `TradeFrequency` and whether the player has players the AI wants
- Proposals appear as notifications: "The Bears want to discuss a trade involving your 2025 1st round pick"
- Player can accept, counter, or decline

---

## 16. UI/UX Design

### Navigation Structure

```
Top Nav Bar (persistent):
[Dashboard] [Roster] [Scouting] [Draft] [Free Agency] [Trades] [Staff] [League]

Right Side:
[Notifications Bell 🔔]  [Cap Space: $28.1M]  [Record: 8-3]  [Advance ▶]
```

### Color Coding Conventions

| Color | Meaning |
|---|---|
| Gold/Yellow | Elite (90+ overall) |
| Green | Good (80–89) |
| Blue | Average (70–79) |
| Orange | Below Average (60–69) |
| Red | Poor (<60) |
| Purple | Development Trait: Superstar/X-Factor |

### Key UI Principles

1. **Card-Based Layout**: Players, coaches, and prospects are always represented as "cards" with key info visible at a glance
2. **Drill-Down**: Click any card to open the full detail view
3. **Contextual Actions**: Right-click or action menu on any entity for relevant actions
4. **Notifications**: Important events (trade offers, injury reports, FA signings) appear as toast notifications and collect in a notification center
5. **Tooltips**: Hover over any stat, rating, or term for an explanation
6. **Comparison Tool**: Select 2–4 players/prospects to compare side-by-side anywhere in the app
7. **Search**: Global search bar to find any player, coach, team, or prospect by name

### Dashboard (Home Screen)

The dashboard is the player's command center:

```
┌──────────────────────────────────────────────────────────────┐
│  PHILADELPHIA EAGLES          Season 2025 | Week 12          │
│  Record: 8-3 | NFC East 1st | Cap Space: $28.1M            │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─ UPCOMING ────────┐  ┌─ RECENT RESULTS ────────────────┐ │
│  │ Week 12: @ DAL    │  │ Week 11: PHI 31 - WAS 17  ✓   │ │
│  │ Sun 4:25 PM       │  │ Week 10: PHI 24 - NYG 20  ✓   │ │
│  │ Line: PHI -3.5    │  │ Week 9:  BYE                   │ │
│  └───────────────────┘  └─────────────────────────────────┘ │
│                                                              │
│  ┌─ TEAM NEEDS ──────┐  ┌─ NOTIFICATIONS ────────────────┐ │
│  │ 1. CB  (Urgent)   │  │ 🔔 CHI wants to trade for      │ │
│  │ 2. DT  (Moderate) │  │    your 2025 1st                │ │
│  │ 3. WR  (Depth)    │  │ 🏥 J. Smith (HB) - Hamstring  │ │
│  │ 4. OLB (Moderate) │  │    Out 2-3 weeks               │ │
│  └───────────────────┘  │ 📋 Waiver claim available       │ │
│                          └─────────────────────────────────┘ │
│  ┌─ CAP SNAPSHOT ──────────────────────────────────────────┐ │
│  │ Cap: $255.4M | Used: $227.3M | Space: $28.1M           │ │
│  │ ████████████████████████████████████████░░░░░░░          │ │
│  │ 2026 Projected: $42.8M available                        │ │
│  └─────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌─ ROSTER ALERTS ─────────────────────────────────────────┐ │
│  │ 🔴 3 players in contract year                           │ │
│  │ 🟡 QB B. Jones approaching decline age                  │ │
│  │ 🟢 EDGE R. Davis having breakout season (+4 OVR)       │ │
│  └─────────────────────────────────────────────────────────┘ │
│                                                              │
│            [Advance to Next Week ▶]                          │
└──────────────────────────────────────────────────────────────┘
```

---

## 17. Save/Load System

### Save Data Structure

Save files are serialized as JSON and contain:

```csharp
public class SaveData
{
    // Meta
    public string SaveId { get; set; }
    public string SaveName { get; set; }
    public DateTime SaveDate { get; set; }
    public string GameVersion { get; set; }

    // Game State
    public int CurrentYear { get; set; }
    public GamePhase CurrentPhase { get; set; }
    public int CurrentWeek { get; set; }
    public string PlayerTeamId { get; set; }

    // All Entities
    public List<Team> Teams { get; set; }
    public List<Player> Players { get; set; }
    public List<Coach> Coaches { get; set; }
    public List<Scout> Scouts { get; set; }
    public List<Prospect> CurrentDraftClass { get; set; }
    public List<DraftPick> AllDraftPicks { get; set; }

    // History
    public List<Season> SeasonHistory { get; set; }
    public List<TransactionRecord> TransactionLog { get; set; }

    // Calendar
    public CalendarState CalendarState { get; set; }

    // AI State
    public Dictionary<string, AIGMProfile> AIProfiles { get; set; }

    // Player's Scouting State
    public Dictionary<string, float> ScoutingProgress { get; set; }
    public List<string> DraftBoardOrder { get; set; }
}
```

### Save Slots

- 10 save slots
- Auto-save at the start of each new phase
- Manual save at any time
- Save file location: `user://saves/`

---

## 18. Data Seeding & League Generation

### Initial League Setup

On "New Game," the system generates:

1. **32 NFL Teams** — loaded from `teams.json` with real city/name/division/conference/colors
2. **~1,700 Players** — procedurally generated to fill all 32 rosters (53 active + 16 PS each)
3. **Coaching Staffs** — one full staff per team (HC, OC, DC, STC, 7 position coaches)
4. **Scouting Staffs** — 4–8 scouts per team
5. **3 Years of Draft Picks** — distributed across all teams
6. **Initial Contracts** — all players start with semi-random contracts appropriate to their overall/age/position

### Player Generation

```csharp
Player GeneratePlayer(Position position, int targetOverall, int age)
{
    // 1. Pick name from name pools
    // 2. Pick college from college pool (weighted by football program size)
    // 3. Generate archetype appropriate for position
    // 4. Generate attributes:
    //    - Start with archetype template (base distribution)
    //    - Add randomness (±5-10 per attribute)
    //    - Scale to hit target overall
    // 5. Generate traits based on archetype and randomness
    // 6. Assign development trait:
    //    - 60% Normal, 25% Star, 10% Superstar, 5% X-Factor (weighted by overall)
    // 7. Generate physical profile (height, weight) based on position norms
    // 8. Assign trajectory modifier based on age and potential
}
```

### Draft Class Generation

Each year, generate ~450 prospects:

- Distribution by position mirrors real NFL draft trends
- Overall range: 55–95 (bell curve centered around 68)
- Top 10 prospects are 78–95 overall
- Round 1 projected prospects: 75–95
- Late round prospects: 55–72
- Each class has 2–4 "generational" prospects (90+ potential)
- Red flags and busts are randomly assigned (some high-rated prospects will underperform)

### Roster Composition Targets

Each generated team should roughly have:
- 3 QBs, 4 HBs, 1 FB, 6 WRs, 3 TEs, 9 OL (distributed across positions)
- 4–5 EDGEs, 3–4 DTs, 4 LBs (MLB+OLB), 5–6 CBs, 4 Safeties
- 1 K, 1 P, 1 LS
- Mix of ages: ~40% age 22–25, ~35% age 26–29, ~20% age 30–33, ~5% age 34+
- Overall distribution: 2–3 elite (88+), 8–10 quality (80–87), 15–20 average (70–79), rest below

---

## 19. Implementation Phases

### Phase 1: Foundation (Weeks 1–3)

**Goal**: Core data models, basic game loop, minimal UI

- [ ] Set up Godot project with .NET solution
- [ ] Implement all data models (Player, Team, Contract, Coach, etc.)
- [ ] Implement `GameManager` singleton and `EventBus`
- [ ] Implement `CalendarSystem` with phase transitions
- [ ] Create `teams.json` with all 32 NFL teams
- [ ] Build player generation system
- [ ] Build basic team generation (fill rosters)
- [ ] Create MainMenu scene
- [ ] Create NewGameSetup scene (pick your team)
- [ ] Create basic Dashboard scene (shows team name, record, phase)
- [ ] Implement save/load system (JSON serialization)

### Phase 2: Roster Management (Weeks 4–5)

**Goal**: View and manage your roster, Madden-style depth chart

- [ ] Build RosterView scene (list of all players with sorting/filtering)
- [ ] Build PlayerCard popup (full player detail)
- [ ] Build DepthChart scene (visual grid with drag-drop)
- [ ] Implement depth chart auto-set (by overall rating)
- [ ] Implement roster actions: Cut, Move to IR, Move to PS, Promote from PS
- [ ] Build cap space overview UI
- [ ] Implement `SalaryCapManager` (track cap hits, dead cap)

### Phase 3: Game Simulation (Weeks 6–8)

**Goal**: Simulate games and produce meaningful results

- [ ] Implement `SimulationEngine` (team power ratings, game outcome calculation)
- [ ] Implement stat generation (passing, rushing, receiving, defensive stats)
- [ ] Implement `ScheduleGenerator` (17-game schedule with byes, divisional matchups)
- [ ] Build WeekSchedule scene (see this week's matchups)
- [ ] Build GameSimView (play-by-play ticker or instant result)
- [ ] Build PostGameReport (box score, player of the game)
- [ ] Build Standings scene (division standings, playoff picture)
- [ ] Implement `InjurySystem` (in-game injuries, recovery timelines)
- [ ] Implement playoff bracket and Super Bowl

### Phase 4: Free Agency (Weeks 9–10)

**Goal**: Full free agent signing experience

- [ ] Implement free agent market generation (players whose contracts expire)
- [ ] Build FreeAgentMarket scene (sortable/filterable list of FAs)
- [ ] Build NegotiationScreen (offer construction, interest meter)
- [ ] Implement AI free agent decision-making (where they sign)
- [ ] Implement franchise tag and transition tag
- [ ] Implement restricted free agency
- [ ] Implement contract signing (create contract from offer terms)
- [ ] Build contract extension flow for existing players
- [ ] Implement contract restructuring
- [ ] Implement compensatory pick formula

### Phase 5: Scouting & Draft (Weeks 11–14)

**Goal**: Full scouting system and draft day experience

- [ ] Implement draft class generation (~450 prospects per year)
- [ ] Implement `ScoutingSystem` (assign scouts, progress over time, reveal attributes)
- [ ] Build ScoutingHub scene (manage scouts, view prospect list)
- [ ] Build ProspectCard (scouted vs. unscouted data display)
- [ ] Build CombineView (combine results table)
- [ ] Build DraftBoard (personal ranking, drag-drop, tagging)
- [ ] Implement draft order determination
- [ ] Build DraftRoom scene (live draft experience, pick timer, trade proposals)
- [ ] Implement AI drafting logic
- [ ] Implement draft-day trades
- [ ] Implement UDFA signing period
- [ ] Implement rookie contract slot system

### Phase 6: Trading (Weeks 15–16)

**Goal**: Trade with AI teams anytime during trade windows

- [ ] Implement `TradeSystem` with value calculations
- [ ] Build TradeHub scene (see available trade partners)
- [ ] Build TradeProposal scene (construct trade, see value comparison)
- [ ] Build TradeBlock scene (mark players available, see interest)
- [ ] Implement AI trade proposals (incoming offers)
- [ ] Implement trade deadline logic
- [ ] Implement conditional pick trading
- [ ] Implement trade history log

### Phase 7: Staff Management (Weeks 17–18)

**Goal**: Hire, fire, and manage coaching staff

- [ ] Implement coaching market generation
- [ ] Build StaffOverview scene (see your current staff)
- [ ] Build CoachCard popup (coach ratings, record)
- [ ] Build HiringScreen (available coaches, interviews)
- [ ] Implement coaching impact on simulation
- [ ] Implement coaching impact on player development
- [ ] Implement coaching carousel (AI firings/hirings)
- [ ] Implement scheme system (offensive/defensive schemes affect performance)

### Phase 8: Progression & AI (Weeks 19–21)

**Goal**: Living, breathing league that evolves year to year

- [ ] Implement `ProgressionSystem` (offseason attribute changes, age curves)
- [ ] Implement retirement system
- [ ] Implement `AIGMController` (AI roster management, signings, cuts)
- [ ] Implement AI coaching decisions (scheme changes, starter decisions)
- [ ] Implement owner patience and job security for player's GM role
- [ ] Implement fan satisfaction
- [ ] Implement season awards (MVP, DPOY, OROY, DROY, All-Pro, Pro Bowl)
- [ ] Implement Hall of Fame tracking (career milestones)

### Phase 9: Polish & Features (Weeks 22–24)

**Goal**: Quality of life, juice, and depth

- [ ] Transaction log (full history of all league moves)
- [ ] League leaders (statistical leaderboards)
- [ ] Team history (past seasons, records, draft picks)
- [ ] Player comparison tool
- [ ] Advanced cap projections (future year estimates)
- [ ] Tutorial / onboarding flow
- [ ] Settings (sim speed, auto-save frequency, notification preferences)
- [ ] Sound design (UI clicks, draft pick sounds, notification chimes)
- [ ] Achievement system (rebuild a team, win 3 Super Bowls, etc.)
- [ ] Bug fixes, balance tuning, playtesting

---

## 20. File Manifest

Complete list of files to create, organized by priority:

### Data Files
```
Resources/Data/teams.json              — 32 NFL teams with full metadata
Resources/Data/firstnames.json         — 500+ first names
Resources/Data/lastnames.json          — 500+ last names
Resources/Data/colleges.json           — 130+ college football programs
Resources/Data/coach_names.json        — name pools for coaches/scouts
Resources/Data/archetypes.json         — attribute templates per archetype
Resources/Data/salary_cap_rules.json   — cap amounts, tag costs, min salaries by year
Resources/Data/draft_value_chart.json  — trade value chart for picks
Resources/Data/injury_types.json       — injury definitions and recovery ranges
```

### Core Scripts
```
Scripts/Core/GameManager.cs
Scripts/Core/CalendarSystem.cs
Scripts/Core/SaveLoadManager.cs
Scripts/Core/EventBus.cs
```

### Model Scripts
```
Scripts/Models/Player.cs
Scripts/Models/PlayerAttributes.cs
Scripts/Models/PlayerTraits.cs
Scripts/Models/Contract.cs
Scripts/Models/ContractYear.cs
Scripts/Models/Team.cs
Scripts/Models/TeamRecord.cs
Scripts/Models/DepthChart.cs
Scripts/Models/Coach.cs
Scripts/Models/Scout.cs
Scripts/Models/Prospect.cs
Scripts/Models/CombineResults.cs
Scripts/Models/DraftPick.cs
Scripts/Models/Game.cs
Scripts/Models/Season.cs
Scripts/Models/Injury.cs
Scripts/Models/SeasonStats.cs
Scripts/Models/TransactionRecord.cs
Scripts/Models/AIGMProfile.cs
Scripts/Models/SaveData.cs
Scripts/Models/Enums/Position.cs
Scripts/Models/Enums/Archetype.cs
Scripts/Models/Enums/Phase.cs
Scripts/Models/Enums/Conference.cs
Scripts/Models/Enums/Division.cs
Scripts/Models/Enums/SchemeType.cs
Scripts/Models/Enums/CoachRole.cs
Scripts/Models/Enums/RosterStatus.cs
Scripts/Models/Enums/DevelopmentTrait.cs
Scripts/Models/Enums/ContractType.cs
Scripts/Models/Enums/ScoutingGrade.cs
```

### System Scripts
```
Scripts/Systems/SimulationEngine.cs
Scripts/Systems/DraftSystem.cs
Scripts/Systems/FreeAgencySystem.cs
Scripts/Systems/TradeSystem.cs
Scripts/Systems/ScoutingSystem.cs
Scripts/Systems/SalaryCapManager.cs
Scripts/Systems/StaffSystem.cs
Scripts/Systems/ProgressionSystem.cs
Scripts/Systems/InjurySystem.cs
Scripts/Systems/ScheduleGenerator.cs
Scripts/Systems/AIGMController.cs
Scripts/Systems/PlayerGenerator.cs
Scripts/Systems/ProspectGenerator.cs
Scripts/Systems/ContractGenerator.cs
Scripts/Systems/OverallCalculator.cs
Scripts/Systems/TradeValueCalculator.cs
Scripts/Systems/CompensatoryPickCalculator.cs
```

### UI Scripts
```
Scripts/UI/PlayerCardUI.cs
Scripts/UI/DepthChartUI.cs
Scripts/UI/DraftBoardUI.cs
Scripts/UI/ContractNegotiationUI.cs
Scripts/UI/TradeProposalUI.cs
Scripts/UI/FreeAgentListUI.cs
Scripts/UI/StandingsUI.cs
Scripts/UI/Components/AttributeBar.cs
Scripts/UI/Components/OverallBadge.cs
Scripts/UI/Components/CapSpaceIndicator.cs
Scripts/UI/Components/DraftPickChip.cs
Scripts/UI/Components/PlayerCardMini.cs
Scripts/UI/Components/NotificationToast.cs
Scripts/UI/Components/PhaseIndicator.cs
Scripts/UI/Components/SearchBar.cs
```

---

## Appendix A: Overall Rating Calculation

Overall is a weighted average of relevant attributes per position:

```csharp
// Example: QB Overall
int CalculateQBOverall(PlayerAttributes a)
{
    return (int)(
        a.ThrowPower * 0.12 +
        a.ShortAccuracy * 0.14 +
        a.MediumAccuracy * 0.14 +
        a.DeepAccuracy * 0.10 +
        a.ThrowOnRun * 0.08 +
        a.PlayAction * 0.05 +
        a.Speed * 0.04 +
        a.Acceleration * 0.03 +
        a.Awareness * 0.12 +
        a.Clutch * 0.05 +
        a.Elusiveness * 0.03 +
        a.Carrying * 0.02 +
        a.Stamina * 0.03 +
        a.Toughness * 0.03 +
        a.InjuryResistance * 0.02
    );
}

// Each position has its own weight formula
// e.g., HB weights Speed, Carrying, BallCarrierVision, BreakTackle heavily
// e.g., CB weights ManCoverage, ZoneCoverage, Speed, Press, PlayRecognition
```

## Appendix B: Position-Specific Stat Tracking

```csharp
public class SeasonStats
{
    public int Season { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesStarted { get; set; }

    // Passing
    public int Completions { get; set; }
    public int Attempts { get; set; }
    public int PassingYards { get; set; }
    public int PassingTDs { get; set; }
    public int Interceptions { get; set; }
    public float PasserRating { get; set; }
    public int Sacked { get; set; }

    // Rushing
    public int RushAttempts { get; set; }
    public int RushingYards { get; set; }
    public int RushingTDs { get; set; }
    public int Fumbles { get; set; }
    public int FumblesLost { get; set; }

    // Receiving
    public int Targets { get; set; }
    public int Receptions { get; set; }
    public int ReceivingYards { get; set; }
    public int ReceivingTDs { get; set; }
    public int Drops { get; set; }

    // Defense
    public int TotalTackles { get; set; }
    public int SoloTackles { get; set; }
    public int AssistedTackles { get; set; }
    public float Sacks { get; set; }
    public int TacklesForLoss { get; set; }
    public int QBHits { get; set; }
    public int ForcedFumbles { get; set; }
    public int FumbleRecoveries { get; set; }
    public int InterceptionsDef { get; set; }
    public int PassesDefended { get; set; }
    public int DefensiveTDs { get; set; }
    public int Safeties { get; set; }

    // Kicking
    public int FGMade { get; set; }
    public int FGAttempted { get; set; }
    public int FGLong { get; set; }
    public int XPMade { get; set; }
    public int XPAttempted { get; set; }

    // Punting
    public int Punts { get; set; }
    public float PuntAverage { get; set; }
    public int PuntsInside20 { get; set; }
    public int Touchbacks { get; set; }

    // Return
    public int KickReturns { get; set; }
    public int KickReturnYards { get; set; }
    public int KickReturnTDs { get; set; }
    public int PuntReturns { get; set; }
    public int PuntReturnYards { get; set; }
    public int PuntReturnTDs { get; set; }

    // Snap Counts
    public int OffensiveSnaps { get; set; }
    public int DefensiveSnaps { get; set; }
    public int SpecialTeamsSnaps { get; set; }
}
```

## Appendix C: Event Bus Signals

```csharp
// EventBus.cs — global signals for decoupled communication
public partial class EventBus : Node
{
    // Calendar
    [Signal] public delegate void PhaseChangedEventHandler(int phase);
    [Signal] public delegate void WeekAdvancedEventHandler(int year, int week);
    [Signal] public delegate void SeasonStartedEventHandler(int year);
    [Signal] public delegate void SeasonEndedEventHandler(int year);

    // Roster
    [Signal] public delegate void PlayerSignedEventHandler(string playerId, string teamId);
    [Signal] public delegate void PlayerCutEventHandler(string playerId, string teamId);
    [Signal] public delegate void PlayerTradedEventHandler(string playerId, string fromTeamId, string toTeamId);
    [Signal] public delegate void PlayerInjuredEventHandler(string playerId, string injuryType, int weeksOut);
    [Signal] public delegate void PlayerRetiredEventHandler(string playerId);
    [Signal] public delegate void DepthChartChangedEventHandler(string teamId);

    // Draft
    [Signal] public delegate void DraftPickMadeEventHandler(int round, int pick, string prospectId, string teamId);
    [Signal] public delegate void DraftStartedEventHandler(int year);
    [Signal] public delegate void DraftCompletedEventHandler(int year);

    // Free Agency
    [Signal] public delegate void FreeAgencyOpenedEventHandler(int year);
    [Signal] public delegate void FranchiseTagAppliedEventHandler(string playerId, string teamId);

    // Trades
    [Signal] public delegate void TradeProposedEventHandler(string fromTeamId, string toTeamId);
    [Signal] public delegate void TradeAcceptedEventHandler(string tradeId);
    [Signal] public delegate void TradeRejectedEventHandler(string tradeId);

    // Staff
    [Signal] public delegate void CoachHiredEventHandler(string coachId, string teamId, int role);
    [Signal] public delegate void CoachFiredEventHandler(string coachId, string teamId);

    // Game
    [Signal] public delegate void GameCompletedEventHandler(string gameId);
    [Signal] public delegate void PlayoffTeamsSetEventHandler();
    [Signal] public delegate void SuperBowlCompletedEventHandler(string winnerTeamId);

    // UI
    [Signal] public delegate void NotificationCreatedEventHandler(string title, string message, int priority);
    [Signal] public delegate void PlayerSelectedEventHandler(string playerId);
    [Signal] public delegate void TeamSelectedEventHandler(string teamId);
}
```

---

## Final Notes for Claude Code Implementation

- **Start with Phase 1**. Get the data models and game loop working before any UI. Test by logging to console.
- **Use Godot Resources** where possible for in-memory state, but serialize to JSON for saves.
- **Keep simulation deterministic** when given a seed — this allows for reproducible testing.
- **All monetary values in cents** (long) to avoid floating point issues. Display as dollars with formatting.
- **Player IDs are GUIDs** — never reference players by index or name.
- **The UI is secondary** to the systems. Build systems first, wire UI after.
- **Test with small datasets** (4 teams, 20 players each) before scaling to full 32-team league.
- **Every system should be independently testable** — the `SimulationEngine` should work without any UI scene loaded.
