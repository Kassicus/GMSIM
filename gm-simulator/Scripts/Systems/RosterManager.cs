using GMSimulator.Core;
using GMSimulator.Models;
using GMSimulator.Models.Enums;

namespace GMSimulator.Systems;

/// <summary>
/// Orchestrates roster transactions. Plain C# class owned by GameManager.
/// Each action: validates → mutates → recalculates cap → logs → emits signal.
/// </summary>
public class RosterManager
{
    private readonly Func<List<Team>> _getTeams;
    private readonly Func<List<Player>> _getPlayers;
    private readonly Func<List<TransactionRecord>> _getTransactionLog;
    private readonly SalaryCapManager _capManager;
    private readonly Func<CalendarSystem> _getCalendar;

    public RosterManager(
        Func<List<Team>> getTeams,
        Func<List<Player>> getPlayers,
        Func<List<TransactionRecord>> getTransactionLog,
        SalaryCapManager capManager,
        Func<CalendarSystem> getCalendar)
    {
        _getTeams = getTeams;
        _getPlayers = getPlayers;
        _getTransactionLog = getTransactionLog;
        _capManager = capManager;
        _getCalendar = getCalendar;
    }

    // --- Cut Player ---

    public (bool Success, string Message) CutPlayer(string playerId, string teamId)
    {
        var team = _getTeams().FirstOrDefault(t => t.Id == teamId);
        var player = _getPlayers().FirstOrDefault(p => p.Id == playerId);
        if (team == null || player == null)
            return (false, "Team or player not found.");
        if (player.TeamId != teamId)
            return (false, "Player does not belong to this team.");

        var calendar = _getCalendar();
        bool postJune1 = _capManager.IsPostJune1(calendar.CurrentPhase);

        // Calculate dead cap
        var (thisYearDead, nextYearDead) = _capManager.CalculateCutDeadCap(player, calendar.CurrentYear, postJune1);

        // Remove from whichever roster list they're on
        team.PlayerIds.Remove(playerId);
        team.PracticeSquadIds.Remove(playerId);
        team.IRPlayerIds.Remove(playerId);

        // Remove from depth chart
        RemoveFromDepthChart(team, playerId);

        // Apply dead cap
        team.DeadCapTotal += thisYearDead;

        // Update player status
        player.TeamId = null;
        player.RosterStatus = RosterStatus.FreeAgent;
        player.CurrentContract = null;

        // Recalculate cap
        _capManager.RecalculateTeamCap(team, _getPlayers(), calendar.CurrentYear);

        // Log transaction
        LogTransaction(TransactionType.Cut, playerId, teamId, calendar,
            $"Released {player.FullName} ({player.Position}). Dead cap: {thisYearDead / 100m:C0}");

        // Emit signal
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerCut, playerId, teamId);

