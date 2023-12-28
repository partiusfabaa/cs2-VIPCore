using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Modularity;
using VipCoreApi;

namespace VIP_KillScreen;

public class VipKillScreen : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Kill Screen";
    public override string ModuleVersion => "v1.0.1";
    
    private static readonly string Feature = "Killscreen";
    private IVipCoreApi _api = null!;
    
    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
    }
    
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var attacker = @event.Attacker;
            if (!attacker.IsValid) return HookResult.Continue;
            if (attacker.PlayerName == @event.Userid.PlayerName) return HookResult.Continue;

            if (!_api.IsClientVip(attacker)) return HookResult.Continue;
            if (!_api.PlayerHasFeature(attacker, Feature)) return HookResult.Continue;
            if (_api.GetPlayerFeatureState(attacker, Feature) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess) return HookResult.Continue;
			if(!_api.GetFeatureValue<bool>(attacker, Feature)) return HookResult.Continue;
            
            var attackerPawn = attacker.PlayerPawn.Value;
            
            if (attackerPawn == null) return HookResult.Continue;
            
            attackerPawn.HealthShotBoostExpirationTime = NativeAPI.GetCurrentTime() + 1.0f;
            Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");

            return HookResult.Continue;
        });
    }
}
