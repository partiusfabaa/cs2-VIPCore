using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;

namespace VIP_Respawn;

public class VipRespawn : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Respawn";
    public override string ModuleVersion => "v2.0.0";

    private Respawn? _respawn;
    private IVipCoreApi? _api;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api != null)
            _api.OnCoreReady += OnCoreReady;
    }

    private void OnCoreReady()
    {
        if (_api != null)
            _respawn ??= new Respawn(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null)
            _api.OnCoreReady -= OnCoreReady;

        _respawn?.Dispose();
    }
}

public class Respawn : VipFeature<int>
{
    private readonly Dictionary<ulong, (long Round, int Count)> _usedRespawns = new();
    private readonly HashSet<ulong> _respawning = new();
    private long _round;
    private int? _respawnOffset;
    private nint _respawnTarget;
    private Action<nint>? _invokeRespawn;

    public Respawn(VipRespawn plugin, IVipCoreApi api) : base("Respawn", api, FeatureType.Selectable)
    {
        api.OnPlayerAuthorized -= OnPlayerAuthorized;
        api.OnPlayerRemoved -= OnPlayerRemoved;
        api.OnPlayerDisconnect -= OnPlayerDisconnect;
        api.OnPlayerSpawn -= OnPlayerSpawn;
        api.OnPlayerUseFeature -= OnPlayerUseFeature;
        api.PlayerFeatureStatesChanged -= OnPlayerFeatureStatesChanged;

        plugin.RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _round++;
            _usedRespawns.Clear();
        });
        plugin.RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            _round++;
            return HookResult.Continue;
        });

        plugin.AddCommand("css_respawn", "", (player, _) => PlayerRespawn(player));
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature) => PlayerRespawn(player);

    private int GetUsedRespawns(ulong steamId) =>
        _usedRespawns.TryGetValue(steamId, out var usage) && usage.Round == _round ? usage.Count : 0;

    private void PlayerRespawn(CCSPlayerController? player)
    {
        if (player is not { IsValid: true } || !PlayerHasFeature(player) ||
            GetPlayerFeatureState(player) is FeatureState.NoAccess)
            return;

        var steamId = player.SteamID;
        if (_respawning.Contains(steamId)) return;

        var used = GetUsedRespawns(steamId);
        if (used >= GetValue(player))
        {
            PrintToChat(player, GetTranslatedText("respawn.Limit"));
            return;
        }

        if (player.Team is CsTeam.None or CsTeam.Spectator)
        {
            PrintToChat(player, GetTranslatedText("respawn.InTeam"));
            return;
        }

        if (player.PawnIsAlive)
        {
            PrintToChat(player, GetTranslatedText("respawn.IsAlive"));
            return;
        }

        if (player.PlayerPawn.Value is not { IsValid: true } pawn) return;

        var round = _round;
        _respawning.Add(steamId);
        try
        {
            player.SetPawn(pawn);
            if (!player.IsValid) return;

            InvokeRespawn(player.Handle);
            if (round == _round)
                _usedRespawns[steamId] = (round, used + 1);
        }
        finally
        {
            _respawning.Remove(steamId);
        }

        if (player.IsValid)
            PrintToChat(player, GetTranslatedText("respawn.Success"));
    }

    private void InvokeRespawn(nint controller)
    {
        var offset = _respawnOffset ?? GameData.GetOffset("CCSPlayerController_Respawn");
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        // CreateVoid binds the function at the current vtable entry.
        var vtable = Marshal.ReadIntPtr(controller);
        if (vtable == 0) throw new InvalidOperationException("Player controller has no virtual function table.");

        var target = Marshal.ReadIntPtr(vtable, checked(offset * IntPtr.Size));
        if (target == 0) throw new InvalidOperationException("Player controller has no respawn function.");

        if (_invokeRespawn == null || target != _respawnTarget)
        {
            _invokeRespawn = VirtualFunction.CreateVoid<nint>(controller, offset);
            _respawnOffset = offset;
            _respawnTarget = target;
        }

        _invokeRespawn(controller);
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        var limit = GetValue(args.Controller);
        var left = Math.Max(limit - GetUsedRespawns(args.Controller.SteamID), 0);

        args.Display = $"[{left}/{limit}]";
    }
}
