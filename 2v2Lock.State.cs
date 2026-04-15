using DeadworksManaged.Api;

namespace _2v2Lock;

public partial class _2v2Lock
{
    private sealed record PendingParticipant(int Team, Heroes Hero);
    private sealed record PendingRoundSnapshot(string LaneKey, Dictionary<int, PendingParticipant> Participants);

    private const int SpectatorTeam = 1;
    private const int AmberTeam = 2;
    private const int SapphireTeam = 3;
    private const int RequiredPlayers = 4;
    private const int DebugMinimumPlayersToStart = 1;

    private enum MatchPhase
    {
        Lobby,
        Starting,
        Live,
        Resetting
    }

    private sealed record LaneDefinition(string Key, string DisplayName, int ActiveLaneValue);

    private static readonly LaneDefinition[] Lanes =
    [
        new("yellow", "Yellow", 1),
        new("green", "Green", 3),
        new("blue", "Blue", 4),
        new("purple", "Purple", 6),
    ];

    private static readonly HashSet<string> AlwaysBlockedObjectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "npc_boss_tier3",
        "npc_boss_tier2",
        "npc_barrack_boss",
        "npc_base_defense_sentry",
    };

    private readonly HashSet<int> _readySlots = new();
    private readonly Dictionary<int, PendingParticipant> _pendingRoundParticipantsBySlot = new();
    private readonly HashSet<int> _roundRespawnedSlots = new();
    private static PendingRoundSnapshot? _sPendingRoundSnapshot;
    private MatchPhase _phase = MatchPhase.Lobby;
    private LaneDefinition? _activeLane;
    private IHandle? _mapResetTimer;
    private bool _pendingRoundReload;
}
