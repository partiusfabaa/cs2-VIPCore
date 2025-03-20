using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Armor;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Armor";
    public override string ModuleVersion => "v2.0.0";

    private Armor? _armor;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api is null) return;

        _armor = new Armor(api);
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
        if (!vip || !IsPlayerValid(player)) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is null)
            return;

        playerPawn.ArmorValue = GetValue(player);
        Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_ArmorValue");
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
        {
            args.Display = $"[{GetValue(args.Controller)}]";
        }
    }
}