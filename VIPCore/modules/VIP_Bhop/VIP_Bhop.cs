using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Bhop;

public class VIP_Bhop : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Bhop";
    public override string ModuleVersion => "v1.0.3";

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
    public override string Feature => "Bhop";

    private readonly VIP_Bhop _vipBhop;
    private readonly BhopSettings[] _bhopSettings = new BhopSettings[70];

    public Bhop(VIP_Bhop vipBhop, IVipCoreApi api) : base(api)
    {
        _vipBhop = vipBhop;
        vipBhop.RegisterListener<Listeners.OnClientConnected>(slot => _bhopSettings[slot] = new BhopSettings());
        vipBhop.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _bhopSettings[slot] = new BhopSettings());

        vipBhop.RegisterEventHandler<EventRoundStart>(EventRoundStart);

        vipBhop.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                var settings = _bhopSettings[player.Slot];
                if (!settings.Active || !settings.Enabled) continue;

                OnTick(player);
            }
        });
    }

    public override void OnPlayerLoaded(CCSPlayerController player, string group)
    {
        if (PlayerHasFeature(player))
            _bhopSettings[player.Slot].Enabled = GetPlayerFeatureState(player) == FeatureState.Enabled;
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        _bhopSettings[player.Slot].Enabled = state == FeatureState.Enabled;
    }

    private void SetBunnyhop(CCSPlayerController player, bool value)
    {
        player.ReplicateConVar("sv_autobunnyhopping", Convert.ToString(value));
        player.ReplicateConVar("sv_enablebunnyhopping", Convert.ToString(value));
    }

    private void OnTick(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        var flags = (PlayerFlags)playerPawn.Flags;
        var buttons = player.Buttons;

        if (flags.HasFlag(PlayerFlags.FL_ONGROUND) && buttons.HasFlag(PlayerButtons.Jump))
        {
            var maxSpeed = _bhopSettings[player.Slot].MaxSpeed;
            if (Math.Round(playerPawn.AbsVelocity.Length2D()) > maxSpeed && maxSpeed is not 0)
                ChangeVelocity(playerPawn, maxSpeed);

            playerPawn.AbsVelocity.Z = 300;
            SetBunnyhop(player, true);
        }
    }

    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        var gamerules = GetGameRules();
        if (gamerules == null || gamerules.WarmupPeriod)
            return HookResult.Continue;

        foreach (var player in Utilities.GetPlayers()
                     .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
        {
            var settings = _bhopSettings[player.Slot];
            if (settings.Enabled)
            {
                settings.Active = false;

                if (!IsClientVip(player) ||
                    !PlayerHasFeature(player) ||
                    GetPlayerFeatureState(player) is not FeatureState.Enabled) continue;

                var bhopSettings = GetFeatureValue<BhopSettings>(player);
                settings.MaxSpeed = bhopSettings.MaxSpeed;

                PrintToChat(player, GetTranslatedText("bhop.TimeToActivation", bhopSettings.Timer));
                _vipBhop.AddTimer(bhopSettings.Timer + gamerules.FreezeTime, () =>
                {
                    PrintToChat(player, GetTranslatedText("bhop.Activated"));
                    settings.Active = true;
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }
        }

        return HookResult.Continue;
    }

    private void ChangeVelocity(CCSPlayerPawn? pawn, float vel)
    {
        if (pawn == null) return;

        var currentVelocity = new Vector(pawn.AbsVelocity.X, pawn.AbsVelocity.Y, pawn.AbsVelocity.Z);
        var currentSpeed = Math.Sqrt(currentVelocity.X * currentVelocity.X + currentVelocity.Y * currentVelocity.Y +
                                     currentVelocity.Z * currentVelocity.Z);

        pawn.AbsVelocity.X = (float)(currentVelocity.X / currentSpeed) * vel;
        pawn.AbsVelocity.Y = (float)(currentVelocity.Y / currentSpeed) * vel;
    }

    private CCSGameRules? GetGameRules()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").ToList();
        if (gameRules.Count < 1) return null;

        return gameRules.First(g => g.IsValid).GameRules;
    }
}

public class BhopSettings
{
    [JsonIgnore] public bool Active { get; set; }
    [JsonIgnore] public bool Enabled { get; set; }
    public float Timer { get; set; }
    public float MaxSpeed { get; set; }
}