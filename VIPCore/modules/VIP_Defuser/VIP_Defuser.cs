using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Defuser;

public class VipDefuser : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Defuser";
    public override string ModuleVersion => "v2.0.0";
    
    private Defuser? _defuser;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _defuser = new Defuser(_api);
    }
    
    public override void Unload(bool hotReload)
    {
        _defuser?.Dispose();
    }
}

public class Defuser : VipFeature
{
    public Defuser(IVipCoreApi api) : base("Defuser", api)
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
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is FeatureState.Disabled
            or FeatureState.NoAccess) return;
            
        if (player == null) return;
        
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;
           
        if (player.TeamNum == 3 && !HasWeapon(player, "item_defuser"))
        {
            if (playerPawn != null)
            {
                var itemServices = new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle);
                    itemServices.HasDefuser = true;
            }
        }
    }
}