using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using VipCoreApi;
using VipCoreApi.Enums;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIP_RainbowModel;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] RainbowModel";
    public override string ModuleVersion => "v2.0.0";

    private RainbowModel _rainbowModel;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _rainbowModel = new RainbowModel(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _rainbowModel?.Dispose();
    }
}

public class RainbowModel : VipFeature<bool>
{
    private readonly BasePlugin _basePlugin;
    private readonly Timer?[] _rainbowTimer = new Timer?[70];

    public RainbowModel(BasePlugin basePlugin, IVipCoreApi api) : base("RainbowModel", api)
    {
        _basePlugin = basePlugin;
        basePlugin.RegisterListener<Listeners.OnClientConnected>(slot => _rainbowTimer[slot] = null);
        basePlugin.RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            if (_rainbowTimer[slot] != null)
                _rainbowTimer[slot]?.Kill();

            _rainbowTimer[slot] = null;
        });
        
        basePlugin.RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is null) return HookResult.Continue;

            _rainbowTimer[player.Slot]?.Kill();
            _rainbowTimer[player.Slot] = null;

            return HookResult.Continue;
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!IsPlayerValid(player)) return;

        var playerPawnValue = player.PlayerPawn.Value;
        var rainbowModelValue = GetValue(player);

        if (playerPawnValue == null) return;
        if (!rainbowModelValue) return;
        
        _rainbowTimer[player.Slot]?.Kill();
        _rainbowTimer[player.Slot] = _basePlugin.AddTimer(1.4f,
            () => SetRainbowModel(playerPawnValue, Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)),
            TimerFlags.REPEAT);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        if (feature.State == FeatureState.Disabled)
        {
            _rainbowTimer[player.Slot]?.Kill();
            SetRainbowModel(playerPawn);
            return;
        }

        _rainbowTimer[player.Slot] = _basePlugin.AddTimer(1.4f,
            () => SetRainbowModel(playerPawn, Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)), TimerFlags.REPEAT);
    }

    private void SetRainbowModel(CCSPlayerPawn pawn, int r = 255, int g = 255, int b = 255)
    {
        pawn.Render = Color.FromArgb(255, r, g, b);
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }
}