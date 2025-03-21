using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Timers;
using FabiusTimer.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;
using VIPCore.Services;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIPCore;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => $"v{Assembly.GetAssembly(typeof(Plugin))?.GetName().Version?.ToString(3)}";

    private readonly Config<GroupsConfig> _groupsConfig;
    private readonly Config<VipConfig> _vipConfig;
    private DatabaseManager _databaseManager;
    private VipCoreApi _api;
    private PlayersManager _playersManager;
    private readonly IServiceProvider _serviceProvider;

    public Plugin(
        IServiceProvider serviceProvider,
        Config<VipConfig> vipConfig,
        Config<GroupsConfig> groupsConfig)
    {
        _serviceProvider = serviceProvider;

        _vipConfig = vipConfig;
        _groupsConfig = groupsConfig;
    }

    public override void Load(bool hotReload)
    {
        IFeature<Plugin>.Instantiate(_serviceProvider);
        DapperUtils.MapProperties<Plugin>();

        RegisterAllAttributes(_serviceProvider.GetRequiredService<CommandsService>());

        _databaseManager = _serviceProvider.GetRequiredService<DatabaseManager>();
        _playersManager = _serviceProvider.GetRequiredService<PlayersManager>();
        _api = _serviceProvider.GetRequiredService<VipCoreApi>();

        Capabilities.RegisterPluginCapability(IVipCoreApi.Capability, () => _api);

        Task.Run(() => _databaseManager.CreateTableAsync());

        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);

        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) => OnCreateMenuCmd(player));

        RegisterListener<Listeners.OnMapStart>(_ => _api.LoadCookies());
        RegisterListener<Listeners.OnMapEnd>(() => _api.SaveCookies());

        AddTimer(300.0f, () => { Task.Run(() => _databaseManager.PurgeExpiredUsersAsync()); }, TimerFlags.REPEAT);
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !_playersManager.TryGetPlayer(player, out var vipPlayer)) return HookResult.Continue;

        AddTimer(_vipConfig.Value.Delay, () => _api.InvokeOnPlayerSpawn(player, vipPlayer.IsVip));

        return HookResult.Continue;
    }

    private void OnCreateMenuCmd(CCSPlayerController? player)
    {
        if (player is null || !_playersManager.TryGetPlayer(player, out var vipPlayer))
            return;

        if (!vipPlayer.IsVip)
        {
            _playersManager.PrintToChat(vipPlayer, Localizer.ForPlayer(player, "vip.NoAccess"));
            return;
        }

        var features = new List<(string, VipFeature, FeatureState)>();
        foreach (var feature in _api.FeatureManager.GetFeatures().Where(f => f.Type != FeatureType.Hide))
        {
            if (!vipPlayer.FeatureStates.TryGetValue(feature, out var featureState)) continue;
            if (!feature.PlayerHasFeature(player) || feature.GetPlayerFeatureState(player) is FeatureState.NoAccess) continue;

            var value = string.Empty;
            if (feature.Type is FeatureType.Toggle)
            {
                value = featureState switch
                {
                    FeatureState.Enabled => Localizer.ForPlayer(player, "FeatureState.Enabled"),
                    FeatureState.Disabled => Localizer.ForPlayer(player, "FeatureState.Disabled"),
                    FeatureState.NoAccess => Localizer.ForPlayer(player, "FeatureState.NoAccess"),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            var displayArgs = new FeatureDisplayArgs
            {
                Controller = player,
                Feature = feature,
                Display = value,
                State = featureState
            };

            if (_api.PlayerHasFeature(player, feature.Name))
            {
                feature.OnFeatureDisplay(displayArgs);
            }

            features.Add((displayArgs.Display, feature, featureState));
        }

        CreateMenu(vipPlayer, features);
    }

    private void CreateMenu(VipPlayer vipPlayer, List<(string, VipFeature, FeatureState)> features)
    {
        var player = vipPlayer.Controller;
        if (player is null) return;

        var menu = _api.CreateMenu(Localizer.ForPlayer(player, "menu.Title", vipPlayer.Data?.Group ?? string.Empty));
        foreach (var (display, feature, state) in features)
        {
            menu.AddItem($"{Localizer.ForPlayer(player, feature.Name)} {display}", (p, _) =>
            {
                var args = new PlayerUseFeatureEventArgs
                {
                    State = state,
                    Controller = p,
                    Feature = feature
                };

                _api.InvokeOnPlayerUseFeature(args);

                if (!args.Allow)
                    return;

                var returnState = state;
                if (feature.Type != FeatureType.Selectable)
                {
                    returnState = state switch
                    {
                        FeatureState.Enabled => FeatureState.Disabled,
                        FeatureState.Disabled => FeatureState.Enabled,
                        _ => returnState
                    };
                }

                vipPlayer.FeatureStates[feature] = returnState;
                feature.State = returnState;
                feature.OnSelectItem(p, feature);

                if (feature.Type != FeatureType.Selectable && _vipConfig.Value.ReOpenMenuAfterItemClick)
                {
                    Server.NextFrame(() => OnCreateMenuCmd(p));
                }
            });
        }

        menu.Display(player, 0);
    }

    public void PrintToChatAll(string message)
    {
        foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot))
        {
            player.PrintToChat($"{Localizer.ForPlayer(player, "vip.Prefix")} {message}");
        }
    }

    public VipData CreateNewUser(int accountId, string name = "", string group = "", long time = -1)
    {
        if (time != -1)
        {
            time = time is 0 ? 0 : CalculateEndTimeInSeconds(time);
        }

        return new VipData
        {
            AccountId = accountId,
            Name = name,
            Group = group,
            Expires = time,
            ServerId = _vipConfig.Value.ServerId
        };
    }

    public void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            _playersManager.PrintToChat(controller, msg);
        else
            Logger.LogInformation($"{msg}");
    }

    public string TimeUnitName => _vipConfig.Value.TimeMode switch
    {
        0 => "second",
        1 => "minute",
        2 => "hours",
        3 => "days",
        _ => throw new KeyNotFoundException("No such number was found!")
    };

    public long CalculateEndTimeInSeconds(long time) => DateTimeOffset.UtcNow.AddSeconds(
        _vipConfig.Value.TimeMode switch
        {
            1 => time * 60,
            2 => time * 3600,
            3 => time * 86400,
            _ => time
        }).ToUnixTimeSeconds();
}