using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_FastPlant;

public class VipFastPlant : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fast Defuse";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi? _api;
    private FastPlant _fastPlant;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _fastPlant = new FastPlant(this, _api);
        _api.RegisterFeature(_fastPlant);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_fastPlant);
    }
}

public class FastPlant : VipFeatureBase
{
    public override string Feature => "fastplant";

    public FastPlant(BasePlugin basePlugin, IVipCoreApi api) : base(api)
    {
        basePlugin.RegisterEventHandler<EventBombBeginplant>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return HookResult.Continue;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null) return HookResult.Continue;

            if (!IsClientVip(player) || !PlayerHasFeature(player) ||
                GetPlayerFeatureState(player) is not FeatureState.Enabled ||
                !player.PawnIsAlive) return HookResult.Continue;

            if (!GetFeatureValue<bool>(player)) return HookResult.Continue;

            var weaponService = playerPawn.WeaponServices?.ActiveWeapon;
            if (weaponService == null) return HookResult.Continue;
            
            var activeWeapon = weaponService.Value;
            if (activeWeapon == null) return HookResult.Continue;

            if (!activeWeapon.DesignerName.Contains("c4")) return HookResult.Continue;

            var c4 = new CC4(activeWeapon.Handle);
            c4.ArmedTime = Server.CurrentTime;
            
            return HookResult.Continue;
        });
    }
}