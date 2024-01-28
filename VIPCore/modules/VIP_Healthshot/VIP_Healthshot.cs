using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Healthshot;

public class VipHealthshot : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Healthshot";
    public override string ModuleVersion => "v1.0.1";
    
    private IVipCoreApi _api = null!;
    private Healthshot _healthshot;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _healthshot = new Healthshot(_api);
        _api.RegisterFeature(_healthshot);
    }
    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_healthshot);
    }
}

public class Healthshot : VipFeatureBase
{
    public override string Feature => "Healthshot";
    
    public Healthshot(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var playerPawnValue = player.PlayerPawn.Value;
        var weaponServices = playerPawnValue?.WeaponServices;
        if (weaponServices == null) return;
        
        var curHealthshotCount = weaponServices.Ammo[20];
        var giveCount = GetFeatureValue<int>(player);
        
        for (var i = 0; i < giveCount - curHealthshotCount; i ++)
        {
            player.GiveNamedItem("weapon_healthshot");
        }
    }
}
