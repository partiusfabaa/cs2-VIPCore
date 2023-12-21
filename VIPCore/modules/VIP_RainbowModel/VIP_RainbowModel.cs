using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Modularity;
using System.Drawing;
using VipCoreApi;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIP_RainbowModel;

public class VipRainbowModel : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] RainbowModel";
    public override string ModuleVersion => "v1.0.1";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "RainbowModel";
    private Timer?[] RainbowTimer = new Timer?[Server.MaxPlayers + 1];

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientConnected>(slot => RainbowTimer[slot + 1] = null);
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => RainbowTimer[slot + 1] = null);
    }

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, selectItem: OnSelectItem);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void SetRainbowModel(CCSPlayerPawn pawn, int R = 255, int G = 255, int B = 255)
    {
        pawn.Render = Color.FromArgb(255, R, G, B);
    }

    private void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (!_api.PlayerHasFeature(controller, Feature)) return;
        if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var playerPawnValue = controller.PlayerPawn.Value;

        var rainbowModelValue = _api.GetFeatureValue<bool>(controller, "RainbowModel");

        if (rainbowModelValue)
        {
            if (playerPawnValue != null)
            {
                RainbowTimer[controller.Index]?.Kill();
                RainbowTimer[controller.Index] = AddTimer(1.4f,
                    () => SetRainbowModel(playerPawnValue, Random.Shared.Next(0, 255),
                    Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)),
                    TimerFlags.REPEAT);
            }
        }
    }

    private void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        var playerPawnValue = player.PlayerPawn.Value;

        if (state == IVipCoreApi.FeatureState.Disabled && playerPawnValue != null)
        {
            RainbowTimer[player.Index]?.Kill();
            SetRainbowModel(playerPawnValue, 255, 255, 255);
            return;
        }
        else if (state == IVipCoreApi.FeatureState.Enabled && playerPawnValue != null)
        {
            RainbowTimer[player.Index] = AddTimer(1.4f,
            () => SetRainbowModel(playerPawnValue, Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)),
            TimerFlags.REPEAT);
        }
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}