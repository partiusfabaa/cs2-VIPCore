using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_Grenades;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Grenades";
    public override string ModuleVersion => "v2.0.0";

    private Grenades? _grenades;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _grenades = new Grenades(api);
    }

    public override void Unload(bool hotReload)
    {
        _grenades?.Dispose();
    }
}

public class Grenades : VipFeature<Dictionary<string, Dictionary<string, int>>>
{
    public Grenades(IVipCoreApi api) : base("Grenades", api)
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

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!IsPlayerValid(player)) return;

        var teamKey = player.TeamNum switch
        {
            2 => "T",
            3 => "CT",
            _ => null
        };

        if (teamKey == null) return;

        var teamGrenadeConfig = GetValue(player);
        if (teamGrenadeConfig == null || !teamGrenadeConfig.TryGetValue(teamKey, out var grenadeConfig)) return;

        var weaponService = player.PlayerPawn.Value?.WeaponServices;
        if (weaponService == null) return;

        foreach (var (grenadeName, maxGrenades) in grenadeConfig)
        {
            if (_grenadeIndex.TryGetValue(grenadeName, out var ammoIndex))
            {
                if (ammoIndex < 0 || ammoIndex >= weaponService.Ammo.Length) return;

                int currentGrenades = weaponService.Ammo[ammoIndex];

                for (var i = currentGrenades; i < maxGrenades; i++)
                {
                    player.GiveNamedItem(grenadeName);
                }
            }
        }
    }
}