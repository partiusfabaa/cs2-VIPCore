using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Items;

public class VipItems : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Items";
    public override string ModuleVersion => "1.0.0";

    private static readonly string Feature = "Items";
    private IVipCoreApi _api = null!;

    private readonly Dictionary<string, int> _grenadeIndex = new()
    {
        ["flashbang"] = 14, 
        ["smokegrenade"] = 15,
        ["decoy"] = 17,
        ["incgrenade"] = 16,
        ["molotov"] = 16,
        ["hegrenade"] = 13
    };

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (_api.IsPistolRound()) return;
        
        if (!_api.PlayerHasFeature(controller, Feature)) return;
        if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var items = _api.GetFeatureValue<List<string>?>(controller, Feature);

        var playerPawnValue = controller.PlayerPawn.Value;
        if (playerPawnValue == null) return;

        var weaponService = playerPawnValue.WeaponServices;
        if (weaponService == null || items is not { Count: > 0 }) return;
        
        foreach (var item in items)
        {
            var itemName = _grenadeIndex.ContainsKey(item) ? item : null;
            var ammoIndex = itemName != null ? _grenadeIndex[item] : -1;

            if (itemName != null && weaponService.Ammo[ammoIndex] == 0)
                controller.GiveNamedItem(item);
            else
            {
                if (weaponService.MyWeapons.ToList().Find(m => m.Value != null && m.Value.DesignerName == item) == null)
                    controller.GiveNamedItem(item);
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}