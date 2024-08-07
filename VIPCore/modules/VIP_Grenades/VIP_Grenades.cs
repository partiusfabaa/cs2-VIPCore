﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Grenades;
public class VipGrenades : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Grenades";
    public override string ModuleVersion => "1.0";

    private Grenades _grenades = null!;
    private IVipCoreApi? _api;
    
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _grenades = new Grenades(_api);
        _api.RegisterFeature(_grenades);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_grenades);
    }
}
public class Grenades : VipFeatureBase
{
    public override string Feature => "Grenades";
    public Grenades(IVipCoreApi api) : base(api)
    {
    }

    private readonly Dictionary<string, int> _grenadeIndex = new()
    {
        ["weapon_flashbang"] = 14, 
        ["weapon_smokegrenade"] = 15,
        ["weapon_decoy"] = 17,
        ["weapon_incgrenade"] = 16,
        ["weapon_molotov"] = 16,
        ["weapon_hegrenade"] = 13
    };
    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var teamKey = player.TeamNum switch
        {
            2 => "T",
            3 => "CT",
            _ => null
        };

        if (teamKey == null) return;

        var teamGrenadeConfig = GetFeatureValue<Dictionary<string, Dictionary<string, int>>?>(player);
        if (teamGrenadeConfig == null || !teamGrenadeConfig.ContainsKey(teamKey)) return;

        var grenadeConfig = teamGrenadeConfig[teamKey];

        var weaponService = player.PlayerPawn.Value?.WeaponServices;
        if (weaponService == null) return;

        foreach (var entry in grenadeConfig)
        {
            string grenadeName = entry.Key;
            int maxGrenades = entry.Value;
            
            if (_grenadeIndex.TryGetValue(grenadeName, out int ammoIndex))
            {
                if (ammoIndex < 0 || ammoIndex >= weaponService.Ammo.Length) return;
                
                int currentGrenades = weaponService.Ammo[ammoIndex];

                for (int i = currentGrenades; i < maxGrenades; i++)
                {
                    player.GiveNamedItem(grenadeName);
                }
            }
        }
    }
}