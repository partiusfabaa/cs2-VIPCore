using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_RegenArmor;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Armor Regeneration";
    public override string ModuleVersion => "v2.0.0";

    private RegenArmor? _regenArmor;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _regenArmor = new RegenArmor(this, api);
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

    public RegenArmor(Plugin plugin, IVipCoreApi api) : base("ArmorRegen", api)
    {
        _api = api;
        plugin.RegisterListener<Listeners.OnMapStart>(name =>
            plugin.AddTimer(1.0f, Timer_ArmorRegen, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        plugin.RegisterListener<Listeners.OnClientConnected>(slot => _isRegenActive[slot] = false);
        plugin.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isRegenActive[slot] = false);

        plugin.RegisterEventHandler<EventPlayerHurt>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && IsPlayerValid(player) && @event.DmgArmor > 0)
            {
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
            if (_isRegenActive[player.Slot] && IsPlayerValid(player))
            {
                if (_regen[player.Slot].Delay > 0)
                {
                    _regen[player.Slot].Delay--;
                    continue;
                }

                if (_regenInterval[player.Slot] > 0)
                {
                    _regenInterval[player.Slot]--;
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
            Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_ArmorValue");
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