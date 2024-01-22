using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using Modularity;
using VipCoreApi;

namespace VIP_RegenHealth;

public class VipRegenHealth : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Health Regeneration";
    public override string ModuleVersion => "v1.0.0";

    private const string Feature = "HealthRegen";
    private IVipCoreApi _api = null!;

    private readonly bool[] _isRegenActive = new bool[65];
    private readonly Regen[] _regen = new Regen[65];
    private readonly float[] _regenInterval = new float[65];

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnMapStart>(name => AddTimer(1.0f, Timer_HealthRegen, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        RegisterListener<Listeners.OnClientConnected>(slot => _isRegenActive[slot] = false);
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isRegenActive[slot] = false);
        
        RegisterEventHandler<EventPlayerHurt>((@event, info) =>
        {
            var player = @event.Userid;

            if (_api.IsClientVip(player) && _api.PlayerHasFeature(player, Feature) && @event.DmgHealth > 0)
            {
                if (_api.GetPlayerFeatureState(player, Feature) is not IVipCoreApi.FeatureState.Enabled) return HookResult.Continue;
                _isRegenActive[player.Slot] = true;
                _regen[player.Slot] = _api.GetFeatureValue<Regen>(player, Feature);
                _regenInterval[player.Slot] = _regen[player.Slot].Interval;
            }
            
            return HookResult.Continue;
        });
    }

    private void Timer_HealthRegen()
    {
        foreach (var player in Utilities.GetPlayers().Where(u => u.PlayerPawn.Value != null && u.PlayerPawn.Value.IsValid && u.PawnIsAlive))
        {
            if (_isRegenActive[player.Slot] && _api.IsClientVip(player) && _api.PlayerHasFeature(player, Feature))
            {
                if (_regen[player.Slot].Delay > 0)
                {
                    _regen[player.Slot].Delay --;
                    continue;
                }

                if (_regenInterval[player.Slot] > 0)
                {
                    _regenInterval[player.Slot] --;
                    continue;
                }

                if (HealthRegen(player)) _regenInterval[player.Slot] = _regen[player.Slot].Interval;
            }
        }
    }

    private bool HealthRegen(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return true;

        if (playerPawn.Health < playerPawn.MaxHealth)
        {
            playerPawn.Health += _regen[player.Slot].Health;
            Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
            if (playerPawn.Health < playerPawn.MaxHealth)
                return true;

            playerPawn.Health = playerPawn.MaxHealth;
            Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
        }

        _isRegenActive[player.Slot] = false;
        return false;
    }

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}

public class Regen
{
    public int Health { get; set; } = 0;
    public float Delay { get; set; } = 0;
    public float Interval { get; set; } = 0;
}
