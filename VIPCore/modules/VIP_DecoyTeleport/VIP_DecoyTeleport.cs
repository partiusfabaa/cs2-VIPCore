using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Modularity;
using VipCoreApi;

namespace VIP_DecoyTeleport;

public class VipDecoyTeleport : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Decoy Teleport";
    public override string ModuleVersion => "v1.0.0";

    private DecoyTeleport _decoyTeleport;
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _decoyTeleport = new DecoyTeleport(this, _api);
        _api.RegisterFeature(_decoyTeleport);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_decoyTeleport);
    }
}

public class DecoyTeleport : VipFeatureBase
{
    public override string Feature => "DecoyTp";
    private readonly int[] _decoyCount = new int[65];

    public DecoyTeleport(VipDecoyTeleport vipDecoyTeleport, IVipCoreApi api) : base(api)
    {
        vipDecoyTeleport.RegisterEventHandler<EventDecoyFiring>(EventDecoyFiring);

        vipDecoyTeleport.RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            for (var i = 0; i < _decoyCount.Length; i ++)
                _decoyCount[i] = 0;

            return HookResult.Continue;
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not IVipCoreApi.FeatureState.Enabled) return;

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

        if (!IsClientVip(controller)) return HookResult.Continue;
        if (!PlayerHasFeature(controller)) return HookResult.Continue;
        if (GetPlayerFeatureState(controller) is not IVipCoreApi.FeatureState.Enabled) return HookResult.Continue;

        var pDecoyFiring = @event;
        //var bodyComponent = controller.PlayerPawn.Value?.CBodyComponent?.SceneNode;
        var playerPawn = controller.PlayerPawn.Value;

        //if (bodyComponent == null) return HookResult.Continue;
        if (playerPawn == null) return HookResult.Continue;

        var decoysPerRound = GetFeatureValue<int>(controller);
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
}