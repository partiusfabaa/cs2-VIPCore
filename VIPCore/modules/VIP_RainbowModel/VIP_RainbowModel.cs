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
    public override string ModuleVersion => "v1.0.3";

    private IVipCoreApi _api = null!;
    private RainbowModel _rainbowModel;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _rainbowModel = new RainbowModel(this, _api);
        _api.RegisterFeature(_rainbowModel, selectItem: _rainbowModel.OnSelectItem);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_rainbowModel);
    }
}

public class RainbowModel : VipFeatureBase
{
    private readonly VipRainbowModel _vipRainbowModel;
    public override string Feature => "RainbowModel";

    private readonly Timer?[] _rainbowTimer = new Timer?[65];

    public RainbowModel(VipRainbowModel vipRainbowModel, IVipCoreApi api) : base(api)
    {
        _vipRainbowModel = vipRainbowModel;
        vipRainbowModel.RegisterListener<Listeners.OnClientConnected>(slot => _rainbowTimer[slot + 1] = null);
        vipRainbowModel.RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            if (_rainbowTimer[slot + 1] != null)
                _rainbowTimer[slot + 1]?.Kill();

            _rainbowTimer[slot + 1] = null;
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (!PlayerHasFeature(controller)) return;
        if (GetPlayerFeatureState(controller) is not IVipCoreApi.FeatureState.Enabled) return;

        var playerPawnValue = controller.PlayerPawn.Value;

        var rainbowModelValue = GetFeatureValue<bool>(controller);

        if (playerPawnValue == null) return;
        if (!rainbowModelValue) return;
        
        _rainbowTimer[controller.Index]?.Kill();
        _rainbowTimer[controller.Index] = _vipRainbowModel.AddTimer(1.4f,
            () => SetRainbowModel(playerPawnValue, Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)),
            TimerFlags.REPEAT);
    }

    public void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        if (state == IVipCoreApi.FeatureState.Disabled)
        {
            _rainbowTimer[player.Index]?.Kill();
            SetRainbowModel(playerPawn);
            return;
        }

        _rainbowTimer[player.Index] = _vipRainbowModel.AddTimer(1.4f,
            () => SetRainbowModel(playerPawn, Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255)), TimerFlags.REPEAT);
    }

    private void SetRainbowModel(CCSPlayerPawn pawn, int R = 255, int G = 255, int B = 255)
    {
        pawn.Render = Color.FromArgb(255, R, G, B);
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }
}