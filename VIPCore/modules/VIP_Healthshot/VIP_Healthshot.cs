using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Healthshot;

public class VipHealthshot : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Healthshot";
    public override string ModuleVersion => "v2.0.0";
    
    private Healthshot? _healthshot;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _healthshot = new Healthshot(_api);
        };
    }
    
    public override void Unload(bool hotReload)
    {
        _healthshot?.Dispose();
    }
}

public class Healthshot : VipFeature<int>
{
    public Healthshot(IVipCoreApi api) : base("Healthshot", api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !IsFeatureEnabled(player)) return;

        var playerPawnValue = player.PlayerPawn.Value;
        var weaponServices = playerPawnValue?.WeaponServices;
        if (weaponServices == null) return;
        
        var curHealthshotCount = weaponServices.Ammo[20];
        var giveCount = GetValue(player);
        
        for (var i = 0; i < giveCount - curHealthshotCount; i ++)
        {
            player.GiveNamedItem("weapon_healthshot");
        }
    }
}
