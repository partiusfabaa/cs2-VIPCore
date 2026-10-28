using VipCoreApi;

namespace VIPCore.Services;

public class FeatureManager(Plugin plugin) : IFeatureManager
{
    private readonly List<VipFeature> _features = [];

    public event Action<VipFeature>? FeatureRegistered;

    public void Register(VipFeature feature)
    {
        if (string.IsNullOrEmpty(feature.Name)) return;

        if (FindByName(feature.Name) is not null)
        {
            plugin.LogWarning("Feature '{feature}' is already registered", feature.Name);
            return;
        }

        _features.Add(feature);
        FeatureRegistered?.Invoke(feature);

        plugin.LogInfo("Feature '{feature}' registered successfully", feature.Name);
    }

    public void Unregister(VipFeature feature)
    {
        if (_features.Remove(feature))
            plugin.LogInfo("Feature '{feature}' unregistered successfully", feature.Name);
    }

    public VipFeature? FindByName(string name)
    {
        return _features.FirstOrDefault(f => f.Name == name);
    }

    public List<VipFeature> GetFeatures()
    {
        return _features;
    }
}
