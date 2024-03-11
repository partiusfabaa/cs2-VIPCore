using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_FastDefuse;

public class VipFastDefuse : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fast Defuse";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi? _api;
    private FastDefuse _fastDefuse;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _fastDefuse = new FastDefuse(this, _api);
            _api.RegisterFeature(_fastDefuse);
        };
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_fastDefuse);
    }
}

public class FastDefuse : VipFeatureBase
{
    public override string Feature => "fastdefuse";

    public FastDefuse(BasePlugin basePlugin, IVipCoreApi api) : base(api)
    {
        basePlugin.RegisterEventHandler<EventBombBegindefuse>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null) return HookResult.Continue;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null) return HookResult.Continue;
            
            if (!IsClientVip(player) || !PlayerHasFeature(player) || GetPlayerFeatureState(player) is not FeatureState.Enabled || !player.PawnIsAlive) return HookResult.Continue;
            
            var featureValue = GetFeatureValue<float>(player);
            var bomb = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").First();
            
            Server.NextFrame(() =>
            {
                float countDown;
                if (bomb.DefuseCountDown < Server.CurrentTime)
                    countDown = 10;
                else
                    countDown = bomb.DefuseCountDown - Server.CurrentTime;

                countDown -= countDown / 100 * featureValue;
                bomb.DefuseCountDown = countDown + Server.CurrentTime;
                playerPawn.ProgressBarDuration = (int)float.Ceiling(countDown);
            });

            return HookResult.Continue;
        });
    }
}