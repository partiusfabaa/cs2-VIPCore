using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.DependencyInjection;
using VIPCore.Configs;
using VIPCore.Player;
using VIPCore.Services;
using VipCoreApi;

namespace VIPCore;

public class VipServiceCollection : IPluginServiceCollection<Plugin>
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IFeatureManager, FeatureManager>();
        serviceCollection.AddSingleton<CommandsService>();
        
        serviceCollection.AddTransient<VipPlayer>();
        serviceCollection.AddSingleton<PlayersManager>();
        
        serviceCollection.AddSingleton<DatabaseProvider>();
        serviceCollection.AddSingleton<DatabaseService>();

        serviceCollection.AddSingleton<VipCoreApi>();
        serviceCollection.AddSingleton<Lazy<VipCoreApi>>(provider =>
            new Lazy<VipCoreApi>(provider.GetRequiredService<VipCoreApi>));

        serviceCollection.AddSingleton<IVipCoreApi>(provider => provider.GetRequiredService<VipCoreApi>());

        var configSystem = new ConfigSystem(serviceCollection);
        configSystem
            .AddConfig<VipConfig>("VIPCore", "vip_core")
            .AddConfig<GroupsConfig>("VIPCore", "vip");

        IFeature<Plugin>.Scan(serviceCollection);
    }
}