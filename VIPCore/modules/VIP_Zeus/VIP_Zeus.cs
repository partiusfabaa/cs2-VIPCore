using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_Zeus;

public class VipZeus : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Zeus";
    public override string ModuleVersion => "v2.0.0";

    private Zeus? _zeus;
    private IVipCoreApi? _api;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _zeus = new Zeus(_api);
    }

    public override void Unload(bool hotReload)
    {
        _zeus?.Dispose();
    }
}

public class Zeus : VipFeature<bool>
{
    public Zeus(IVipCoreApi api) : base("Zeus", api)
    {
    }

    public static bool HasWeapon(CCSPlayerController player, string weaponName)
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
        if (!vip) return;
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;
        if (!IsFeatureEnabled(player) || !GetValue(player)) return;

        if ((player.TeamNum == 3 || player.TeamNum == 2) && !HasWeapon(player, "weapon_taser"))
        {
            player.GiveNamedItem("weapon_taser");
        }
    }
}