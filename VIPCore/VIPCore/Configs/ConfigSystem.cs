using System.Text.Json;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VIPCore.Configs;

public class ConfigSystem
{
    private readonly IServiceCollection _serviceCollection;
    private readonly List<IConfig> _configs = new();

    public static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true, 
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public ConfigSystem(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;
        _serviceCollection.AddSingleton(this);
    }

    public ConfigSystem AddConfig<T>(string directory, string fileName) where T : class, new()
    {
        var directoryPath = Path.Combine(Application.RootDirectory, $"configs/plugins/{directory}/");
        Directory.CreateDirectory(directoryPath);
        var fullPath = directoryPath + $"{fileName}.json";

        var config = new Config<T>(fullPath, fileName);
        _serviceCollection.AddSingleton(config);

        _configs.Add(config);
        return this;
    }

    public void RegisterCommands(BasePlugin plugin, string cmd)
    {
        Console.WriteLine($"Register {cmd} command");
        plugin.AddCommand(cmd, "",
            (_, info) =>
            {
                foreach (var config in _configs)
                {
                    config.Load();
                    plugin.Logger.LogInformation("{config} reloaded", config.Name);
                }
            });
    }
}