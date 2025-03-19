namespace VipCoreApi.Enums;

/// <summary>
/// Specifies the possible states for VIP features.
/// </summary>
public enum FeatureState
{
    /// <summary>
    /// The feature is enabled.
    /// </summary>
    Enabled,

    /// <summary>
    /// The feature is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// The player does not have access to the feature.
    /// </summary>
    NoAccess
}