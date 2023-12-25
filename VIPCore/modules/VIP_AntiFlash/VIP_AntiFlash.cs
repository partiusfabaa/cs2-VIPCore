using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_AntiFlash;

public class VipAntiFlash : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Anti Flash";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "Antiflash";
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerBlind>((@event, info) =>
        {
            var player = @event.Userid;
            if (!_api.IsClientVip(player)) return HookResult.Continue;
            if (!_api.PlayerHasFeature(player, Feature)) return HookResult.Continue;
            if (_api.GetPlayerFeatureState(player, Feature) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess) return HookResult.Continue;
            
            var featureValue = _api.GetFeatureValue<int>(player, Feature);
            var attacker = @event.Attacker;

            var playerPawn = player.PlayerPawn.Value;

            if (playerPawn == null) return HookResult.Continue;

            var sameTeam = attacker.TeamNum == player.TeamNum;
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
                    if (sameTeam && player == attacker)
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