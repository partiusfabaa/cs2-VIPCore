using Microsoft.Extensions.Logging;
using VipCoreApi;

namespace VIPCore.Services;

public class FeatureManager(Plugin plugin) : IFeatureManager
{
    private readonly List<VipFeature> _registeredFeatures = [];

    public void Register(VipFeature feature)
    {
        if (_registeredFeatures.Any(f => f.Name == feature.Name))
            return;

        _registeredFeatures.Add(feature);
        plugin.Logger.LogInformation("Feature '{feature}' registered successfully", feature.Name);
    }

    public void Unregister(VipFeature feature)
    {
        if (_registeredFeatures.Remove(feature))
        {
            plugin.Logger.LogInformation("Feature '{feature}' unregistered successfully", feature.Name);
        }
    }

    public VipFeature? FindByName(string name)
    {
        return _registeredFeatures.FirstOrDefault(f => f.Name == name);
    }

    public List<VipFeature> GetFeatures()
    {
        return _registeredFeatures;
    }
}