using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Modularity;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Armor;

public class VipArmor : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Armor";
    public override string ModuleVersion => "v1.0.1";

    private IVipCoreApi _api = null!;
    private Armor _armor;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _armor = new Armor(_api);
        _api.RegisterFeature(_armor);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_armor);
    }
}

public class Armor : VipFeatureBase
{
    public override string Feature => "Armor";
    public Armor(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawn = player.PlayerPawn.Value;

        var armorValue = GetFeatureValue<int>(player);

        if (armorValue <= 0 || playerPawn == null) return;

        if (playerPawn.ItemServices != null)
            new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle).HasHelmet = true;

        playerPawn.ArmorValue = armorValue;
    }
}