        return (true, $"{player.FullName} has been released.");
    }

    // --- Move to IR ---

    public (bool Success, string Message) MoveToIR(string playerId, string teamId)
    {
        var team = _getTeams().FirstOrDefault(t => t.Id == teamId);
        var player = _getPlayers().FirstOrDefault(p => p.Id == playerId);
        if (team == null || player == null)
            return (false, "Team or player not found.");
        if (player.TeamId != teamId)
            return (false, "Player does not belong to this team.");
        if (player.RosterStatus != RosterStatus.Active53)
            return (false, "Player must be on active roster to move to IR.");

        // Move from active to IR
        team.PlayerIds.Remove(playerId);
        team.IRPlayerIds.Add(playerId);
        player.RosterStatus = RosterStatus.InjuredReserve;

        // Remove from depth chart
        RemoveFromDepthChart(team, playerId);

        // Cap does NOT change (IR players still count against cap)
        var calendar = _getCalendar();

        // Log transaction
        LogTransaction(TransactionType.Demoted, playerId, teamId, calendar,
            $"Placed {player.FullName} ({player.Position}) on Injured Reserve.");

        // Emit signal
        EventBus.Instance?.EmitSignal(EventBus.SignalName.DepthChartChanged, teamId);

        return (true, $"{player.FullName} placed on Injured Reserve.");
    }

    // --- Move to Practice Squad ---

    public (bool Success, string Message) MoveToPracticeSquad(string playerId, string teamId)
    {
        var team = _getTeams().FirstOrDefault(t => t.Id == teamId);
        var player = _getPlayers().FirstOrDefault(p => p.Id == playerId);
        if (team == null || player == null)
            return (false, "Team or player not found.");
        if (player.TeamId != teamId)
            return (false, "Player does not belong to this team.");
        if (player.RosterStatus != RosterStatus.Active53)
            return (false, "Player must be on active roster to move to practice squad.");
        if (team.PracticeSquadIds.Count >= _capManager.PracticeSquadSize)
            return (false, $"Practice squad is full ({_capManager.PracticeSquadSize}/{_capManager.PracticeSquadSize}).");

        // Check PS eligibility (simplified: 2 or fewer years in league, or veteran exception)
        int veteransOnPS = 0;
        foreach (var psId in team.PracticeSquadIds)
        {
            var psPlayer = _getPlayers().FirstOrDefault(p => p.Id == psId);
            if (psPlayer != null && psPlayer.YearsInLeague > 2)
                veteransOnPS++;
        }
        bool isEligible = player.YearsInLeague <= 2 || veteransOnPS < _capManager.PracticeSquadVeteranSlots;
        if (!isEligible)
            return (false, "Player is not eligible for practice squad (too many accrued seasons, veteran slots full).");

        var calendar = _getCalendar();

        // Move from active to PS
        team.PlayerIds.Remove(playerId);
        team.PracticeSquadIds.Add(playerId);
        player.RosterStatus = RosterStatus.PracticeSquad;

        // Replace contract with PS contract
        player.CurrentContract = ContractGenerator.GeneratePracticeSquadContract(
            calendar.CurrentYear, playerId, teamId);

        // Remove from depth chart
        RemoveFromDepthChart(team, playerId);

        // Recalculate cap (PS contract is much cheaper)
        _capManager.RecalculateTeamCap(team, _getPlayers(), calendar.CurrentYear);

        // Log transaction
        LogTransaction(TransactionType.Demoted, playerId, teamId, calendar,
            $"Moved {player.FullName} ({player.Position}) to Practice Squad.");

        // Emit signal
        EventBus.Instance?.EmitSignal(EventBus.SignalName.DepthChartChanged, teamId);

        return (true, $"{player.FullName} moved to Practice Squad.");
    }

    // --- Promote from Practice Squad ---

    public (bool Success, string Message) PromoteFromPracticeSquad(string playerId, string teamId)
    {
        var team = _getTeams().FirstOrDefault(t => t.Id == teamId);
        var player = _getPlayers().FirstOrDefault(p => p.Id == playerId);
        if (team == null || player == null)
            return (false, "Team or player not found.");
        if (player.TeamId != teamId)
            return (false, "Player does not belong to this team.");
        if (player.RosterStatus != RosterStatus.PracticeSquad)
            return (false, "Player must be on practice squad to promote.");
        if (team.PlayerIds.Count >= _capManager.ActiveRosterSize)
            return (false, $"Active roster is full ({_capManager.ActiveRosterSize}/{_capManager.ActiveRosterSize}).");

        var calendar = _getCalendar();

        // Move from PS to active
        team.PracticeSquadIds.Remove(playerId);
        team.PlayerIds.Add(playerId);
        player.RosterStatus = RosterStatus.Active53;

        // Replace contract with minimum salary
        player.CurrentContract = ContractGenerator.GenerateMinimumContract(
            player.YearsInLeague, calendar.CurrentYear, playerId, teamId);

        // Add to end of depth chart for their position
        if (team.DepthChart.Chart.TryGetValue(player.Position, out var depthList))
            depthList.Add(playerId);
        else
            team.DepthChart.Chart[player.Position] = new List<string> { playerId };

        // Recalculate cap
        _capManager.RecalculateTeamCap(team, _getPlayers(), calendar.CurrentYear);

        // Log transaction
        LogTransaction(TransactionType.Promoted, playerId, teamId, calendar,
            $"Promoted {player.FullName} ({player.Position}) from Practice Squad.");

        // Emit signal
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerSigned, playerId, teamId);

        return (true, $"{player.FullName} promoted to active roster.");
    }

    // --- Depth Chart Operations ---

    public void AutoSetDepthChart(Team team, List<Player> teamActivePlayers)
    {
        team.DepthChart.Chart.Clear();

        foreach (Position pos in Enum.GetValues<Position>())
        {
            var playersAtPos = teamActivePlayers
                .Where(p => p.Position == pos && p.RosterStatus == RosterStatus.Active53)
                .OrderByDescending(p => p.Overall)
                .Select(p => p.Id)
                .ToList();

            if (playersAtPos.Count > 0)
                team.DepthChart.Chart[pos] = playersAtPos;
        }

        EventBus.Instance?.EmitSignal(EventBus.SignalName.DepthChartChanged, team.Id);
    }

    public (bool Success, string Message) SwapDepthChart(Team team, Position position, int indexA, int indexB)
    {
        if (!team.DepthChart.Chart.TryGetValue(position, out var depthList))
            return (false, "Position not found in depth chart.");
        if (indexA < 0 || indexA >= depthList.Count || indexB < 0 || indexB >= depthList.Count)
            return (false, "Invalid depth chart index.");

        (depthList[indexA], depthList[indexB]) = (depthList[indexB], depthList[indexA]);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.DepthChartChanged, team.Id);
        return (true, "Depth chart updated.");
    }

    // --- Helpers ---

    private void RemoveFromDepthChart(Team team, string playerId)
    {
        foreach (var kvp in team.DepthChart.Chart)
        {
            kvp.Value.Remove(playerId);
        }
    }

    private void LogTransaction(TransactionType type, string playerId, string teamId,
        CalendarSystem calendar, string description)
    {
        _getTransactionLog().Add(new TransactionRecord
        {
            Type = type,
            PlayerId = playerId,
            TeamId = teamId,
            Description = description,
            Year = calendar.CurrentYear,
            Week = calendar.CurrentWeek,
            Phase = calendar.CurrentPhase,
        });
    }
}
