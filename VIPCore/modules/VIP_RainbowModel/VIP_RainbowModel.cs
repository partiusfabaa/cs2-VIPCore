using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIP_RainbowModel;

public class VipRainbowModel : BasePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] RainbowModel";
    public override string ModuleVersion => "v1.0.4";

    private IVipCoreApi? _api;
    private RainbowModel _rainbowModel;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _rainbowModel = new RainbowModel(this, _api);
        _api.RegisterFeature(_rainbowModel);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_rainbowModel);
    }
}

public class RainbowModel : VipFeatureBase
{
    private readonly BasePlugin _basePlugin;
    public override string Feature => "RainbowModel";

    private readonly Timer?[] _rainbowTimer = new Timer?[70];

    public RainbowModel(BasePlugin basePlugin, IVipCoreApi api) : base(api)
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

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawnValue = player.PlayerPawn.Value;

        var rainbowModelValue = GetFeatureValue<bool>(player);

        if (playerPawnValue == null) return;
        if (!rainbowModelValue) return;
        
        _rainbowTimer[player.Slot]?.Kill();
        _rainbowTimer[player.Slot] = _basePlugin.AddTimer(1.4f,
            () => SetRainbowModel(playerPawnValue, Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)),
            TimerFlags.REPEAT);
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        if (state == FeatureState.Disabled)
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