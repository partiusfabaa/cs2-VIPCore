using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Health;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Health";
    public override string ModuleVersion => "v2.0.0";

    private Health? _health;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api is null) return;

        _health = new Health(api);
    }

    public override void Unload(bool hotReload)
    {
        _health?.Dispose();
    }
}

public class Health(IVipCoreApi api) : VipFeature<int>("Health", api)
{
    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !IsPlayerValid(player)) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is null)
            return;

        var health = GetValue(player);

        playerPawn.Health = health;
        playerPawn.MaxHealth = health;

        Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
        {
            args.Display = $"[{GetValue(args.Controller)}]";
        }
    }
}