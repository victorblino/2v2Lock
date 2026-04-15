using DeadworksManaged.Api;

namespace _2v2Lock;

public partial class _2v2Lock
{
    private const string UnknownLaneDisplayName = "Unknown";
    private static readonly int[] FullHealRetryTicks = [1, 2, 4, 8, 16, 32, 64, 128];

    private void ResetStateForNewMap()
    {
        _readySlots.Clear();
        _pendingRoundParticipantsBySlot.Clear();
        _roundRespawnedSlots.Clear();
        _pendingRoundReload = false;
        _phase = MatchPhase.Lobby;
        _activeLane = null;
        _mapResetTimer?.Cancel();
        _mapResetTimer = null;
    }

    private void ClearPendingRoundSnapshot()
    {
        _sPendingRoundSnapshot = null;
    }

    private void PersistPendingRoundSnapshot()
    {
        if (_activeLane == null || _pendingRoundParticipantsBySlot.Count == 0)
            return;

        _sPendingRoundSnapshot = new PendingRoundSnapshot(
            _activeLane.Key,
            _pendingRoundParticipantsBySlot.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
    }

    private void RestorePendingRoundSnapshotIfNeeded()
    {
        if (_pendingRoundReload || _sPendingRoundSnapshot == null)
            return;

        var lane = Lanes.FirstOrDefault(candidate =>
            candidate.Key.Equals(_sPendingRoundSnapshot.LaneKey, StringComparison.OrdinalIgnoreCase));
        if (lane == null)
        {
            ClearPendingRoundSnapshot();
            return;
        }

        _pendingRoundReload = true;
        _activeLane = lane;
        _pendingRoundParticipantsBySlot.Clear();

        foreach (var (slot, participant) in _sPendingRoundSnapshot.Participants)
            _pendingRoundParticipantsBySlot[slot] = participant;
    }

    private void ApplyLobbyConVars()
    {
        ConVar.Find("citadel_trooper_spawn_enabled")?.SetInt(0);
        ConVar.Find("citadel_npc_spawn_enabled")?.SetInt(0);
        ConVar.Find("citadel_active_lane")?.SetInt(255);
        ConVar.Find("citadel_start_players_on_zipline")?.SetInt(0);
        ConVar.Find("citadel_allow_duplicate_heroes")?.SetInt(1);
        ConVar.Find("citadel_voice_all_talk")?.SetInt(1);
        Server.ExecuteCommand("ai_disable 0");
    }

    private void ApplyRoundConVars(LaneDefinition lane)
    {
        ConVar.Find("citadel_trooper_spawn_enabled")?.SetInt(1);
        ConVar.Find("citadel_npc_spawn_enabled")?.SetInt(1);
        ConVar.Find("citadel_active_lane")?.SetInt(lane.ActiveLaneValue);
        ConVar.Find("citadel_start_players_on_zipline")?.SetInt(1);
        Server.ExecuteCommand("ai_disable 0");
        Timer.NextTick(() => Server.ExecuteCommand("ai_disable 0"));
    }

    private IReadOnlyList<CCitadelPlayerController> GetLockedPlayers()
    {
        return Players.GetAll()
            .Where(controller => controller.TeamNum is AmberTeam or SapphireTeam)
            .ToArray();
    }

    private static bool IsLockedTeam(int teamNum)
    {
        return teamNum is AmberTeam or SapphireTeam;
    }

    private int GetReadyCount()
    {
        return _readySlots.Count;
    }

    private int GetLockedPlayerCount()
    {
        return GetLockedPlayers().Count;
    }

    private int GetMinimumPlayersToStart()
    {
        return IsDebugEnabled() ? DebugMinimumPlayersToStart : RequiredPlayers;
    }

    private bool CanStartWithCurrentRoster(IReadOnlyList<CCitadelPlayerController> lockedPlayers)
    {
        if (lockedPlayers.Count < GetMinimumPlayersToStart())
            return false;

        if (!IsDebugEnabled())
            return HasFullLobby();

        return true;
    }

    private bool HasFullLobby()
    {
        var amberCount = 0;
        var sapphireCount = 0;

        foreach (var controller in GetLockedPlayers())
        {
            if (controller.TeamNum == AmberTeam)
                amberCount++;
            else if (controller.TeamNum == SapphireTeam)
                sapphireCount++;
        }

        return amberCount == 2 && sapphireCount == 2 && amberCount + sapphireCount == RequiredPlayers;
    }

    private static string TeamName(int teamNum)
    {
        return teamNum switch
        {
            AmberTeam => "Hidden King",
            SapphireTeam => "The Archmother",
            _ => "Unknown"
        };
    }

    private static string GetPlayerName(CCitadelPlayerController controller, int slot)
    {
        return string.IsNullOrWhiteSpace(controller.PlayerName) ? $"Player {slot}" : controller.PlayerName;
    }

    private static CCitadelUserMsg_ChatMsg CreateChatMessage(string text)
    {
        return new CCitadelUserMsg_ChatMsg
        {
            PlayerSlot = -1,
            Text = text,
            AllChat = true
        };
    }

    private static void SendChat(int slot, string text)
    {
        NetMessages.Send(CreateChatMessage(text), RecipientFilter.Single(slot));
    }

    private static void BroadcastChat(string text)
    {
        NetMessages.Send(CreateChatMessage(text), RecipientFilter.All);
    }

    private static void BroadcastHud(string title, string description)
    {
        var msg = new CCitadelUserMsg_HudGameAnnouncement
        {
            TitleLocstring = title,
            DescriptionLocstring = description
        };

        NetMessages.Send(msg, RecipientFilter.All);
    }

    private static void SendHud(int slot, string title, string description)
    {
        var msg = new CCitadelUserMsg_HudGameAnnouncement
        {
            TitleLocstring = title,
            DescriptionLocstring = description
        };

        NetMessages.Send(msg, RecipientFilter.Single(slot));
    }

    private string GetActiveLaneDisplayName()
    {
        return _activeLane?.DisplayName ?? UnknownLaneDisplayName;
    }

    private static bool TryGetSlot(CBasePlayerController controller, out int slot)
    {
        for (var i = 0; i < Players.MaxSlot; i++)
        {
            var current = Players.FromSlot(i);
            if (current?.EntityIndex != controller.EntityIndex)
                continue;

            slot = i;
            return true;
        }

        slot = -1;
        return false;
    }

    private static bool IsObjectiveDebugEntity(string designerName)
    {
        return designerName.StartsWith("npc_boss_", StringComparison.OrdinalIgnoreCase) ||
               designerName.Equals("npc_barrack_boss", StringComparison.OrdinalIgnoreCase) ||
               designerName.Equals("npc_base_defense_sentry", StringComparison.OrdinalIgnoreCase) ||
               designerName.Equals("npc_trooper_boss", StringComparison.OrdinalIgnoreCase);
    }

    private static void ForceFullHealth(CCitadelPlayerPawn pawn)
    {
        var maxHealth = pawn.GetMaxHealth();
        if (maxHealth <= 0)
            maxHealth = pawn.MaxHealth;

        if (maxHealth <= 0)
            return;

        if (pawn.MaxHealth < maxHealth)
            pawn.MaxHealth = maxHealth;

        pawn.Health = maxHealth;
        pawn.Heal(maxHealth);
    }

    private void EnsureFullHealth(CCitadelPlayerPawn pawn)
    {
        ForceFullHealth(pawn);
        ScheduleFullHeal(pawn);
    }

    private void ScheduleFullHeal(CCitadelPlayerPawn pawn)
    {
        var entityIndex = pawn.EntityIndex;

        foreach (var delayTicks in FullHealRetryTicks)
        {
            Timer.Once(delayTicks.Ticks(), () =>
            {
                var currentPawn = CBaseEntity.FromIndex<CCitadelPlayerPawn>(entityIndex);
                if (currentPawn == null)
                    return;

                ForceFullHealth(currentPawn);
            });
        }
    }

    private void ScheduleFullHealForRoundParticipants()
    {
        foreach (var slot in _pendingRoundParticipantsBySlot.Keys)
        {
            var pawn = Players.FromSlot(slot)?.GetHeroPawn();
            if (pawn == null)
                continue;

            EnsureFullHealth(pawn);
        }
    }

    private bool IsRoundObjectivesEnabled()
    {
        return _activeLane != null && !_pendingRoundReload && _phase == MatchPhase.Live;
    }

    private bool IsActiveLaneTier1Objective(CBaseEntity entity)
    {
        if (_activeLane == null)
            return false;

        if (!entity.DesignerName.Equals("npc_trooper_boss", StringComparison.OrdinalIgnoreCase))
            return false;

        var entityName = entity.Name;
        if (string.IsNullOrWhiteSpace(entityName))
            return false;

        var normalizedName = entityName.ToLowerInvariant();
        return normalizedName.Contains("t1") && normalizedName.Contains(_activeLane.Key.ToLowerInvariant());
    }

    private static int ResolveWinningTeamFromObjective(CBaseEntity entity)
    {
        return entity.TeamNum switch
        {
            AmberTeam => SapphireTeam,
            SapphireTeam => AmberTeam,
            _ => 0
        };
    }

    private static Heroes[] GetAvailableHeroes()
    {
        return Enum.GetValues<Heroes>()
            .Where(candidate => candidate.GetHeroData()?.AvailableInGame == true)
            .ToArray();
    }

    private static Heroes GetRandomAvailableHero()
    {
        var heroes = GetAvailableHeroes();
        return heroes[Random.Shared.Next(heroes.Length)];
    }

    private static string NormalizeHeroName(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool TryResolveHero(string input, out Heroes hero)
    {
        if (HeroTypeExtensions.TryParse(input, out hero) && hero.GetHeroData()?.AvailableInGame == true)
            return true;

        var normalizedInput = NormalizeHeroName(input);
        foreach (var candidate in GetAvailableHeroes())
        {
            if (NormalizeHeroName(candidate.ToString()) == normalizedInput ||
                NormalizeHeroName(candidate.ToHeroName()) == normalizedInput ||
                NormalizeHeroName(candidate.ToDisplayName()) == normalizedInput)
            {
                hero = candidate;
                return true;
            }
        }

        hero = default;
        return false;
    }

    private void TryStartMatch()
    {
        if (_phase != MatchPhase.Lobby)
            return;

        var lockedPlayers = GetLockedPlayers();
        if (!CanStartWithCurrentRoster(lockedPlayers))
            return;

        if (lockedPlayers.Any(controller => controller.GetHeroPawn() == null))
            return;

        if (GetReadyCount() != lockedPlayers.Count)
            return;

        StartMatch(lockedPlayers);
    }

    private void StartMatch(IReadOnlyList<CCitadelPlayerController> players)
    {
        _phase = MatchPhase.Starting;
        var selectedLane = Lanes[Random.Shared.Next(Lanes.Length)];
        _activeLane = selectedLane;
        _readySlots.Clear();
        _pendingRoundReload = true;
        _pendingRoundParticipantsBySlot.Clear();
        _roundRespawnedSlots.Clear();

        foreach (var controller in players)
        {
            if (!TryGetSlot(controller, out var slot))
                continue;

            var hero = GetCurrentOrFallbackHero(controller);
            _pendingRoundParticipantsBySlot[slot] = new PendingParticipant(controller.TeamNum, hero);
        }

        PersistPendingRoundSnapshot();
        ApplyRoundConVars(selectedLane);
        BroadcastHud("2v2Lock", "Everyone is ready... starting!");
        BroadcastChat("[2v2Lock] Everyone is ready... starting!");
        Timer.Once(3.Seconds(), () => Server.ExecuteCommand($"changelevel {Server.MapName}"));
    }

    private Heroes GetCurrentOrFallbackHero(CCitadelPlayerController controller)
    {
        var heroId = controller.PlayerDataGlobal.HeroID;
        if (Enum.IsDefined(typeof(Heroes), heroId))
        {
            var hero = (Heroes)heroId;
            if (hero.GetHeroData()?.AvailableInGame == true)
                return hero;
        }

        return GetRandomAvailableHero();
    }

    private void FinishRound(int winningTeam)
    {
        if (_phase == MatchPhase.Resetting)
            return;

        _phase = MatchPhase.Resetting;
        _readySlots.Clear();
        ClearPendingRoundSnapshot();

        if (winningTeam is AmberTeam or SapphireTeam)
        {
            var laneName = GetActiveLaneDisplayName();
            BroadcastHud($"{TeamName(winningTeam)} won!", string.Empty);
            BroadcastChat($"[2v2Lock] {TeamName(winningTeam)} won on {laneName} lane.");
        }
        else
        {
            BroadcastHud("2v2Lock", "Round canceled: not enough players.");
            BroadcastChat("[2v2Lock] Round canceled: not enough players. Resetting the map...");
        }

        _mapResetTimer?.Cancel();
        _mapResetTimer = Timer.Once(4.Seconds(), () =>
        {
            Server.ExecuteCommand($"changelevel {Server.MapName}");
        });
        _mapResetTimer.CancelOnMapChange();
    }

    private void CancelRoundBecauseRosterChanged()
    {
        if (_phase is not MatchPhase.Starting and not MatchPhase.Live)
            return;

        FinishRound(0);
    }
}
