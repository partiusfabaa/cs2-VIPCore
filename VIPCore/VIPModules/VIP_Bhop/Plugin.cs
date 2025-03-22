using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Bhop;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Bhop";
    public override string ModuleVersion => "v2.0.0";

    private Bhop? _bhop;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _bhop = new Bhop(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _bhop?.Dispose();
    }
}

public class Bhop : VipFeature<BhopPlayer>
{
    private static readonly MemoryFunctionVoid<CCSPlayer_MovementServices, IntPtr>
        ProcessMovement = new(GameData.GetSignature("CCSPlayer_MovementServices_ProcessMovement"));

    private readonly Plugin _plugin;
    private readonly BhopPlayer[] _bhopPlayer = new BhopPlayer[70];

    private readonly ConVar? _autobunnyhopping = ConVar.Find("sv_autobunnyhopping");
    private readonly ConVar? _enablebunnyhopping = ConVar.Find("sv_enablebunnyhopping");

    private bool _wasAutobunnyhoppingChanged;
    private bool _wasEnablebunnyhoppingChanged;

    public Bhop(Plugin plugin, IVipCoreApi api) : base("Bhop", api)
    {
        _plugin = plugin;
        plugin.RegisterListener<Listeners.OnClientConnected>(slot => _bhopPlayer[slot] = new BhopPlayer());
        plugin.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _bhopPlayer[slot] = new BhopPlayer());

        plugin.RegisterEventHandler<EventRoundStart>(EventRoundStart);

        ProcessMovement.Hook(ProcessMovementPre, HookMode.Pre);
        ProcessMovement.Hook(ProcessMovementPost, HookMode.Post);

        plugin.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (!_bhopPlayer[player.Slot].Active) continue;

                OnTick(player);
            }
        });
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        SetBunnyhop(player, feature.State == FeatureState.Enabled);
    }

    private void SetBunnyhop(CCSPlayerController player, bool value)
    {
        player.ReplicateConVar("sv_autobunnyhopping", Convert.ToString(value));
        player.ReplicateConVar("sv_enablebunnyhopping", Convert.ToString(value));
        _bhopPlayer[player.Slot].Active = value;
    }

    private void OnTick(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        var maxSpeed = _bhopPlayer[player.Slot].MaxSpeed;
        if (Math.Round(playerPawn.AbsVelocity.Length2D()) > maxSpeed && maxSpeed is not 0)
            ChangeVelocity(playerPawn, maxSpeed);
    }

    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (_autobunnyhopping?.GetPrimitiveValue<bool>() == true) return HookResult.Continue;
 
        var gamerules = GetGameRules();
        if (gamerules == null || gamerules.WarmupPeriod)
            return HookResult.Continue;

        foreach (var player in Utilities.GetPlayers()
                     .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
        {
            SetBunnyhop(player, false);

            if (!IsPlayerValid(player))
                continue;

            var bhopSettings = GetValue(player);
            if (bhopSettings is null) continue;

            _bhopPlayer[player.Slot].MaxSpeed = bhopSettings.MaxSpeed;

            PrintToChat(player, GetTranslatedText(player, "bhop.TimeToActivation", bhopSettings.Timer));
            _plugin.AddTimer(bhopSettings.Timer + gamerules.FreezeTime, () =>
            {
                PrintToChat(player, GetTranslatedText(player, "bhop.Activated"));
                SetBunnyhop(player, true);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }

    private void ChangeVelocity(CCSPlayerPawn? pawn, float vel)
    {
        if (pawn == null) return;

        var currentVelocity = new Vector(pawn.AbsVelocity.X, pawn.AbsVelocity.Y, pawn.AbsVelocity.Z);
        var currentSpeed3D = Math.Sqrt(currentVelocity.X * currentVelocity.X + currentVelocity.Y * currentVelocity.Y +
                                       currentVelocity.Z * currentVelocity.Z);

        pawn.AbsVelocity.X = (float)(currentVelocity.X / currentSpeed3D) * vel;
        pawn.AbsVelocity.Y = (float)(currentVelocity.Y / currentSpeed3D) * vel;
        pawn.AbsVelocity.Z = (float)(currentVelocity.Z / currentSpeed3D) * vel;
    }

    private HookResult ProcessMovementPre(DynamicHook hook)
    {
        if (_autobunnyhopping == null || _enablebunnyhopping == null)
        {
            return HookResult.Continue;
        }

        _wasAutobunnyhoppingChanged = false;
        _wasEnablebunnyhoppingChanged = false;

        var movementServices = hook.GetParam<CCSPlayer_MovementServices>(0);
        if (!IsBhopEnabled(movementServices))
        {
            return HookResult.Continue;
        }

        if (!_autobunnyhopping.GetPrimitiveValue<bool>())
        {
            _autobunnyhopping.SetValue(true);
            _wasAutobunnyhoppingChanged = true;
        }

        if (!_enablebunnyhopping.GetPrimitiveValue<bool>())
        {
            _enablebunnyhopping.SetValue(true);
            _wasEnablebunnyhoppingChanged = true;
        }

        return HookResult.Continue;
    }

    private HookResult ProcessMovementPost(DynamicHook hook)
    {
        if (_autobunnyhopping == null || _enablebunnyhopping == null)
        {
            return HookResult.Continue;
        }

        if (_wasAutobunnyhoppingChanged)
        {
            _autobunnyhopping.SetValue(false);
            _wasAutobunnyhoppingChanged = false;
        }

        if (_wasEnablebunnyhoppingChanged)
        {
            _enablebunnyhopping.SetValue(false);
            _wasEnablebunnyhoppingChanged = false;
        }

        return HookResult.Continue;
    }

    private int? GetSlot(CCSPlayer_MovementServices? movementServices)
    {
        var index = movementServices?.Pawn.Value?.Controller.Value?.Index;
        if (index == null)
            return null;

        return (int)index.Value - 1;
    }

    private bool IsBhopEnabled(CCSPlayer_MovementServices movementServices)
    {
        var slot = GetSlot(movementServices);
        if (slot == null)
        {
            return false;
        }

        return _bhopPlayer[slot.Value].Active;
    }

    private CCSGameRules? GetGameRules()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").ToList();
        if (gameRules.Count < 1) return null;

        return gameRules.First(g => g.IsValid).GameRules;
    }

    public override void Dispose()
    {
        ProcessMovement.Unhook(ProcessMovementPre, HookMode.Pre);
        ProcessMovement.Unhook(ProcessMovementPost, HookMode.Post);
        base.Dispose();
    }
}

public class BhopPlayer
{
    [JsonIgnore] public bool Active { get; set; }
    public float Timer { get; set; }
    public float MaxSpeed { get; set; }
}