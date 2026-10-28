using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;

namespace VIP_Armor;

public class VipArmor : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Armor";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private Armor? _armor;
    
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _armor = new Armor(_api);
    }

    public override void Unload(bool hotReload)
    {
        _armor?.Dispose();
    }
}

public class Armor(IVipCoreApi api) : VipFeature<int>("Armor", api)
{
    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !IsFeatureEnabled(player)) return;

        var playerPawn = player.PlayerPawn.Value;

        var armorValue = GetValue(player);
        if (armorValue <= 0 || playerPawn == null) return;

        if (playerPawn.ItemServices != null)
            new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle).HasHelmet = true;

        playerPawn.ArmorValue = armorValue;
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
            args.Display = $"[{GetValue(args.Controller)}]";
    }
}