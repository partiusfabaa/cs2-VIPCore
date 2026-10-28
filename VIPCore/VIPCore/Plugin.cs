using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;
using VIPCore.Services;
using VipCoreApi;

namespace VIPCore;

public class Plugin(IServiceProvider serviceProvider, Config<VipConfig> coreConfig, Config<GroupsConfig> groupsConfig)
    : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => $"v{typeof(Plugin).Assembly.GetName().Version!.ToString(3)}";

    public readonly FakeConVar<bool> EnableConVar = new("css_vip_enable", "", true);

    private VipCoreApi _api = null!;
    private PlayersManager _players = null!;
    private CookieService _cookies = null!;
    private MenuManager _menuManager = null!;

    public override void Load(bool hotReload)
    {
        DapperUtils.MapProperties<Plugin>();

        _cookies = serviceProvider.GetRequiredService<CookieService>();
        _cookies.Load();

        var database = serviceProvider.GetRequiredService<DatabaseService>();
        _players = serviceProvider.GetRequiredService<PlayersManager>();
        _api = serviceProvider.GetRequiredService<VipCoreApi>();
        _menuManager = serviceProvider.GetRequiredService<MenuManager>();
        RegisterAllAttributes(serviceProvider.GetRequiredService<CommandsService>());

        Capabilities.RegisterPluginCapability(IVipCoreApi.Capability, () => _api);

        Task.Run(database.CreateTablesAsync);

        RegisterListener<Listeners.OnClientAuthorized>(_players.OnClientAuthorized);
        RegisterListener<Listeners.OnClientDisconnect>(_players.OnClientDisconnect);
        RegisterListener<Listeners.OnMapEnd>(_cookies.Save);
        RegisterEventHandler<EventServerShutdown>((_, _) =>
        {
            _cookies.Save();
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);

        Server.NextWorldUpdate(() =>
        {
            AddTimer(30.0f, _players.RemoveExpired, TimerFlags.REPEAT);
            AddTimer(300.0f, () => Task.Run(database.PurgeExpiredUsersAsync), TimerFlags.REPEAT);

            _players.LoadConnectedPlayers();
            _api.InvokeOnCoreReady();
        });
    }

    public override void Unload(bool hotReload)
    {
        _cookies.Save();
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;

        AddTimer(coreConfig.Value.Delay, () =>
        {
            if (!player.IsValid || player.Connected != PlayerConnectedState.Connected) return;

            _api.InvokeOnPlayerSpawn(player, _api.IsPlayerVip(player));
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    public void ReloadConfigs()
    {
        coreConfig.Load();
        groupsConfig.Load();
        _menuManager.LoadSortOrder();
        _players.RefreshGroups();
    }

    public string Translate(CCSPlayerController player, string key, params object[] args) =>
        Localizer.ForPlayer(player, key, args);

    public void PrintToChat(CCSPlayerController player, string message)
    {
        if (!player.IsValid) return;

        player.PrintToChat($"{Translate(player, "vip.Prefix")} {message}");
    }

    public void PrintToChatAll(string message)
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
            PrintToChat(player, message);
    }

    public void ReplyToCommand(CCSPlayerController? controller, string message)
    {
        if (controller != null)
            PrintToChat(controller, message);
        else
            Server.PrintToConsole($"[VIPCore] {message}");
    }

    public void LogInfo(string message, params object?[] args)
    {
        if (coreConfig.Value.VipLogging)
            Logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object?[] args)
    {
        if (coreConfig.Value.VipLogging)
            Logger.LogWarning(message, args);
    }

    public string TimeUnitName => coreConfig.Value.TimeMode switch
    {
        1 => "minute",
        2 => "hours",
        3 => "days",
        _ => "second"
    };

    public long ExpiresIn(int time)
    {
        if (time == 0) return 0;

        return DateTimeOffset.UtcNow.AddSeconds(coreConfig.Value.TimeMode switch
        {
            1 => time * 60L,
            2 => time * 3600L,
            3 => time * 86400L,
            _ => time
        }).ToUnixTimeSeconds();
    }
}
