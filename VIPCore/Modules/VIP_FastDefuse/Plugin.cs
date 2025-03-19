using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_FastDefuse;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fast Defuse";
    public override string ModuleVersion => "v2.0.0";

    private FastDefuse? _fastDefuse;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _fastDefuse = new FastDefuse(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _fastDefuse?.Dispose();
    }
}

public class FastDefuse : VipFeature<float>
{
    public FastDefuse(BasePlugin basePlugin, IVipCoreApi api) : base("fastdefuse", api)
    {
        basePlugin.RegisterEventHandler<EventBombBegindefuse>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null) return HookResult.Continue;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null) return HookResult.Continue;

            if (!IsPlayerValid(player) || !player.PawnIsAlive) return HookResult.Continue;

            var featureValue = GetValue(player);
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