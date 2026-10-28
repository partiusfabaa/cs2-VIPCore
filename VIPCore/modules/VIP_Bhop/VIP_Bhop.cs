using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Bhop;

public class VIP_Bhop : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Bhop";
    public override string ModuleVersion => "v2.0.0";

    private Bhop? _bhop;
    private IVipCoreApi? _api;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _bhop = new Bhop(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _bhop?.Dispose();
    }
}

public class Bhop : VipFeature<BhopSettings>
{
    private const float SpeedCapSmoothing = 0.5f;

    private readonly VIP_Bhop _vipBhop;
    private readonly BhopSettings[] _bhopSettings = new BhopSettings[70];
    private readonly CCSPlayerController?[] _players = new CCSPlayerController?[65];
    private readonly bool[] _bunnyhopReplicated = new bool[65];

    public Bhop(VIP_Bhop vipBhop, IVipCoreApi api) : base("Bhop", api)
    {
        _vipBhop = vipBhop;
        for (var i = 0; i < _bhopSettings.Length; i++)
            _bhopSettings[i] = new BhopSettings();

        vipBhop.RegisterListener<Listeners.OnClientConnected>(slot => _bhopSettings[slot] = new BhopSettings());
        vipBhop.RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            _bhopSettings[slot] = new BhopSettings();
            _players[slot] = null;
            _bunnyhopReplicated[slot] = false;
        });

        vipBhop.RegisterEventHandler<EventRoundStart>(EventRoundStart);

        vipBhop.RegisterListener<Listeners.OnTick>(() =>
        {
            for (var slot = 0; slot < _players.Length; slot++)
            {
                var player = _players[slot];
                if (player is null) continue;

                if (!player.IsValid)
                {
                    _players[slot] = null;
                    continue;
                }

                if (!IsFeatureEnabled(player))
                {
                    RemovePlayer(player);
                    continue;
                }

                if (!player.PawnIsAlive || !_bhopSettings[slot].Active) continue;

                OnTick(player);
            }
        });

        Server.NextWorldUpdate(() =>
        {
            foreach (var player in Utilities.GetPlayers())
                UpdatePlayer(player);
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip) => UpdatePlayer(player);

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group) => RemovePlayer(player);

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip) => RemovePlayer(player);

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (player is not { IsValid: true, IsBot: false }) return;

        if (!IsFeatureEnabled(player))
        {
            RemovePlayer(player);
            return;
        }

        _players[player.Slot] = player;
        _bhopSettings[player.Slot].Enabled = true;
    }

    private void RemovePlayer(CCSPlayerController player)
    {
        if (player.Slot is < 0 or >= 65) return;

        _players[player.Slot] = null;
        _bhopSettings[player.Slot].Enabled = false;

        if (player.IsValid)
            SetBunnyhop(player, false);
    }

    private void SetBunnyhop(CCSPlayerController player, bool value)
    {
        if (_bunnyhopReplicated[player.Slot] == value) return;
        _bunnyhopReplicated[player.Slot] = value;

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
            var speed = playerPawn.AbsVelocity.Length2D();
            if (maxSpeed > 0 && speed > maxSpeed)
                ChangeVelocity(playerPawn, maxSpeed + (speed - maxSpeed) * SpeedCapSmoothing);

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

                if (!IsFeatureEnabled(player)) continue;

                SetBunnyhop(player, false);

                var bhopSettings = GetValue(player);
                if (bhopSettings is null) continue;

                settings.MaxSpeed = bhopSettings.MaxSpeed;

                PrintToChat(player, GetTranslatedText("bhop.TimeToActivation", bhopSettings.Timer));
                _vipBhop.AddTimer(bhopSettings.Timer + gamerules.FreezeTime, () =>
                {
                    if (!player.IsValid || _players[player.Slot] is null || !IsFeatureEnabled(player)) return;

                    PrintToChat(player, GetTranslatedText("bhop.Activated"));
                    settings.Active = true;
                    SetBunnyhop(player, true);
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }
        }

        return HookResult.Continue;
    }

    private void ChangeVelocity(CCSPlayerPawn? pawn, float vel)
    {
        if (pawn == null) return;

        var currentSpeed = pawn.AbsVelocity.Length2D();
        if (currentSpeed <= 0) return;

        var scale = vel / currentSpeed;
        pawn.AbsVelocity.X *= scale;
        pawn.AbsVelocity.Y *= scale;
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