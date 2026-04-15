using DeadworksManaged.Api;

namespace _2v2Lock;

public partial class _2v2Lock
{
    [ChatCommand("ready")]
    public HookResult OnReady(ChatCommandContext ctx)
    {
        var controller = ctx.Controller;
        var pawn = controller?.GetHeroPawn();
        var slot = ctx.Message.SenderSlot;
        if (controller == null || pawn == null)
            return HookResult.Handled;

        if (!IsLockedTeam(controller.TeamNum))
        {
            SendChat(slot, "[2v2Lock] Spectators cannot ready up.");
            return HookResult.Handled;
        }

        if (_phase != MatchPhase.Lobby)
        {
            SendChat(slot, "[2v2Lock] A round is already in progress.");
            return HookResult.Handled;
        }

        if (_readySlots.Contains(slot))
        {
            _readySlots.Remove(slot);
            BroadcastChat($"[2v2Lock] {GetPlayerName(controller, slot)} is no longer ready ({GetReadyCount()}/{GetLockedPlayerCount()}).");
            return HookResult.Handled;
        }

        _readySlots.Add(slot);
        BroadcastChat($"[2v2Lock] {GetPlayerName(controller, slot)} is ready ({GetReadyCount()}/{GetLockedPlayerCount()}).");
        TryStartMatch();

        if (_phase == MatchPhase.Lobby && GetLockedPlayerCount() < RequiredPlayers)
        {
            if (IsDebugEnabled())
                SendChat(slot, $"[2v2Lock] Debug is enabled: the round can start with {GetLockedPlayerCount()} player(s) if everyone types /ready.");
            else
                SendChat(slot, $"[2v2Lock] {RequiredPlayers - GetLockedPlayerCount()} more player(s) are needed to start.");
        }

        return HookResult.Handled;
    }

    [ChatCommand("unready")]
    public HookResult OnUnready(ChatCommandContext ctx)
    {
        var controller = ctx.Controller;
        var slot = ctx.Message.SenderSlot;
        if (controller == null)
            return HookResult.Handled;

        if (_readySlots.Remove(slot))
            BroadcastChat($"[2v2Lock] {GetPlayerName(controller, slot)} is no longer ready ({GetReadyCount()}/{GetLockedPlayerCount()}).");
        else
            SendChat(slot, "[2v2Lock] You were not ready.");

        return HookResult.Handled;
    }
}
