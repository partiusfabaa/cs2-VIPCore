using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Gravity;

public class VipGravity : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Gravity";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private Gravity? _gravity;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _gravity = new Gravity(this, _api);
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
            foreach (var player in Utilities.GetPlayers()
                         .Where(u => u.IsValid &&
                                     IsPlayerVip(u) &&
                                     PlayerHasFeature(u) &&
                                     GetPlayerFeatureState(u) is FeatureState.Enabled))
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
        if (!vip || !PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawnValue = player.PlayerPawn.Value;

        if (playerPawnValue == null) return;

        playerPawnValue.GravityScale = GetValue(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        var state = GetPlayerFeatureState(player);
        Console.WriteLine(state);
        var playerPawnValue = player.PlayerPawn.Value;

        if (state == FeatureState.Disabled)
        {
            if (playerPawnValue != null)
                playerPawnValue.GravityScale = 1.0f;
            return;
        }

        if (playerPawnValue != null)
            playerPawnValue.GravityScale = GetValue(player);
    }
}