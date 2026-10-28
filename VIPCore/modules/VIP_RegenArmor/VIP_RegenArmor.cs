using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_RegenArmor;

public class VipRegenArmor : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Armor Regeneration";
    public override string ModuleVersion => "v2.0.0";

    private RegenArmor? _regenArmor;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _regenArmor = new RegenArmor(this, _api);
        };
    }

    public override void Unload(bool hotReload)
    {
        _regenArmor?.Dispose();
    }
}

public class RegenArmor : VipFeature<Regen>
{
    private readonly IVipCoreApi _api;
    private readonly bool[] _isRegenActive = new bool[65];
    private readonly Regen[] _regen = new Regen[65];
    private readonly float[] _regenInterval = new float[65];

    public RegenArmor(VipRegenArmor vipRegenArmor, IVipCoreApi api) : base("ArmorRegen", api)
    {
        _api = api;
        vipRegenArmor.RegisterListener<Listeners.OnMapStart>(name =>
            vipRegenArmor.AddTimer(1.0f, Timer_ArmorRegen, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        vipRegenArmor.RegisterListener<Listeners.OnClientConnected>(slot => _isRegenActive[slot] = false);
        vipRegenArmor.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isRegenActive[slot] = false);

        vipRegenArmor.RegisterEventHandler<EventPlayerHurt>((@event, info) =>
        {
            var player = @event.Userid;

            if (IsPlayerVip(player) && PlayerHasFeature(player) && @event.DmgArmor > 0)
            {
                if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return HookResult.Continue;

                _isRegenActive[player.Slot] = true;
                _regen[player.Slot] = GetValue(player)!;
                _regenInterval[player.Slot] = _regen[player.Slot].Interval;
            }

            return HookResult.Continue;
        });
    }

    private void Timer_ArmorRegen()
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

        const string armorFeature = "Armor";
        var maxArmor = 100;

        if (_api.PlayerHasFeature(player, armorFeature) &&
            _api.GetPlayerFeatureState(player, armorFeature) is FeatureState.Enabled)
        {
            maxArmor = _api.GetFeatureValue<int>(player, armorFeature);
        }

        if (playerPawn.ArmorValue < maxArmor)
        {
            playerPawn.ArmorValue += _regen[player.Slot].Armor;
            if (playerPawn.ArmorValue < maxArmor)
                return true;

            playerPawn.ArmorValue = maxArmor;
        }

        _isRegenActive[player.Slot] = false;
        return false;
    }
}

public class Regen
{
    public int Armor { get; set; } = 0;
    public float Delay { get; set; } = 0;
    public float Interval { get; set; } = 0;
}