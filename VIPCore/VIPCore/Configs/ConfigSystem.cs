using System.Text.Encodings.Web;
using System.Text.Json;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;

namespace VIPCore.Configs;

public class ConfigSystem
{
    public static readonly string CoreConfigDirectory =
        Path.Combine(Application.RootDirectory, "configs/plugins/VIPCore/");

    public static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IServiceCollection _serviceCollection;

    public ConfigSystem(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;
        Directory.CreateDirectory(Path.Combine(CoreConfigDirectory, "Modules"));
    }

    public ConfigSystem AddConfig<T>(string fileName) where T : class, new()
    {
        _serviceCollection.AddSingleton(new Config<T>(Path.Combine(CoreConfigDirectory, $"{fileName}.json"), fileName));
        return this;
    }
}
