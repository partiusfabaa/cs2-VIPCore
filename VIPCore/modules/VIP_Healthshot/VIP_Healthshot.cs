using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Healthshot;

public class VipHealthshot : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Healthshot";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "Healthshot";
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (!_api.PlayerHasFeature(controller, Feature)) return;
        if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var playerPawnValue = controller.PlayerPawn.Value;
        var weaponServices = playerPawnValue?.WeaponServices;
        if (weaponServices == null) return;
        
        var curHealthshotCount = weaponServices.Ammo[20];
        var giveCount = _api.GetFeatureValue<int>(controller, Feature);
        
        for (var i = 0; i < giveCount - curHealthshotCount; i ++)
        {
            controller.GiveNamedItem("weapon_healthshot");
        }
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}
