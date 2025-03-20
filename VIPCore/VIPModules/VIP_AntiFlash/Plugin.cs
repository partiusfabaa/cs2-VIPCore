using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_AntiFlash;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Anti Flash";
    public override string ModuleVersion => "v2.0.0";

    private AntiFlash? _antiFlash;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _antiFlash = new AntiFlash(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _antiFlash?.Dispose();
    }
}

public class AntiFlash : VipFeature<int>
{
    public AntiFlash(Plugin plugin, IVipCoreApi api) : base("Antiflash", api)
    {
        plugin.RegisterEventHandler<EventPlayerBlind>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || !IsPlayerValid(player)) return HookResult.Continue;

            var featureValue = GetValue(player);
            var attacker = @event.Attacker;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null || playerPawn.LifeState is not (byte)LifeState_t.LIFE_ALIVE)
                return HookResult.Continue;

            var sameTeam = attacker?.Team == player.Team;
            switch (featureValue)
            {
                case 1:
                    if (sameTeam && player != attacker)
                        playerPawn.FlashDuration = 0.0f;
                    break;
                case 2:
                    if (player == attacker)
                        playerPawn.FlashDuration = 0.0f;
                    break;
                case 3:
                    if (sameTeam || player == attacker)
                        playerPawn.FlashDuration = 0.0f;
                    break;
                default:
                    playerPawn.FlashDuration = 0.0f;
                    break;
            }

            return HookResult.Continue;
        });
    }
}