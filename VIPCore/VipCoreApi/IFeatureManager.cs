namespace VipCoreApi;

/// <summary>
/// Defines methods for managing VIP features.
/// </summary>
public interface IFeatureManager
{
    /// <summary>
    /// Registers the specified VIP feature.
    /// </summary>
    /// <param name="feature">The feature to register.</param>
    void Register(VipFeature feature);

    /// <summary>
    /// Unregisters the specified VIP feature.
    /// </summary>
    /// <param name="feature">The feature to unregister.</param>
    void Unregister(VipFeature feature);

    /// <summary>
    /// Finds a registered VIP feature by its name.
    /// </summary>
    /// <param name="name">The name of the feature to search for.</param>
    /// <returns>The found feature, or <c>null</c> if not registered.</returns>
    VipFeature? FindByName(string name);

    /// <summary>
    /// Returns a list of all registered VIP features.
    /// </summary>
    /// <returns>A list of <see cref="VipFeature"/> instances.</returns>
    List<VipFeature> GetFeatures();
}