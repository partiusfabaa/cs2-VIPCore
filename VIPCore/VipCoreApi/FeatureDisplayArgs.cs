using CounterStrikeSharp.API.Core;
using VipCoreApi.Enums;

namespace VipCoreApi;

/// <summary>
/// Provides data for the event when a VIP feature is displayed.
/// </summary>
public class FeatureDisplayArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the player controller for whom the feature is displayed.
    /// </summary>
    public CCSPlayerController Controller { get; set; } 
        
    /// <summary>
    /// Gets or sets the feature that is being displayed.
    /// </summary>
    public VipFeature Feature { get; set; }
        
    /// <summary>
    /// Gets or sets the string used for displaying the feature.
    /// </summary>
    public string Display { get; set; } = string.Empty;
        
    /// <summary>
    /// Gets or sets the state of the feature during display.
    /// </summary>
    public FeatureState State { get; set; }
}