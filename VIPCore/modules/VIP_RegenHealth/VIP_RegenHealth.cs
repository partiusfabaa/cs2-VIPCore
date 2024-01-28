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
    public override string ModuleVersion => "v1.0.1";

    private RegenHealth _regenHealth; 
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _regenHealth = new RegenHealth(this, _api);
        _api.RegisterFeature(_regenHealth);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_regenHealth);
    }
}

public class RegenHealth : VipFeatureBase
{
    public override string Feature => "HealthRegen";
    
    private readonly bool[] _isRegenActive = new bool[65];
    private readonly Regen[] _regen = new Regen[65];
    private readonly float[] _regenInterval = new float[65];

    public RegenHealth(VipRegenHealth vipRegenHealth, IVipCoreApi api) : base(api)
    {
        vipRegenHealth.RegisterListener<Listeners.OnMapStart>(name =>
            vipRegenHealth.AddTimer(1.0f, Timer_HealthRegen, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        vipRegenHealth.RegisterListener<Listeners.OnClientConnected>(slot => _isRegenActive[slot] = false);
        vipRegenHealth. RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isRegenActive[slot] = false);

        vipRegenHealth.RegisterEventHandler<EventPlayerHurt>((@event, info) =>
        {
            var player = @event.Userid;

            if (IsClientVip(player) && PlayerHasFeature(player) && @event.DmgHealth > 0)
            {
                if (GetPlayerFeatureState(player) is not IVipCoreApi.FeatureState.Enabled) return HookResult.Continue;
                _isRegenActive[player.Slot] = true;
                _regen[player.Slot] = GetFeatureValue<Regen>(player);
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
            if (_isRegenActive[player.Slot] && IsClientVip(player) && PlayerHasFeature(player))
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