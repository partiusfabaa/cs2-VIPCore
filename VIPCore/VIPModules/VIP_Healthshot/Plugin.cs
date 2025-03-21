using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_Healthshot;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Healthshot";
    public override string ModuleVersion => "v2.0.0";

    private Healthshot _healthshot;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _healthshot = new Healthshot(api);
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
        if (!IsPlayerValid(player)) return;

        var playerPawnValue = player.PlayerPawn.Value;
        var weaponServices = playerPawnValue?.WeaponServices;
        if (weaponServices == null) return;

        var curHealthshotCount = weaponServices.Ammo[20];
        var giveCount = GetValue(player);

        for (var i = 0; i < giveCount - curHealthshotCount; i++)
        {
            player.GiveNamedItem("weapon_healthshot");
        }
    }
}