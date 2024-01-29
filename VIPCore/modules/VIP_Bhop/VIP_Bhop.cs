using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Modularity;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Bhop;

public class VIP_Bhop : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Bhop";
    public override string ModuleVersion => "v1.0.1";

    private Bhop _bhop;
    private IVipCoreApi _api = null!;
    
    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _bhop = new Bhop(this, _api);
        _api.RegisterFeature(_bhop);
    }
    
    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_bhop);
    }
}

public class Bhop : VipFeatureBase
{
    private readonly VIP_Bhop _vipBhop;
    public override string Feature => "Bhop";
    private bool?[] _isBhopActive = new bool?[65];

    public Bhop(VIP_Bhop vipBhop, IVipCoreApi api) : base(api)
    {
        _vipBhop = vipBhop;
        vipBhop.RegisterListener<Listeners.OnClientConnected>(slot => _isBhopActive[slot + 1] = false);
        vipBhop.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isBhopActive[slot + 1] = null);
        vipBhop.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (_isBhopActive[player.Index] == null) continue;
                if (!_isBhopActive[player.Index]!.Value) continue;

                OnTick(player);
            }
        });

        vipBhop.RegisterEventHandler<EventRoundStart>(EventRoundStart);
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

            if (!IsClientVip(player)) continue;
            if (!PlayerHasFeature(player)) continue;
            if (GetPlayerFeatureState(player) is not FeatureState.Enabled) continue;

            var bhopActiveTime = GetFeatureValue<float>(player);

            PrintToChat(player, GetTranslatedText("bhop.TimeToActivation", bhopActiveTime));
            _vipBhop.AddTimer(bhopActiveTime + gamerules.FreezeTime, () =>
            {
                PrintToChat(player, GetTranslatedText("bhop.Activated"));
                _isBhopActive[player.Index] = true;
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }
}