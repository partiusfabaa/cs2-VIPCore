using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Health;

public class VipHealth : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Health";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private Health? _health;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();

        if (_api == null) return;

        _health = new Health(_api);
    }

    public override void Unload(bool hotReload)
    {
        _health?.Dispose();
    }
}

public class Health : VipFeature<int>
{
    public Health(IVipCoreApi api) : base("Health", api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawn = player.PlayerPawn.Value;

        var healthValue = GetValue(player);

        if (healthValue <= 0 || playerPawn == null) return;

        playerPawn.Health = healthValue;
        playerPawn.MaxHealth = healthValue;
        Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
    }
}