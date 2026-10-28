using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;

namespace VIP_DecoyTeleport;

public class VipDecoyTeleport : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Decoy Teleport";
    public override string ModuleVersion => "v2.0.0";

    private DecoyTeleport? _decoyTeleport;
    private IVipCoreApi? _api;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _decoyTeleport = new DecoyTeleport(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _decoyTeleport?.Dispose();
    }
}

public class DecoyTeleport : VipFeature<int>
{
    private readonly int[] _decoyCount = new int[65];

    public DecoyTeleport(VipDecoyTeleport vipDecoyTeleport, IVipCoreApi api) : base("DecoyTp", api)
    {
        vipDecoyTeleport.RegisterEventHandler<EventDecoyFiring>(EventDecoyFiring);

        vipDecoyTeleport.RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            for (var i = 0; i < _decoyCount.Length; i ++)
                _decoyCount[i] = 0;

            return HookResult.Continue;
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip) return;
        if (!IsFeatureEnabled(player)) return;

        _decoyCount[player.Index] = 0;

        var playerPawn = player.PlayerPawn.Value;

        if (playerPawn != null && playerPawn.WeaponServices != null && playerPawn.WeaponServices.Ammo[17] == 0)
            player.GiveNamedItem("weapon_decoy");
    }

    private HookResult EventDecoyFiring(EventDecoyFiring @event, GameEventInfo info)
    {
        if (@event.Userid == null) return HookResult.Continue;

        var controller = @event.Userid;
        var entityIndex = controller.Index;

        if (!IsFeatureEnabled(controller)) return HookResult.Continue;

        var pDecoyFiring = @event;
        //var bodyComponent = controller.PlayerPawn.Value?.CBodyComponent?.SceneNode;
        var playerPawn = controller.PlayerPawn.Value;

        //if (bodyComponent == null) return HookResult.Continue;
        if (playerPawn == null) return HookResult.Continue;

        var decoysPerRound = GetValue(controller);
        if (_decoyCount[entityIndex] >= decoysPerRound && decoysPerRound > 0)
            return HookResult.Continue;

        //bodyComponent.AbsOrigin.X = pDecoyFiring.X;
        //bodyComponent.AbsOrigin.Y = pDecoyFiring.Y;
        //bodyComponent.AbsOrigin.Z = pDecoyFiring.Z;
        playerPawn.Teleport(new Vector(pDecoyFiring.X, pDecoyFiring.Y, pDecoyFiring.Z), playerPawn.AbsRotation,
            playerPawn.AbsVelocity);

        _decoyCount[entityIndex] ++;

        var decoyIndex = NativeAPI.GetEntityFromIndex(pDecoyFiring.Entityid);

        if (decoyIndex == IntPtr.Zero) return HookResult.Continue;

        new CBaseCSGrenadeProjectile(decoyIndex).Remove();

        return HookResult.Continue;
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State != FeatureState.Enabled) return;

        var limit = GetValue(args.Controller);
        args.Display = limit > 0
            ? $"[{Math.Max(limit - _decoyCount[args.Controller.Index], 0)}/{limit}]"
            : "[∞]";
    }
}