using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Endurance;

public class VipEndurance : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Endurance";
    public override string ModuleVersion => "v2.0.0";

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    private IVipCoreApi? _api;
    private Endurance? _endurance;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _endurance = new Endurance(this, _api);
        };
    }

    public override void Unload(bool hotReload)
    {
        _endurance?.Dispose();
    }
}

public class Endurance : VipFeature
{
    private readonly CCSPlayerController?[] _players = new CCSPlayerController?[65];

    public Endurance(BasePlugin basePlugin, IVipCoreApi api) : base("endurance", api)
    {
        basePlugin.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _players[slot] = null);
        basePlugin.RegisterListener<Listeners.OnTick>(() =>
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

                if (!IsFeatureEnabled(player))
                {
                    _players[slot] = null;
                    continue;
                }

                if (!player.PawnIsAlive) continue;

                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn == null) continue;

                if (playerPawn is { IsValid: true, VelocityModifier: < 1.0f })
                {
                    playerPawn.VelocityModifier = 1.0f;
                }
            }
        });

        Server.NextWorldUpdate(() =>
        {
            foreach (var player in Utilities.GetPlayers())
                UpdatePlayer(player);
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip) => UpdatePlayer(player);

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group) => _players[player.Slot] = null;

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip) => _players[player.Slot] = null;

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (player is not { IsValid: true, IsBot: false }) return;

        _players[player.Slot] = IsFeatureEnabled(player) ? player : null;
    }
}