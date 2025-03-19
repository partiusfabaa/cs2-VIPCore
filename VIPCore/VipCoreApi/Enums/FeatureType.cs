namespace VipCoreApi.Enums;

/// <summary>
/// Specifies the types of VIP features.
/// </summary>
public enum FeatureType
{
    /// <summary>
    /// A feature that can be toggled on or off.
    /// </summary>
    Toggle,
        
    /// <summary>
    /// A feature with multiple selectable options.
    /// </summary>
    Selectable,
        
    /// <summary>
    /// A feature that is hidden from the user interface.
    /// </summary>
    Hide
}