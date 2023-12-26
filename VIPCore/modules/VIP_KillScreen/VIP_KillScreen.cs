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
    public override string ModuleVersion => "v1.0.0";

    private readonly MemoryFunctionVoid<nint, nint, int, short, short> _stateChanged = new(GetSignatureStateChanged());
    private readonly MemoryFunctionVoid<nint, int, long> _networkStateChanged = new(GetSignatureNetworkStateChanged());

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
            SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");

            return HookResult.Continue;
        });
    }
    
    private int FindSchemaChain(string classname) => Schema.GetSchemaOffset(classname, "__m_pChainEntity");

    private void SetStateChanged(CBaseEntity entity, string classname, string fieldname, int extraOffset = 0)
    {
        var offset = Schema.GetSchemaOffset(classname, fieldname);
        var chainOffset = FindSchemaChain(classname);

        if (chainOffset != 0)
        {
            _networkStateChanged.Invoke(entity.Handle + chainOffset, offset, 0xFFFFFFFF);
            return;
        }

        _stateChanged.Invoke(entity.NetworkTransmitComponent.Handle, entity.Handle, offset + extraOffset, -1, -1);

        entity.LastNetworkChange = Server.CurrentTime;
        entity.IsSteadyState.Clear();
    }
    
    private static string GetSignatureStateChanged()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x40\x55\x53\x56\x41\x55\x41\x57\x48\x8D\x6C\x24\xB0"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x53\x89\xD3";
    }
    
    private static string GetSignatureNetworkStateChanged()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x4C\x8B\xC9\x48\x8B\x09\x48\x85\xC9\x74\x2A\x48\x8B\x41\x10"
            : @"\x4C\x8B\x07\x4D\x85\xC0\x74\x2A\x49\x8B\x40\x10";
    }


}
