using Microsoft.Extensions.DependencyInjection;

namespace VIPCore;

public interface IFeature
{
}

public interface IFeature<T>
{
    private static List<Type> _features = [];

    public static void Scan(IServiceCollection collection)
    {
        foreach (var serviceType in typeof (T).Assembly.GetTypes().Where(t => t != typeof (IFeature) && t.IsAssignableTo(typeof (IFeature))))
        {
            _features.Add(serviceType);
            collection.AddSingleton(serviceType);
        }
    }

    public static void Instantiate(IServiceProvider provider)
    {
        foreach (var feature in _features)
            provider.GetRequiredService(feature);
        _features = null;
    }
}