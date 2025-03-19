using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Gravity;

public class VipGravity : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Gravity";
    public override string ModuleVersion => "v2.0.0";

    private Gravity? _gravity;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _gravity = new Gravity(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _gravity?.Dispose();
    }
}

public class Gravity : VipFeature<float>
{
    private readonly MoveType_t[] _oldMoveType = new MoveType_t[70];

    public Gravity(VipGravity vipGravity, IVipCoreApi api) : base("Gravity", api)
    {
        vipGravity.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers().Where(u => u.IsValid && IsPlayerValid(u)))
            {
                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn is null) return;

                if (_oldMoveType[player.Slot] is MoveType_t.MOVETYPE_LADDER &&
                    playerPawn.ActualMoveType is not MoveType_t.MOVETYPE_LADDER)
                {
                    playerPawn.GravityScale = GetValue(player);
                }

                _oldMoveType[player.Slot] = playerPawn.ActualMoveType;
            }
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!IsPlayerValid(player)) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        playerPawn.GravityScale = GetValue(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        var playerPawn = player.PlayerPawn.Value;

        if (feature.State == FeatureState.Disabled)
        {
            if (playerPawn != null)
                playerPawn.GravityScale = 1.0f;
            return;
        }

        if (playerPawn != null)
            playerPawn.GravityScale = GetValue(player);
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
        {
            args.Display = $"[{GetValue(args.Controller):0.0}]";
        }
    }
}