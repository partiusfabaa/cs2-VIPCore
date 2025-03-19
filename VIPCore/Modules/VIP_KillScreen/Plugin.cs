using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_KillScreen;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Kill Screen";
    public override string ModuleVersion => "v2.0.0";

    private KillScreen? _killScreen;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _killScreen = new KillScreen(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _killScreen?.Dispose();
    }
}

public class KillScreen : VipFeature<bool>
{
    public KillScreen(Plugin plugin, IVipCoreApi api) : base("Killscreen", api)
    {
        plugin.RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var attacker = @event.Attacker;
            if (attacker is null || !attacker.IsValid) return HookResult.Continue;
            if (@event.Userid is not null && attacker.PlayerName == @event.Userid.PlayerName) return HookResult.Continue;

            if (!IsPlayerValid(attacker) || !GetValue(attacker)) return HookResult.Continue;
            
            var attackerPawn = attacker.PlayerPawn.Value;
            if (attackerPawn == null) return HookResult.Continue;
            
            attackerPawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
            Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
            return HookResult.Continue;
        });
    }
}