using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Bhop;

public class VIP_Bhop : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Bhop";
    public override string ModuleVersion => "v1.0.1";

    private Bhop _bhop;
    private IVipCoreApi? _api;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _bhop = new Bhop(this, _api);
        _api.RegisterFeature(_bhop);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_bhop);
    }
}

public class Bhop : VipFeatureBase
{
    private readonly VIP_Bhop _vipBhop;
    public override string Feature => "Bhop";
    private readonly BhopSettings[] _isBhopActive = new BhopSettings[70];

    public Bhop(VIP_Bhop vipBhop, IVipCoreApi api) : base(api)
    {
        _vipBhop = vipBhop;
        vipBhop.RegisterListener<Listeners.OnClientConnected>(slot => _isBhopActive[slot] = new BhopSettings());
        vipBhop.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isBhopActive[slot] = new BhopSettings());

        vipBhop.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (!_isBhopActive[player.Slot].Active) continue;

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
            var maxSpeed = _isBhopActive[player.Slot].MaxSpeed;
            if(Math.Round(playerPawn.AbsVelocity.Length2D()) > maxSpeed && maxSpeed is not 0)
                ChangeVelocity(playerPawn, maxSpeed);
            
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;

            if (buttons.HasFlag(PlayerButtons.Jump) && flags.HasFlag(PlayerFlags.FL_ONGROUND) &&
                !playerPawn.MoveType.HasFlag(MoveType_t.MOVETYPE_LADDER) && _isBhopActive[player.Slot].Active)
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
            _isBhopActive[player.Slot].Active = false;

            if (!IsClientVip(player)) continue;
            if (!PlayerHasFeature(player)) continue;
            if (GetPlayerFeatureState(player) is not FeatureState.Enabled) continue;

            
            var bhopSettings = GetFeatureValue<BhopSettings>(player);
            _isBhopActive[player.Slot].MaxSpeed = bhopSettings.MaxSpeed;

            PrintToChat(player, GetTranslatedText("bhop.TimeToActivation", bhopSettings.Timer));
            _vipBhop.AddTimer(bhopSettings.Timer + gamerules.FreezeTime, () =>
            {
                PrintToChat(player, GetTranslatedText("bhop.Activated"));
                _isBhopActive[player.Slot].Active = true;
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }

    private void ChangeVelocity(CCSPlayerPawn? pawn, float vel)
    {
        if (pawn == null) return;

        var currentVelocity = new Vector(pawn.AbsVelocity.X, pawn.AbsVelocity.Y, pawn.AbsVelocity.Z);
        var currentSpeed3D = Math.Sqrt(currentVelocity.X * currentVelocity.X + currentVelocity.Y * currentVelocity.Y + currentVelocity.Z * currentVelocity.Z);

        pawn.AbsVelocity.X = (float)(currentVelocity.X / currentSpeed3D) * vel;
        pawn.AbsVelocity.Y = (float)(currentVelocity.Y / currentSpeed3D) * vel;
        pawn.AbsVelocity.Z = (float)(currentVelocity.Z / currentSpeed3D) * vel;
    }
}

public class BhopSettings
{
    [JsonIgnore] public bool Active { get; set; }
    public float Timer { get; set; }
    public float MaxSpeed { get; set; }
}