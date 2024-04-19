using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Speed;

public class VipSpeed : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Speed";
    public override string ModuleVersion => "v1.0";
    private IVipCoreApi? _api;
    private SpeedModifier? _speedModifier;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _speedModifier = new SpeedModifier(this, _api);
        _api.RegisterFeature(_speedModifier);
    }

    public override void Unload(bool hotReload)
    {
        if(_api != null && _speedModifier != null)
        {
            _api?.UnRegisterFeature(_speedModifier);
        }
    }
}

public class SpeedModifier : VipFeatureBase
{
    public override string Feature => "Speed";
    public SpeedModifier(VipSpeed vipSpeed, IVipCoreApi api) : base(api)
    {
        vipSpeed.RegisterEventHandler<EventPlayerHurt>(PrePlayerHurtHandler);
    }

    private HookResult PrePlayerHurtHandler(EventPlayerHurt @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;

        if (!PlayerHasFeature(player)) return HookResult.Continue;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return HookResult.Continue;

        if(!player.IsValid) return HookResult.Continue;

        var speedModifierValue = GetFeatureValue<float>(player);
        var playerPawn = player.PlayerPawn.Value;

        if(IsClientVip(player))
        {
            if (playerPawn != null)
            {
                playerPawn.VelocityModifier = speedModifierValue;
            } 
        }
        return HookResult.Continue;
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        if(!player.IsValid) return;

        var speedModifierValue = GetFeatureValue<float>(player);
        var playerPawn = player.PlayerPawn.Value;

        if (playerPawn != null)
        {
            playerPawn.VelocityModifier = speedModifierValue;
        } 
    }
}
