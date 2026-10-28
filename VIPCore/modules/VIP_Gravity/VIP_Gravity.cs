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
    private readonly CCSPlayerController?[] _players = new CCSPlayerController?[65];

    public Gravity(VipGravity vipGravity, IVipCoreApi api) : base("Gravity", api)
    {
        vipGravity.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _players[slot] = null);
        vipGravity.RegisterListener<Listeners.OnTick>(() =>
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

                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn is null) continue;

                if (_oldMoveType[slot] is MoveType_t.MOVETYPE_LADDER &&
                    playerPawn.ActualMoveType is not MoveType_t.MOVETYPE_LADDER)
                {
                    playerPawn.GravityScale = GetValue(player);
                }

                _oldMoveType[slot] = playerPawn.ActualMoveType;
            }
        });

        Server.NextWorldUpdate(() =>
        {
            foreach (var player in Utilities.GetPlayers())
                UpdatePlayer(player);
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group) => _players[player.Slot] = null;

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip) => _players[player.Slot] = null;

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (player is not { IsValid: true, IsBot: false }) return;

        _players[player.Slot] = IsPlayerValid(player) ? player : null;
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        UpdatePlayer(player);

        if (!vip || !PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawnValue = player.PlayerPawn.Value;

        if (playerPawnValue == null) return;

        playerPawnValue.GravityScale = GetValue(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        UpdatePlayer(player);

        var state = GetPlayerFeatureState(player);
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