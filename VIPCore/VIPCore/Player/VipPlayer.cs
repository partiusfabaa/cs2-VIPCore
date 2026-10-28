using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using VipCoreApi.Enums;

namespace VIPCore.Player;

public class VipPlayer(CCSPlayerController controller, SteamID steamId)
{
    public CCSPlayerController Controller { get; } = controller;
    public SteamID SteamId { get; } = steamId;
    public bool Disconnected { get; set; }

    public VipData? Data { get; set; }
    public Dictionary<string, object>? Features { get; set; }
    public bool IsTemporary { get; set; }

    public Dictionary<string, FeatureState> FeatureStates { get; } = new();
    public Dictionary<(string Feature, Type Type), object?> FeatureValues { get; } = new();

    [MemberNotNullWhen(true, nameof(Data), nameof(Features))]
    public bool IsVip => Data != null && Features != null && !Data.IsExpired;

    public string Group => Data?.Group ?? string.Empty;

    public bool HasFeature(string feature) =>
        Features != null && Features.TryGetValue(feature, out var value) && !string.IsNullOrEmpty(value.ToString());

    public void Clear()
    {
        Data = null;
        Features = null;
        IsTemporary = false;
        FeatureStates.Clear();
        FeatureValues.Clear();
    }
}
