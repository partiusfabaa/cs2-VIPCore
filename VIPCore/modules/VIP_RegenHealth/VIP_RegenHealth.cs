using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_RegenHealth;

public class VipRegenHealth : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Health Regeneration";
    public override string ModuleVersion => "v2.0.0";

    private RegenHealth? _regenHealth; 
    private IVipCoreApi? _api;
    
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _regenHealth = new RegenHealth(this, _api);
        };
    }

    public override void Unload(bool hotReload)
    {
        _regenHealth?.Dispose();
    }
}

public class RegenHealth : VipFeature<Regen>
{
    private readonly bool[] _isRegenActive = new bool[65];
    private readonly Regen[] _regen = new Regen[65];
    private readonly float[] _regenInterval = new float[65];

    public RegenHealth(VipRegenHealth vipRegenHealth, IVipCoreApi api) : base("HealthRegen", api)
    {
        vipRegenHealth.RegisterListener<Listeners.OnMapStart>(name =>
            vipRegenHealth.AddTimer(1.0f, Timer_HealthRegen, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        vipRegenHealth.RegisterListener<Listeners.OnClientConnected>(slot => _isRegenActive[slot] = false);
        vipRegenHealth. RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isRegenActive[slot] = false);

        vipRegenHealth.RegisterEventHandler<EventPlayerHurt>((@event, info) =>
        {
            var player = @event.Userid;

            if (IsPlayerVip(player) && PlayerHasFeature(player) && @event.DmgHealth > 0)
            {
                if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return HookResult.Continue;
                _isRegenActive[player.Slot] = true;
                _regen[player.Slot] = GetValue(player)!;
                _regenInterval[player.Slot] = _regen[player.Slot].Interval;
            }

            return HookResult.Continue;
        });
    }

    private void Timer_HealthRegen()
    {
        foreach (var player in Utilities.GetPlayers()
                     .Where(u => u.PlayerPawn.Value != null && u.PlayerPawn.Value.IsValid && u.PawnIsAlive))
        {
            if (_isRegenActive[player.Slot] && IsPlayerVip(player) && PlayerHasFeature(player))
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
}

public class Regen
{
    public int Health { get; set; } = 0;
    public float Delay { get; set; } = 0;
    public float Interval { get; set; } = 0;
}