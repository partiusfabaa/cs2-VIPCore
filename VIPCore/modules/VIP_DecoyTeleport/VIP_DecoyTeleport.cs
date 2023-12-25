using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_DecoyTeleport;

public class VipDecoyTeleport : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Decoy Teleport";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "DecoyTp";
    private IVipCoreApi _api = null!;
    private readonly int?[] _decoyCount = new int?[65];

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_api.PlayerHasFeature(player, Feature)) return;
        if (_api.GetPlayerFeatureState(player, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        if (_decoyCount[player.Index] == null) return;

        _decoyCount[player.Index] = 0;

        var playerPawn = player.PlayerPawn.Value;

        if (playerPawn != null && playerPawn.WeaponServices != null && playerPawn.WeaponServices.Ammo[17] == 0)
            player.GiveNamedItem("weapon_decoy");
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientConnected>(slot => _decoyCount[slot + 1] = 0);
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => _decoyCount[slot + 1] = null);
        
        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            foreach (var players in Utilities.GetPlayers())
            {
                if (_decoyCount[players.Index] == null) continue;
                _decoyCount[players.Index] = 0;
            }

            return HookResult.Continue;
        });

        RegisterEventHandler<EventDecoyFiring>((@event, info) =>
        {
            if (@event.Userid == null) return HookResult.Continue;

            var controller = @event.Userid;
            var entityIndex = controller.Index;

            if (_decoyCount[entityIndex] == null) return HookResult.Continue;

            if (!_api.IsClientVip(controller)) return HookResult.Continue;
            if (!_api.PlayerHasFeature(controller, Feature)) return HookResult.Continue;
            if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess) return HookResult.Continue;

            var pDecoyFiring = @event;
            var bodyComponent = @event.Userid.PlayerPawn.Value?.CBodyComponent?.SceneNode;

            if (bodyComponent == null) return HookResult.Continue;

            var decoysPerRound = _api.GetFeatureValue<int>(controller, Feature);
            if (_decoyCount[entityIndex] >= decoysPerRound && decoysPerRound > 0)
                return HookResult.Continue;
            
            bodyComponent.AbsOrigin.X = pDecoyFiring.X;
            bodyComponent.AbsOrigin.Y = pDecoyFiring.Y;
            bodyComponent.AbsOrigin.Z = pDecoyFiring.Z;
            _decoyCount[entityIndex] ++;

            var decoyIndex = NativeAPI.GetEntityFromIndex(pDecoyFiring.Entityid);

            if (decoyIndex == IntPtr.Zero) return HookResult.Continue;

            new CBaseCSGrenadeProjectile(decoyIndex).Remove();

            return HookResult.Continue;
        });
    }
    
    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}