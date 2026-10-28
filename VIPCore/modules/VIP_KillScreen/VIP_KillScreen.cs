using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_KillScreen;

public class VipKillScreen : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Kill Screen";
    public override string ModuleVersion => "v2.0.0";

    private KillScreen? _killScreen;
    private IVipCoreApi? _api;
    
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _killScreen = new KillScreen(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _killScreen?.Dispose();
    }
}

public class KillScreen : VipFeature<bool>
{
    public KillScreen(VipKillScreen vipKillScreen, IVipCoreApi api) : base("Killscreen", api)
    {
        vipKillScreen.RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var attacker = @event.Attacker;
            if (attacker is null || !attacker.IsValid) return HookResult.Continue;
            if (@event.Userid is not null && attacker.PlayerName == @event.Userid.PlayerName) return HookResult.Continue;

            if (!IsPlayerVip(attacker)) return HookResult.Continue;
            if (!PlayerHasFeature(attacker)) return HookResult.Continue;
            if (GetPlayerFeatureState(attacker) is not FeatureState.Enabled) return HookResult.Continue;
            if(!GetValue(attacker)) return HookResult.Continue;
            
            var attackerPawn = attacker.PlayerPawn.Value;
            
            if (attackerPawn == null) return HookResult.Continue;
            
            attackerPawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
            Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");

            return HookResult.Continue;
        });
    }
}
