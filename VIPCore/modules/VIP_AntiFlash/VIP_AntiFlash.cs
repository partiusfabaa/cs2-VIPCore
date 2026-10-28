using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;

namespace VIP_AntiFlash;

public class VipAntiFlash : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Anti Flash";
    public override string ModuleVersion => "v2.0.0";
   
    private IVipCoreApi? _api;
    private AntiFlash? _antiFlash;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _antiFlash = new AntiFlash(this, _api);
        };
    }

    public override void Unload(bool hotReload)
    {
        _antiFlash?.Dispose();
    }
}

public class AntiFlash : VipFeature<int>
{
    public AntiFlash(VipAntiFlash vipAntiFlash, IVipCoreApi api) : base("Antiflash", api)
    {
        vipAntiFlash.RegisterEventHandler<EventPlayerBlind>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return HookResult.Continue;

            if (!IsFeatureEnabled(player)) return HookResult.Continue;

            var featureValue = GetValue(player);
            var attacker = @event.Attacker;
            var playerPawn = player.PlayerPawn.Value;

            if (playerPawn == null || playerPawn.LifeState is not (byte)LifeState_t.LIFE_ALIVE) return HookResult.Continue;

            var sameTeam = attacker != null && attacker.Team == player.Team;

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

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State != FeatureState.Enabled) return;

        var mode = GetValue(args.Controller) switch
        {
            1 => "antiflash.Team",
            2 => "antiflash.Self",
            3 => "antiflash.TeamSelf",
            _ => "antiflash.All"
        };

        args.Display = $"[{GetTranslatedText(args.Controller, mode)}]";
    }
}