using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Items;

public class VipItems : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Items";
    public override string ModuleVersion => "v2.0.0";

    private Items _items = null!;
    private IVipCoreApi? _api;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _items = new Items(_api);
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
        if (!vip || IsPistolRound()) return;

        if (!IsFeatureEnabled(player)) return;

        var itemsConfig = GetValue(player);
        if (itemsConfig == null) return;

        var teamKey = player.TeamNum switch
        {
            2 => "T",
            3 => "CT",
            _ => null
        };

        if (teamKey == null || !itemsConfig.ContainsKey(teamKey)) return;

        var items = itemsConfig[teamKey];

        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null) return;

        var weaponService = playerPawnValue.WeaponServices;
        if (weaponService == null || items is not { Count: > 0 }) return;

        foreach (var item in items)
        {
            var itemName = item.StartsWith("weapon_") || item.StartsWith("item_") ? item : $"weapon_{item}";
            var shortName = itemName.StartsWith("weapon_") ? itemName["weapon_".Length..] : itemName;

            if (_grenadeIndex.TryGetValue(shortName, out var ammoIndex))
            {
                if (weaponService.Ammo[ammoIndex] == 0)
                    player.GiveNamedItem(itemName);
            }
            else
            {
                if (weaponService.MyWeapons.ToList().Find(m => m.Value != null && m.Value.DesignerName == itemName) == null)
                    player.GiveNamedItem(itemName);
            }
        }
    }
}