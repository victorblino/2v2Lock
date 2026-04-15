using DeadworksManaged.Api;

namespace _2v2Lock;

public partial class _2v2Lock
{
    [ChatCommand("hero")]
    public HookResult OnHero(ChatCommandContext ctx)
    {
        var controller = ctx.Controller;
        var slot = ctx.Message.SenderSlot;
        if (controller == null)
            return HookResult.Handled;

        if (!IsLockedTeam(controller.TeamNum))
        {
            SendChat(slot, "[2v2Lock] Spectators cannot choose a hero.");
            return HookResult.Handled;
        }

        if (_pendingRoundReload || _phase is MatchPhase.Live or MatchPhase.Resetting)
        {
            SendChat(slot, "[2v2Lock] You can only change hero in the lobby.");
            return HookResult.Handled;
        }

        if (ctx.Args.Length == 0)
        {
            var currentHero = GetCurrentOrFallbackHero(controller);
            SendChat(slot, $"[2v2Lock] Current hero: {currentHero.ToDisplayName()}. Use /hero random or /hero <name>.");
            return HookResult.Handled;
        }

        Heroes hero;
        if (ctx.Args.Length == 1 && ctx.Args[0].Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            hero = GetRandomAvailableHero();
        }
        else
        {
            var heroInput = string.Join(" ", ctx.Args);
            if (!TryResolveHero(heroInput, out hero))
            {
                SendChat(slot, "[2v2Lock] Invalid hero. Example: /hero inferno, /hero haze, /hero grey talon or /hero random.");
                return HookResult.Handled;
            }
        }

        controller.SelectHero(hero);
        SendChat(slot, $"[2v2Lock] Hero set to {hero.ToDisplayName()}. When you're ready, type /ready.");
        return HookResult.Handled;
    }
}
