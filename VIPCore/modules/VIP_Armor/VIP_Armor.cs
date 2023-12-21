using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Armor;

public class VipArmor : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Armor";
    public override string ModuleVersion => "v1.0.1";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "Armor";

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

        var armorValue = _api.GetFeatureValue<int>(controller, Feature);

        if (armorValue <= 0 || playerPawnValue == null) return;

        if (playerPawnValue.ItemServices != null)
            new CCSPlayer_ItemServices(playerPawnValue.ItemServices.Handle).HasHelmet = true;

        playerPawnValue.ArmorValue = armorValue;
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}