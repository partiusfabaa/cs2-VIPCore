using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Defuser;

public class VipDefuser : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Defuser";
    public override string ModuleVersion => "v1.1";
    
    private Defuser? _defuser;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _defuser = new Defuser(_api);
        _api.RegisterFeature(_defuser);
    }
    
    public override void Unload(bool hotReload)
    {
        if (_api != null && _defuser != null)
        {
            _api.UnRegisterFeature(_defuser);
        }
    }
}

public class Defuser : VipFeatureBase
{
    public override string Feature => "Defuser";
    public Defuser(IVipCoreApi api) : base(api)
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
    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;
        
        if (player.TeamNum == 3 && !HasWeapon(player, "item_defuser"))
        {
            player.GiveNamedItem("item_defuser");
        }
    }
}