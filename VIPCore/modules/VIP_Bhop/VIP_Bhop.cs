using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Modularity;
using VipCoreApi;

namespace VIP_Bhop;

public class VIP_Bhop : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Bhop";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "Bhop";
    private bool?[] _isBhopActive = new bool?[65];
    private IVipCoreApi _api = null!;

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientConnected>(slot => _isBhopActive[slot + 1] = false);
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isBhopActive[slot + 1] = null);
        RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (_isBhopActive[player.Index] == null) continue;
                if(!_isBhopActive[player.Index]!.Value) continue;

                OnTick(player);
            }
        });

        RegisterEventHandler<EventRoundStart>(EventRoundStart);
    }

    private void OnTick(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn != null)
        {
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;

            if (buttons.HasFlag(PlayerButtons.Jump) && flags.HasFlag(PlayerFlags.FL_ONGROUND) &&
                !playerPawn.MoveType.HasFlag(MoveType_t.MOVETYPE_LADDER) && _isBhopActive[player.Index]!.Value)
                playerPawn.AbsVelocity.Z = 300;
        }
    }

    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (ConVar.Find("sv_autobunnyhopping")!.GetPrimitiveValue<bool>()) return HookResult.Continue;

        if (Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!.WarmupPeriod)
            return HookResult.Continue;

        var gamerules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

        if (gamerules == null) return HookResult.Continue;
        
        foreach (var player in Utilities.GetPlayers()
                     .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
        {
            if (_isBhopActive[player.Index] == null) continue;
            _isBhopActive[player.Index] = false;
            
            if (!_api.IsClientVip(player)) continue;
            if (!_api.PlayerHasFeature(player, Feature)) continue;
            if (_api.GetPlayerFeatureState(player, Feature) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess) continue;

            var bhopActiveTime = _api.GetFeatureValue<float>(player, Feature);
        
            _api.PrintToChat(player, _api.GetTranslatedText("bhop.TimeToActivation", bhopActiveTime));
            AddTimer(bhopActiveTime + gamerules.FreezeTime, () =>
            {
                _api.PrintToChat(player, _api.GetTranslatedText("bhop.Activated"));
                _isBhopActive[player.Index] = true;
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }


    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}