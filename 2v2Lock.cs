using DeadworksManaged.Api;

namespace _2v2Lock;

public partial class _2v2Lock : DeadworksPluginBase
{
    public override string Name => "2v2Lock";

    public override void OnPrecacheResources()
    {
        foreach (var hero in GetAvailableHeroes())
            Precache.AddHero(hero);
    }

    public override void OnLoad(bool isReload)
    {
        LoadConfig();
        Console.WriteLine(isReload ? "[2v2Lock] Reloaded!" : "[2v2Lock] Loaded!");
    }

    public override void OnUnload()
    {
        _mapResetTimer?.Cancel();
        Console.WriteLine("[2v2Lock] Unloaded!");
    }

    public override void OnStartupServer()
    {
        LoadConfig();
        _mapResetTimer?.Cancel();
        _mapResetTimer = null;
        RestorePendingRoundSnapshotIfNeeded();

        if (_pendingRoundReload && _activeLane != null && _pendingRoundParticipantsBySlot.Count > 0)
        {
            _phase = MatchPhase.Starting;
            _roundRespawnedSlots.Clear();
            ApplyRoundConVars(_activeLane);
            Console.WriteLine($"[2v2Lock] Startup on map {Server.MapName} with pending round on lane {GetActiveLaneDisplayName()}");
            return;
        }

        ResetStateForNewMap();
        ApplyLobbyConVars();
        Console.WriteLine($"[2v2Lock] Startup on map {Server.MapName}");
    }

    public override void OnEntityDeleted(EntityDeletedEvent args)
    {
        var entity = args.Entity;
        var designerName = entity.DesignerName;
        if (!IsObjectiveDebugEntity(designerName))
            return;

        var entityName = string.IsNullOrWhiteSpace(entity.Name) ? "(sem name)" : entity.Name;
        var isWinningObjective = IsActiveLaneTier1Objective(entity);
        var debugText = $"[2v2Lock] Objective destroyed: {designerName} | {entityName} | team {entity.TeamNum}" +
                        (isWinningObjective ? " | ACTIVE_LANE_T1" : "");

        Console.WriteLine(debugText);

        if (!IsRoundObjectivesEnabled() || !isWinningObjective)
            return;

        var winningTeam = ResolveWinningTeamFromObjective(entity);
        Timer.NextTick(() => FinishRound(winningTeam));
    }
}
