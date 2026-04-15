using DeadworksManaged.Api;

namespace _2v2Lock;

public partial class _2v2Lock
{
    public override void OnClientFullConnect(ClientFullConnectEvent args)
    {
        var controller = args.Controller;
        if (controller == null)
            return;

        if (HandlePendingRoundConnect(args.Slot, controller))
            return;

        if (_phase == MatchPhase.Lobby)
            ApplyLobbyConVars();

        InitializeLobbyPlayer(args.Slot, controller);
    }

    public override void OnClientDisconnect(ClientDisconnectedEvent args)
    {
        _readySlots.Remove(args.Slot);

        if (_pendingRoundReload || _phase == MatchPhase.Resetting)
            return;

        Timer.NextTick(() =>
        {
            if (_phase == MatchPhase.Lobby && GetLockedPlayerCount() < RequiredPlayers)
                BroadcastChat($"[2v2Lock] A player left. Lobby now has {GetLockedPlayerCount()}/{RequiredPlayers} players.");

            if (GetLockedPlayerCount() < RequiredPlayers)
                CancelRoundBecauseRosterChanged();
        });

        var controller = args.Controller;
        if (controller == null)
            return;

        controller.GetHeroPawn()?.Remove();
        controller.Remove();
    }

    public override HookResult OnClientConCommand(ClientConCommandEvent e)
    {
        if (e.Command is "changeteam" or "jointeam")
        {
            if (e.Controller != null && TryGetSlot(e.Controller, out var slot))
                SendChat(slot, "[2v2Lock] Teams are managed automatically by the plugin.");
            return HookResult.Stop;
        }

        if (IsHeroChangeCommand(e.Command))
        {
            if (IsHeroChangeLocked())
            {
                if (e.Controller != null && TryGetSlot(e.Controller, out var slot))
                    SendChat(slot, "[2v2Lock] Do not change hero during the round.");
                return HookResult.Stop;
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler("player_hero_changed")]
    public HookResult OnHeroChanged(PlayerHeroChangedEvent args)
    {
        var pawn = args.Userid?.As<CCitadelPlayerPawn>();
        if (pawn == null)
            return HookResult.Continue;

        EnsureFullHealth(pawn);

        if (_phase != MatchPhase.Lobby || pawn.Controller == null || !TryGetSlot(pawn.Controller, out var slot))
            return HookResult.Continue;

        if (_readySlots.Remove(slot))
            BroadcastChat($"[2v2Lock] {GetPlayerName(pawn.Controller, slot)} changed hero and is no longer ready.");

        return HookResult.Continue;
    }

    [GameEventHandler("player_respawned")]
    public HookResult OnPlayerRespawned(PlayerRespawnedEvent args)
    {
        var pawn = args.Userid?.As<CCitadelPlayerPawn>();
        if (pawn == null)
            return HookResult.Continue;

        EnsureFullHealth(pawn);

        if (_pendingRoundReload && pawn.Controller != null && TryGetSlot(pawn.Controller, out var slot) && _pendingRoundParticipantsBySlot.ContainsKey(slot))
            CompletePendingRoundLoad(slot);

        return HookResult.Continue;
    }

    public override HookResult OnTakeDamage(TakeDamageEvent args)
    {
        var victim = args.Entity;
        var designerName = victim.DesignerName;
        var roundObjectivesEnabled = IsRoundObjectivesEnabled();

        if (AlwaysBlockedObjectives.Contains(designerName))
        {
            if (!roundObjectivesEnabled)
                return HookResult.Stop;

            Console.WriteLine($"[2v2Lock] Objective damage allowed during round: {designerName} | {victim.Name} | team {victim.TeamNum}");
            return HookResult.Continue;
        }

        if (roundObjectivesEnabled && IsActiveLaneTier1Objective(victim))
            Console.WriteLine($"[2v2Lock] Active lane T1 taking damage: {designerName} | {victim.Name} | team {victim.TeamNum} | damage {MathF.Max(args.Info.Damage, args.Info.TotalledDamage)}");

        return HookResult.Continue;
    }

    private bool HandlePendingRoundConnect(int slot, CCitadelPlayerController controller)
    {
        if (!_pendingRoundReload)
            return false;

        if (_pendingRoundParticipantsBySlot.TryGetValue(slot, out var participant))
        {
            controller.ChangeTeam(participant.Team);
            controller.SelectHero(participant.Hero);
            SendChat(slot, $"[2v2Lock] The round is loading on {GetActiveLaneDisplayName()} lane.");
            return true;
        }

        controller.ChangeTeam(SpectatorTeam);
        SendChat(slot, "[2v2Lock] The round has already started. You joined as a spectator.");
        return true;
    }

    private void InitializeLobbyPlayer(int slot, CCitadelPlayerController controller)
    {
        var team = PickTeamForNewPlayer();
        controller.ChangeTeam(team);

        if (team == SpectatorTeam)
        {
            SendChat(slot, "[2v2Lock] The 2v2 lobby is full. You joined as a spectator.");
            return;
        }

        var randomHero = GetRandomAvailableHero();
        controller.SelectHero(randomHero);

        SendHud(slot, "WELCOME TO 2v2LOCK", "Choose your hero and type \"/ready\" in chat!");
        SendChat(slot, $"[2v2Lock] You joined team {TeamName(team)} with {randomHero.ToDisplayName()}. Use /hero <name> if you want to switch, then type /ready.");
        BroadcastChat($"[2v2Lock] {GetPlayerName(controller, slot)} joined ({GetLockedPlayerCount()}/{RequiredPlayers}).");

        if (GetLockedPlayerCount() == RequiredPlayers)
            BroadcastHud("2v2Lock", "All 4 players are here. Type /ready.");
        else if (IsDebugEnabled())
            SendChat(slot, "[2v2Lock] Debug is enabled: you do not need 4 players to test.");
    }

    private static bool IsHeroChangeCommand(string command)
    {
        return command is "selecthero" or "citadel_hero_pick";
    }

    private bool IsHeroChangeLocked()
    {
        return _pendingRoundReload || _phase is MatchPhase.Live or MatchPhase.Resetting;
    }

    private void CompletePendingRoundLoad(int slot)
    {
        _roundRespawnedSlots.Add(slot);
        if (_roundRespawnedSlots.Count < _pendingRoundParticipantsBySlot.Count)
            return;

        _pendingRoundReload = false;
        ClearPendingRoundSnapshot();
        _phase = MatchPhase.Live;
        ScheduleFullHealForRoundParticipants();
    }

    private int PickTeamForNewPlayer()
    {
        var lockedPlayers = GetLockedPlayers();
        if (lockedPlayers.Count >= RequiredPlayers)
            return SpectatorTeam;

        var amberCount = lockedPlayers.Count(controller => controller.TeamNum == AmberTeam);
        var sapphireCount = lockedPlayers.Count(controller => controller.TeamNum == SapphireTeam);

        if (amberCount >= 2 && sapphireCount >= 2)
            return SpectatorTeam;

        if (amberCount < sapphireCount && amberCount < 2)
            return AmberTeam;

        if (sapphireCount < amberCount && sapphireCount < 2)
            return SapphireTeam;

        return amberCount < 2 ? AmberTeam : SapphireTeam;
    }
}
