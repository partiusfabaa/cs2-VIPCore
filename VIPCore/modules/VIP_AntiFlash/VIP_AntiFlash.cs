using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_AntiFlash;

public class VipAntiFlash : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Anti Flash";
    public override string ModuleVersion => "v1.0.2";
    
    private IVipCoreApi? _api;
    private AntiFlash _antiFlash;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _antiFlash = new AntiFlash(this, _api);
            _api.RegisterFeature(_antiFlash);
        };
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_antiFlash);
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

            var sameTeam = attacker.Team == player.Team;
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