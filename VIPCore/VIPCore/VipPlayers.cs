using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VIPCore.Extensions;
using VIPCore.Models;
using static VipCoreApi.IVipCoreApi;

namespace VIPCore;

public sealed class VipPlayer
{
    public required ulong SteamId64 { get; init; }
    public required int AccountId { get; init; }
    public required string Name { get; set; }
    public required string Group { get; set; }
    public int Expires { get; set; }
    public bool IsTemporary { get; init; }
    public Dictionary<string, FeatureState> FeatureStates { get; } = new();

    public bool IsExpired => Expires != 0 && Expires <= DateTime.UtcNow.GetUnixEpoch();

    public static VipPlayer FromUser(User user, ulong steamId64, bool isTemporary = false) => new()
    {
        SteamId64 = steamId64,
        AccountId = user.account_id,
        Name = user.name,
        Group = user.group,
        Expires = user.expires,
        IsTemporary = isTemporary
    };
}

public sealed class VipPlayers(VipCore core)
{
    private readonly ConcurrentDictionary<ulong, VipPlayer> _players = new();

    public bool TryGet(CCSPlayerController player, [NotNullWhen(true)] out VipPlayer? vip)
    {
        if (TryGetRecord(player, out vip) && !vip.IsExpired)
            return true;

        vip = null;
        return false;
    }

    public bool TryGetRecord(CCSPlayerController player, [NotNullWhen(true)] out VipPlayer? vip) =>
        _players.TryGetValue(player.SteamID, out vip);

    public bool IsVip(CCSPlayerController player) => core.IsCoreEnableConVar.Value && TryGet(player, out _);

    public void Set(CCSPlayerController player, VipPlayer vip)
    {
        _players[vip.SteamId64] = vip;
        core.ApplyFeatureStates(vip);
        core.VipApi.OnPlayerLoaded(player, vip.Group);
    }

    public bool Remove(CCSPlayerController player)
    {
        if (!TryGetRecord(player, out var vip))
            return false;

        core.VipApi.OnPlayerRemoved(player, vip.Group);
        _players.TryRemove(player.SteamID, out _);
        return true;
    }

    public void Forget(CCSPlayerController player) => _players.TryRemove(player.SteamID, out _);

    public IEnumerable<VipPlayer> Records => _players.Values;

    public IEnumerable<(CCSPlayerController Player, VipPlayer Vip)> Online()
    {
        if (_players.IsEmpty) yield break;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is { IsValid: true, IsBot: false } && TryGetRecord(player, out var vip))
                yield return (player, vip);
        }
    }
}

public sealed class FeatureRegistry
{
    private readonly ConcurrentDictionary<string, Feature> _features = new();
    private readonly HashSet<string> _forcedDisabled = new();

    public IEnumerable<KeyValuePair<string, Feature>> All => _features;
    public IEnumerable<string> Names => _features.Keys;

    public void Register(string name, Feature feature) => _features[name] = feature;
    public void Unregister(string name) => _features.TryRemove(name, out _);

    public bool TryGet(string name, [NotNullWhen(true)] out Feature? feature) =>
        _features.TryGetValue(name, out feature);

    public bool IsForcedDisabled(string name) => _forcedDisabled.Contains(name);

    public void DisableAll()
    {
        _forcedDisabled.Clear();
        _forcedDisabled.UnionWith(_features.Keys);
    }

    public void EnableAll() => _forcedDisabled.Clear();
}
