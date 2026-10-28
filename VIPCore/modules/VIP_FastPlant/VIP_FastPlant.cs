using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_FastPlant;

public class VipFastPlant : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fast Plant";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private FastPlant? _fastPlant;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _fastPlant = new FastPlant(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _fastPlant?.Dispose();
    }
}

public class FastPlant : VipFeature<bool>
{
    public FastPlant(BasePlugin basePlugin, IVipCoreApi api) : base("fastplant", api)
    {
        basePlugin.RegisterEventHandler<EventBombBeginplant>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return HookResult.Continue;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null) return HookResult.Continue;

            if (!IsFeatureEnabled(player) || !player.PawnIsAlive) return HookResult.Continue;

            if (!GetValue(player)) return HookResult.Continue;

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