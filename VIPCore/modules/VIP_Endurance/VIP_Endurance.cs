using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Endurance;

public class VipEndurance : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Endurance";
    public override string ModuleVersion => "v2.0.0";

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    private IVipCoreApi? _api;
    private Endurance? _endurance;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _endurance = new Endurance(this, _api);
        };
    }

    public override void Unload(bool hotReload)
    {
        _endurance?.Dispose();
    }
}

public class Endurance : VipFeature
{
    private readonly bool[] _enduranceEnabled = new bool[64];

    public Endurance(BasePlugin basePlugin, IVipCoreApi api) : base("endurance", api)
    {
        basePlugin.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var players in Utilities.GetPlayers().Where(u =>
                         u is { IsValid: true, PawnIsAlive: true } && IsPlayerVip(u) && PlayerHasFeature(u) && _enduranceEnabled[u.Slot]))
            {
                var playerPawn = players.PlayerPawn.Value;
                if (playerPawn == null) continue;

                if (playerPawn is { IsValid: true, VelocityModifier: < 1.0f })
                {
                    playerPawn.VelocityModifier = 1.0f;
                }
            }
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        if (!PlayerHasFeature(player)) return;

        _enduranceEnabled[player.Slot] = GetPlayerFeatureState(player) == FeatureState.Enabled;
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        _enduranceEnabled[player.Slot] = GetPlayerFeatureState(player) == FeatureState.Enabled;
    }
}