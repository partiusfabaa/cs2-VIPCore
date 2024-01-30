using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_AntiFlash;

public class VipAntiFlash : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Anti Flash";
    public override string ModuleVersion => "v1.0.1";
    
    private IVipCoreApi _api = null!;
    private AntiFlash _antiFlash;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _antiFlash = new AntiFlash(this, _api);
        _api.RegisterFeature(_antiFlash);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_antiFlash);
    }
}

public class AntiFlash : VipFeatureBase
{
    public override string Feature => "Antiflash";

    public AntiFlash(VipAntiFlash vipAntiFlash, IVipCoreApi api) : base(api)
    {
        vipAntiFlash.RegisterEventHandler<EventPlayerBlind>((@event, info) =>
        {
            var player = @event.Userid;

            if (player == null) return HookResult.Continue;
            
            if (!IsClientVip(player)) return HookResult.Continue;
            if (!PlayerHasFeature(player)) return HookResult.Continue;
            if (GetPlayerFeatureState(player) is not IVipCoreApi.FeatureState.Enabled) return HookResult.Continue;

            var featureValue = GetFeatureValue<int>(player);
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