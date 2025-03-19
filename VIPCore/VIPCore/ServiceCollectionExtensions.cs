using System.Text.Json;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace VIPCore;

public static class ServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static IServiceCollection AddConfig<T>(
        this IServiceCollection collection,
        string directory,
        string fileName)
        where T : class, new()
    {
        var path1 = Path.Combine(Application.RootDirectory, "configs/plugins/" + directory + "/");
        Directory.CreateDirectory(path1);

        var path2 = path1 + fileName + ".json";

        collection.Configure<T>(options =>
        {
            if (File.Exists(path2))
            {
                var json = File.ReadAllText(path2);
                var configInstance = JsonSerializer.Deserialize<T>(json)!;
                foreach (var property in typeof(T).GetProperties())
                {
                    property.SetValue(options, property.GetValue(configInstance));
                }
            }
            else
            {
                var newConfigInstance = new T();
                var json = JsonSerializer.Serialize(newConfigInstance, ConfigJsonOptions);
                File.WriteAllText(path2, json);
            }
        });

        collection.AddSingleton(provider => provider.GetRequiredService<IOptionsMonitor<T>>().CurrentValue);

        return collection;
    }

    public static IServiceCollection AddConfigPart<TConfig, TPart>(
        this IServiceCollection collection,
        Func<TConfig, TPart> getter)
        where TConfig : notnull
        where TPart : class
    {
        collection.AddSingleton<TPart>(
            provider => getter(provider.GetRequiredService<TConfig>()));
        return collection;
    }
}

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