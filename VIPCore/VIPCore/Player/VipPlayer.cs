using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using VIPCore.Configs;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIPCore.Player;

public class VipPlayer : IDisposable
{
    public CCSPlayerController? Controller { get; set; }
    public required SteamID SteamId { get; set; }
    public bool Disconnected { get; set; }
    public VipData? Data { get; set; }
    public VipGroup? Group { get; set; }

    public Dictionary<VipFeature, FeatureState> FeatureStates { get; set; } = new();

    public bool IsVip => Group != null && Data != null &&
                         (Data.Expires is 0 || Data.Expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public event Action<VipPlayer>? OnDisconnect;

    public void Dispose()
    {
        OnDisconnect?.Invoke(this);
        
        Data = null;
        Group = null;
        FeatureStates.Clear();
    }
}