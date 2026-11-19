using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Endurance;

public class VipEndurance : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Endurance";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private Endurance? _endurance;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _endurance = new Endurance(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _endurance?.Dispose();
    }
}

public class Endurance : VipFeature
{
    private readonly BasePlugin _plugin;
    private readonly IVipFeatureStateApi? _stateApi;
    private readonly Dictionary<int, CCSPlayerController> _players = new();
    private bool _mapEnding;
    private bool _tickRegistered;
    private bool _tickUpdatePending;
    private bool _disposed;

    public Endurance(BasePlugin basePlugin, IVipCoreApi api) : base("endurance", api)
    {
        _plugin = basePlugin;
        _stateApi = api as IVipFeatureStateApi;
        if (_stateApi != null) _stateApi.PlayerFeatureStatesChanged += UpdatePlayer;

        _plugin.RegisterListener<Listeners.OnClientDisconnectPost>(RemovePlayer);
        _plugin.RegisterListener<Listeners.OnMapStart>(OnMapStart);
        _plugin.RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        Server.NextWorldUpdate(RestorePlayers);
    }

    private void OnTick()
    {
        foreach (var player in _players.Values)
        {
            if (player is not { IsValid: true, Connected: PlayerConnectedState.Connected, PawnIsAlive: true })
                continue;

            if (player.PlayerPawn.Value is not { IsValid: true } pawn)
                continue;

            ref var modifier = ref pawn.VelocityModifier;
            if (modifier < 1.0f && IsFeatureEnabled(player))
                modifier = 1.0f;
        }
    }

    private void RestorePlayers()
    {
        if (_disposed || _mapEnding) return;

        foreach (var player in Utilities.GetPlayers())
            UpdatePlayer(player);
    }

    private void OnMapStart(string mapName)
    {
        _mapEnding = false;
        Server.NextWorldUpdate(RestorePlayers);
    }

    private void OnMapEnd()
    {
        _mapEnding = true;
        _players.Clear();
        UpdateTickListener();
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip) => UpdatePlayer(player);

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        if (player.IsValid) RemovePlayer(player.Slot);
    }

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (_disposed || _mapEnding || player is not { IsValid: true, IsBot: false, IsHLTV: false }) return;

        var state = _stateApi?.GetPlayerFeatureSelection(player, Name) ?? GetPlayerFeatureState(player);
        if (state is FeatureState.Enabled)
            _players[player.Slot] = player;
        else
            _players.Remove(player.Slot);
        UpdateTickListener();
    }

    private void RemovePlayer(int slot)
    {
        if (_players.Remove(slot)) UpdateTickListener();
    }

    private void UpdateTickListener()
    {
        if (_tickUpdatePending || _tickRegistered == (_players.Count > 0)) return;

        _tickUpdatePending = true;
        // Change the listener list outside the current dispatch.
        Server.NextWorldUpdate(() =>
        {
            _tickUpdatePending = false;
            var needsTick = _players.Count > 0;
            if (_disposed || _tickRegistered == needsTick) return;

            if (needsTick)
                _plugin.RegisterListener<Listeners.OnTick>(OnTick);
            else
                _plugin.RemoveListener<Listeners.OnTick>(OnTick);
            _tickRegistered = needsTick;
        });
    }

    public override void Dispose()
    {
        _disposed = true;
        _players.Clear();
        if (_tickRegistered) _plugin.RemoveListener<Listeners.OnTick>(OnTick);
        _plugin.RemoveListener<Listeners.OnClientDisconnectPost>(RemovePlayer);
        _plugin.RemoveListener<Listeners.OnMapStart>(OnMapStart);
        _plugin.RemoveListener<Listeners.OnMapEnd>(OnMapEnd);
        if (_stateApi != null) _stateApi.PlayerFeatureStatesChanged -= UpdatePlayer;
        base.Dispose();
    }
}
