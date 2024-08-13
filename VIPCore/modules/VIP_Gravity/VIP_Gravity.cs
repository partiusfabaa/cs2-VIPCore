using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Gravity;

public class VipGravity : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Gravity";
    public override string ModuleVersion => "1.0.1";

    private IVipCoreApi? _api;
    private Gravity _gravity;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _gravity = new Gravity(this, _api);
        _api.RegisterFeature(_gravity);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_gravity);
    }
}

public class Gravity : VipFeatureBase
{
    public override string Feature => "Gravity";

    private readonly MoveType_t[] _oldMoveType = new MoveType_t[70];

    public Gravity(VipGravity vipGravity, IVipCoreApi api) : base(api)
    {
        vipGravity.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(u => u.IsValid &&
                                     IsClientVip(u) &&
                                     PlayerHasFeature(u) &&
                                     GetPlayerFeatureState(u) is FeatureState.Enabled))
            {
                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn is null) return;

                if (_oldMoveType[player.Slot] is MoveType_t.MOVETYPE_LADDER &&
                    playerPawn.ActualMoveType is not MoveType_t.MOVETYPE_LADDER)
                {
                    playerPawn.GravityScale = GetFeatureValue<float>(player);
                }

                _oldMoveType[player.Slot] = playerPawn.ActualMoveType;
            }
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawnValue = player.PlayerPawn.Value;

        if (playerPawnValue == null) return;

        playerPawnValue.GravityScale = GetFeatureValue<float>(player);
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        Console.WriteLine(state);
        var playerPawnValue = player.PlayerPawn.Value;

        if (state == FeatureState.Disabled)
        {
            if (playerPawnValue != null)
                playerPawnValue.GravityScale = 1.0f;
            return;
        }

        if (playerPawnValue != null)
            playerPawnValue.GravityScale = GetFeatureValue<float>(player);
    }
}