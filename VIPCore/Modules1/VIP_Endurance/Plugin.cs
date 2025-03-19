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

    private Endurance? _endurance;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _endurance = new Endurance(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _endurance?.Dispose();
    }
}

public class Endurance : VipFeature<bool>
{
    private readonly bool[] _enduranceEnabled = new bool[64];

    public Endurance(BasePlugin basePlugin, IVipCoreApi api) : base("Endurance", api)
    {
        basePlugin.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var players in Utilities.GetPlayers()
                         .Where(u =>
                         u is { IsValid: true, PawnIsAlive: true } && 
                         IsPlayerValid(u) && 
                         _enduranceEnabled[u.Slot]))
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
        _enduranceEnabled[player.Slot] = IsPlayerValid(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        _enduranceEnabled[player.Slot] = feature.State == FeatureState.Enabled;
    }
}