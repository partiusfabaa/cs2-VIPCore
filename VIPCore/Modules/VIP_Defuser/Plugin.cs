using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;

namespace VIP_Defuser;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Defuser";
    public override string ModuleVersion => "v2.0.0";

    private Defuser? _defuser;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _defuser = new Defuser(api);
    }

    public override void Unload(bool hotReload)
    {
        _defuser?.Dispose();
    }
}

public class Defuser(IVipCoreApi api) : VipFeature<bool>("Defuser", api)
{
    private bool HasWeapon(CCSPlayerController player, string weaponName)
    {
        if (!player.IsValid || !player.PawnIsAlive)
            return false;

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || pawn.WeaponServices == null)
            return false;

        foreach (var weapon in pawn.WeaponServices.MyWeapons)
        {
            if (weapon?.Value?.IsValid == true && weapon.Value.DesignerName?.Contains(weaponName) == true)
            {
                return true;
            }
        }

        return false;
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!IsPlayerValid(player)) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        if (player.Team is CsTeam.CounterTerrorist && !HasWeapon(player, "item_defuser"))
        {
            var itemServices = new CCSPlayer_ItemServices(playerPawn.ItemServices!.Handle);
            itemServices.HasDefuser = true;
        }
    }
}