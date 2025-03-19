using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_Items;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Items";
    public override string ModuleVersion => "v2.0.0";

    private Items? _items;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _items = new Items(api);
    }

    public override void Unload(bool hotReload)
    {
        _items?.Dispose();
    }
}

public class Items : VipFeature<Dictionary<string, List<string>>>
{
    private readonly Dictionary<string, int> _grenadeIndex = new()
    {
        ["flashbang"] = 14, 
        ["smokegrenade"] = 15,
        ["decoy"] = 17,
        ["incgrenade"] = 16,
        ["molotov"] = 16,
        ["hegrenade"] = 13
    };
    
    public Items(IVipCoreApi api) : base("Items", api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (IsPistolRound() || !IsPlayerValid(player)) return;

        var itemsConfig = GetFeatureValue<Dictionary<string, List<string>>?>(player);
        if (itemsConfig == null) return;

        var teamKey = player.TeamNum switch
        {
            2 => "T",
            3 => "CT",
            _ => null
        };

        if (teamKey == null || !itemsConfig.TryGetValue(teamKey, out var items)) return;

        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null) return;

        var weaponService = playerPawnValue.WeaponServices;
        if (weaponService == null || items is not { Count: > 0 }) return;

        foreach (var item in items)
        {
            var itemName = _grenadeIndex.ContainsKey(item) ? item : null;
            var ammoIndex = itemName != null ? _grenadeIndex[item] : -1;
            
            if (itemName != null && weaponService.Ammo[ammoIndex] == 0)
                player.GiveNamedItem(item);
            else
            {
                if (weaponService.MyWeapons.ToList().Find(m => m.Value != null && m.Value.DesignerName == item) == null)
                    player.GiveNamedItem(item);
            }
        }
    }
}