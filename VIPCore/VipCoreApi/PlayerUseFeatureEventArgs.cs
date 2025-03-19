using CounterStrikeSharp.API.Core;
using VipCoreApi.Enums;

namespace VipCoreApi;

/// <summary>
/// Provides data for the event when a player uses a feature.
/// </summary>
public class PlayerUseFeatureEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the player controller for the player using the feature.
    /// </summary>
    public CCSPlayerController Controller { get; set; }
        
    /// <summary>
    /// Gets or sets the feature that was used.
    /// </summary>
    public VipFeature Feature { get; set; }
        
    /// <summary>
    /// Gets or sets the state of the feature at the time of use.
    /// </summary>
    public FeatureState State { get; set; }
        
    /// <summary>
    /// Gets or sets a value indicating whether the feature use is allowed.
    /// </summary>
    public bool Allow { get; set; } = true;
}