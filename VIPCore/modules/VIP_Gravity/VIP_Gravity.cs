using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;

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
                if (playerPawn is null || !player.PawnIsAlive ||
                    playerPawn.ActualMoveType is MoveType_t.MOVETYPE_LADDER) continue;

                SetGravity(playerPawn, GetValue(player));
            }
        });

        Server.NextWorldUpdate(() =>
        {
            foreach (var player in Utilities.GetPlayers())
                UpdatePlayer(player);
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        _players[player.Slot] = null;
        ResetGravity(player);
    }

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip) => _players[player.Slot] = null;

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (player is not { IsValid: true, IsBot: false }) return;

        _players[player.Slot] = IsFeatureEnabled(player) ? player : null;
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        UpdatePlayer(player);

        if (!vip || !IsFeatureEnabled(player)) return;

        var playerPawnValue = player.PlayerPawn.Value;

        if (playerPawnValue == null) return;

        SetGravity(playerPawnValue, GetValue(player));
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        UpdatePlayer(player);

        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null) return;

        if (GetPlayerFeatureState(player) is FeatureState.Enabled)
            SetGravity(playerPawnValue, GetValue(player));
        else
            SetGravity(playerPawnValue, 1.0f);
    }

    private static void ResetGravity(CCSPlayerController player)
    {
        if (player.IsValid && player.PlayerPawn.Value is { } playerPawn)
            SetGravity(playerPawn, 1.0f);
    }

    private static void SetGravity(CCSPlayerPawn playerPawn, float gravity)
    {
        if (Math.Abs(playerPawn.GravityScale - gravity) < 0.0001f &&
            Math.Abs(playerPawn.ActualGravityScale - gravity) < 0.0001f) return;

        playerPawn.GravityScale = gravity;
        playerPawn.ActualGravityScale = gravity;
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State != FeatureState.Enabled) return;

        var percent = (int)MathF.Round((GetValue(args.Controller) - 1) * 100);
        args.Display = percent > 0 ? $"[+{percent}%]" : $"[{percent}%]";
    }
}