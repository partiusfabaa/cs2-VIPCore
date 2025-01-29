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
    public override string ModuleVersion => "v1.0.2";

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

public class Bhop : VipFeatureBase, IDisposable
{
    public override string Feature => "Bhop";

    private static readonly MemoryFunctionVoid<CCSPlayer_MovementServices, IntPtr>
        ProcessMovement = new(GameData.GetSignature("CCSPlayer_MovementServices_ProcessMovement"));

    private readonly VIP_Bhop _vipBhop;
    private readonly BhopSettings[] _isBhopActive = new BhopSettings[70];

    private readonly ConVar? _autobunnyhopping = ConVar.Find("sv_autobunnyhopping");
    private readonly ConVar? _enablebunnyhopping = ConVar.Find("sv_enablebunnyhopping");

    private bool _wasAutobunnyhoppingChanged;
    private bool _wasEnablebunnyhoppingChanged;

    public Bhop(VIP_Bhop vipBhop, IVipCoreApi api) : base(api)
    {
        _vipBhop = vipBhop;
        vipBhop.RegisterListener<Listeners.OnClientConnected>(slot => _isBhopActive[slot] = new BhopSettings());
        vipBhop.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _isBhopActive[slot] = new BhopSettings());

        vipBhop.RegisterEventHandler<EventRoundStart>(EventRoundStart);

        ProcessMovement.Hook(ProcessMovementPre, HookMode.Pre);
        ProcessMovement.Hook(ProcessMovementPost, HookMode.Post);

        vipBhop.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (!_isBhopActive[player.Slot].Active) continue;

                OnTick(player);
            }
        });
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        SetBunnyhop(player, state == FeatureState.Enabled);
    }

    private void SetBunnyhop(CCSPlayerController player, bool value)
    {
        player.ReplicateConVar("sv_autobunnyhopping", Convert.ToString(value));
        player.ReplicateConVar("sv_enablebunnyhopping", Convert.ToString(value));
        _isBhopActive[player.Slot].Active = value;
    }

    private void OnTick(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        var maxSpeed = _isBhopActive[player.Slot].MaxSpeed;
        if (Math.Round(playerPawn.AbsVelocity.Length2D()) > maxSpeed && maxSpeed is not 0)
            ChangeVelocity(playerPawn, maxSpeed);
    }

    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (_autobunnyhopping?.GetPrimitiveValue<bool>() == true) return HookResult.Continue;

        var gamerules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        if (gamerules == null || gamerules.WarmupPeriod)
            return HookResult.Continue;

        foreach (var player in Utilities.GetPlayers()
                     .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
        {
            SetBunnyhop(player, false);
            
            if (!IsClientVip(player) ||
                !PlayerHasFeature(player) ||
                GetPlayerFeatureState(player) is not FeatureState.Enabled) continue;

            var bhopSettings = GetFeatureValue<BhopSettings>(player);
            _isBhopActive[player.Slot].MaxSpeed = bhopSettings.MaxSpeed;

            PrintToChat(player, GetTranslatedText("bhop.TimeToActivation", bhopSettings.Timer));
            _vipBhop.AddTimer(bhopSettings.Timer + gamerules.FreezeTime, () =>
            {
                PrintToChat(player, GetTranslatedText("bhop.Activated"));
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

        return _isBhopActive[slot.Value].Active;
    }

    public void Dispose()
    {
        ProcessMovement.Unhook(ProcessMovementPre, HookMode.Pre);
        ProcessMovement.Unhook(ProcessMovementPost, HookMode.Post);
    }
}

public class BhopSettings
{
    [JsonIgnore] public bool Active { get; set; }
    public float Timer { get; set; }
    public float MaxSpeed { get; set; }
}