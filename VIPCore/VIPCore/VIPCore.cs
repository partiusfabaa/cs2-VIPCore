using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using VIPCore.Extensions;
using VIPCore.Models;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIPCore;

public class VipCore : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => $"v{typeof(VipCore).Assembly.GetName().Version!.ToString(3)}";

    public Config Config { get; set; } = null!;
    public CoreConfig CoreConfig { get; set; } = null!;
    public VipCoreApi VipApi { get; set; } = null!;

    public Database Database = null!;

    public VipPlayers Players { get; private set; } = null!;
    public readonly FeatureRegistry FeatureRegistry = new();

    private readonly PluginCapability<IVipCoreApi> _pluginCapability = new("vipcore:core");

    public readonly FakeConVar<bool> IsCoreEnableConVar = new("css_vip_enable", "", true);

    public string DbConnectionString = string.Empty;

    private string[] _sortedItems = [];

    public override void Load(bool hotReload)
    {
        Players = new VipPlayers(this);
        VipApi = new VipCoreApi(this);
        Capabilities.RegisterPluginCapability(_pluginCapability, () => VipApi);
        Server.NextWorldUpdate(() => VipApi.CoreReady());

        LoadConfig();

        DbConnectionString = BuildConnectionString();
        Database = new Database(this, Logger, DbConnectionString);

        Task.Run(() => Database.CreateTable());

        RegisterEventHandlers();
        Server.NextWorldUpdate(SetupTimers);

        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) => CreateMenu(player));
    }

    private void LoadConfig()
    {
        var coreConfigDirectory = VipApi.CoreConfigDirectory;

        Config = VipApi.LoadConfig<Config>("vip", coreConfigDirectory);
        CoreConfig = VipApi.LoadConfig<CoreConfig>("vip_core", coreConfigDirectory);

        var sortMenuPath = Path.Combine(coreConfigDirectory, "sort_menu.txt");

        if (!File.Exists(sortMenuPath))
            File.WriteAllLines(sortMenuPath, ["feature1", "feature2"]);

        _sortedItems = File.ReadAllLines(sortMenuPath);
    }

    private void RegisterEventHandlers()
    {
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is null || !player.IsValid) return;

            Task.Run(() => OnClientAuthorizedAsync(player, id));
        });

        RegisterListener<Listeners.OnMapStart>(_ => VipApi.LoadCookies());
        RegisterListener<Listeners.OnMapEnd>(() => VipApi.SaveCookies());
        RegisterEventHandler<EventServerShutdown>((@event, info) =>
        {
            VipApi.SaveCookies();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>(EventPlayerDisconnect);
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
    }

    private HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !Players.TryGetRecord(player, out var vip))
            return HookResult.Continue;

        foreach (var (key, state) in vip.FeatureStates)
        {
            if (FeatureRegistry.TryGet(key, out var feature) && feature.FeatureType is FeatureType.Toggle)
                VipApi.SetPlayerCookie(player.SteamID, key, (int)state);
        }

        Players.Forget(player);

        if (vip.IsTemporary) return HookResult.Continue;

        var authAccId = player.AuthorizedSteamID;
        if (authAccId == null) return HookResult.Continue;

        var playerName = player.PlayerName;
        Task.Run(() => Database.UpdateUserVip(authAccId.AccountId, name: playerName));

        return HookResult.Continue;
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.Handle == IntPtr.Zero || player.UserId == null)
            return HookResult.Continue;

        if (player.IsBot || !Players.IsVip(player))
            return HookResult.Continue;

        AddTimer(Config.Delay, () =>
        {
            if (player.Connected != PlayerConnectedState.Connected) return;

            try
            {
                VipApi.PlayerSpawn(player);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception in VipApi.PlayerSpawn: {ex}");
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    private void SetupTimers()
    {
        AddTimer(30.0f, RemoveExpiredVips, TimerFlags.REPEAT);
    }

    private void RemoveExpiredVips()
    {
        foreach (var (player, vip) in Players.Online().ToList())
        {
            if (!vip.IsExpired) continue;

            Players.Remove(player);
            PrintToChat(player, Localizer["vip.Expired", vip.Group]);
            PrintLogInfo("User '{name} [{accId}]' has been removed due to expired VIP status.", vip.Name,
                vip.AccountId);

            if (vip.IsTemporary) continue;

            var accountId = vip.AccountId;
            Task.Run(() => Database.RemoveExpiredUser(accountId));
        }
    }

    public async Task OnClientAuthorizedAsync(CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await Database.RemoveExpiredUser(steamId.AccountId);

            var usersFromDb = await Database.GetUserFromDb(steamId.AccountId);
            if (usersFromDb == null) return;

            var user = usersFromDb.OfType<User>().FirstOrDefault(u => u.sid == CoreConfig.ServerId);

            await Server.NextFrameAsync(() =>
            {
                if (!player.IsValid || player.SteamID != steamId.SteamId64) return;

                if (user == null)
                {
                    if (Players.TryGetRecord(player, out var current) && !current.IsTemporary)
                        Players.Remove(player);
                    return;
                }

                var vip = VipPlayer.FromUser(user, steamId.SteamId64);
                Players.Set(player, vip);

                AddTimer(5.0f, () => PrintWelcome(player, vip));
            });
        }
        catch (Exception e)
        {
            Logger.LogError(e.ToString());
        }
    }

    public void ApplyFeatureStates(VipPlayer vip, bool onlyMissing = false)
    {
        if (!onlyMissing)
            vip.FeatureStates.Clear();

        Config.Groups.TryGetValue(vip.Group, out var group);

        foreach (var key in FeatureRegistry.Names)
        {
            if (onlyMissing && vip.FeatureStates.ContainsKey(key)) continue;

            if (group == null || !group.Values.TryGetValue(key, out var value) ||
                string.IsNullOrEmpty(value.ToString()))
            {
                vip.FeatureStates[key] = FeatureState.NoAccess;
                continue;
            }

            var cookie = VipApi.GetPlayerCookie<int>(vip.SteamId64, key);
            vip.FeatureStates[key] = (FeatureState)(cookie == 2 ? 0 : cookie);
        }
    }

    public User CreateNewUser(int accountId, string username, string group, int endTime)
    {
        return new User
        {
            account_id = accountId,
            name = username,
            lastvisit = DateTime.UtcNow.GetUnixEpoch(),
            sid = CoreConfig.ServerId,
            group = group,
            expires = endTime == 0 ? 0 : CalculateEndTimeInSeconds(endTime)
        };
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_adduser")]
    public void OnCmdAddUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount != 4)
        {
            ReplyToCommand(controller,
                $"Usage: css_vip_adduser <steamid or accountid> <group> <time_{GetTimeUnitName}>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
        {
            ReplyToCommand(controller, $"Invalid SteamID/AccountID: {command.GetArg(1)}");
            return;
        }

        var vipGroup = command.GetArg(2);
        var endVipTime = Convert.ToInt32(command.GetArg(3));

        if (!Config.Groups.ContainsKey(vipGroup))
        {
            ReplyToCommand(controller, $"VIP group '{vipGroup}' was not found!");
            return;
        }

        var username = player == null ? "unknown" : player.PlayerName;

        var user = CreateNewUser(accountId, username, vipGroup, endVipTime);

        AddVip(player, user);

        if (player != null && Players.TryGetRecord(player, out var vip))
            PrintWelcome(player, vip);

        var timeText = endVipTime == 0 ? "permanent" : $"{endVipTime} {GetTimeUnitName}";
        ReplyToCommand(controller,
            $"VIP added: {username} (AccountID {accountId}) -> group '{vipGroup}', time: {timeText}.");
    }

    public void AddVip(CCSPlayerController? player, User user) // :)
    {
        Task.Run(() => Database.AddUserToDb(user));

        if (player == null) return;

        Players.Set(player, VipPlayer.FromUser(user, player.SteamID));
    }

    public void UpdateOnlineVip(CCSPlayerController player, VipPlayer vip, string name, string group, int time,
        bool notify = false)
    {
        if (!string.IsNullOrEmpty(name))
            vip.Name = name;

        if (!string.IsNullOrEmpty(group) && group != vip.Group)
        {
            vip.Group = group;
            Players.Set(player, vip);

            if (notify)
                PrintToChat(player, Localizer["vip.GroupChanged", vip.Group]);
        }

        if (time > -1)
        {
            vip.Expires = time == 0 ? 0 : CalculateEndTimeInSeconds(time);

            if (notify)
                PrintToChat(player, $"{Localizer["vip.TimeChanged"]} {ExpiresText(vip.Expires)}");
        }
    }

    public void PrintWelcome(CCSPlayerController player, VipPlayer vip)
    {
        var welcome = Localizer["vip.WelcomeToTheServer", vip.Name].Value.TrimEnd();
        PrintToChat(player, vip.Expires == 0
            ? welcome
            : $"{welcome} {Localizer["vip.Expires", vip.Group, FormatTime(vip.Expires)]}");
    }

    public void NotifyRemoved(CCSPlayerController player, VipPlayer vip)
    {
        PrintToChat(player, Localizer["vip.Removed", vip.Group]);
    }

    private string ExpiresText(int expires)
    {
        return expires == 0
            ? Localizer["vip.Permanent"]
            : Localizer["vip.Until", FormatTime(expires)];
    }

    private static string FormatTime(int unixTime)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixTime).ToString("G");
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_deleteuser")]
    public void OnCmdDeleteVipUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount != 2)
        {
            ReplyToCommand(controller, "Usage: css_vip_deleteuser <steamid or accountid>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
        {
            ReplyToCommand(controller, $"Invalid SteamID/AccountID: {command.GetArg(1)}");
            return;
        }

        RemoveVip(player, accountId);

        ReplyToCommand(controller,
            $"VIP removed: AccountID {accountId}{(player != null ? $" ({player.PlayerName})" : "")}.");
    }

    public void RemoveVip(CCSPlayerController? player, int accountId) // :)
    {
        if (player != null && Players.TryGetRecord(player, out var vip) && Players.Remove(player))
            NotifyRemoved(player, vip);

        Task.Run(() => Database.RemoveUserFromDb(accountId));
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_updateuser")]
    public void OnCmdUpdateUserGroup(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount != 4)
        {
            ReplyToCommand(controller, "Usage: css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>");
            ReplyToCommand(controller,
                "Use '-' or '-s' to keep a field unchanged. Example: css_vip_updateuser \"STEAM_0:0:123456\" -s 3600");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
        {
            ReplyToCommand(controller, $"Invalid SteamID/AccountID: {command.GetArg(1)}");
            return;
        }

        var vipGroup = command.GetArg(2);

        if (vipGroup is not ("-" or "-s"))
        {
            if (!Config.Groups.ContainsKey(vipGroup))
            {
                ReplyToCommand(controller, $"VIP group '{vipGroup}' was not found!");
                return;
            }
        }
        else
            vipGroup = string.Empty;

        var time = int.TryParse(command.GetArg(3), out var arg) ? arg : -1;

        if (player != null && Players.TryGetRecord(player, out var vip))
            UpdateOnlineVip(player, vip, string.Empty, vipGroup, time, notify: true);

        Task.Run(() => Database.UpdateUserVip(accountId, group: vipGroup, time: time));

        var groupText = string.IsNullOrEmpty(vipGroup) ? "unchanged" : $"'{vipGroup}'";
        var timeText = time == -1 ? "unchanged" : (time == 0 ? "permanent" : $"{time} {GetTimeUnitName}");
        ReplyToCommand(controller, $"VIP updated: AccountID {accountId} -> group: {groupText}, time: {timeText}.");
    }

    [RequiresPermissions("@css/root")]
    [CommandHelper(1, "<steamid>")]
    [ConsoleCommand("css_reload_vip_player")]
    public void OnCommandVipReloadInfractions(CCSPlayerController? player, CommandInfo command)
    {
        var target = Utils.GetPlayerFromSteamId(command.GetArg(1));

        if (target == null) return;
        if (target.AuthorizedSteamID == null) return;

        var steamid = target.AuthorizedSteamID;

        Task.Run(async () => await OnClientAuthorizedAsync(target, steamid));
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        LoadConfig();

        foreach (var vip in Players.Records)
            ApplyFeatureStates(vip);

        const string msg = "configuration successfully rebooted!";

        ReplyToCommand(controller, msg);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_help", "Lists all VIPCore admin commands.")]
    public void OnCommandHelp(CCSPlayerController? controller, CommandInfo command)
    {
        var t = GetTimeUnitName;
        var lines = new[]
        {
            "===== VIPCore commands =====",
            $"css_vip_adduser <steamid or accountid> <group> <time_{t}>  - add a VIP (time 0 = permanent)",
            "css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>  - update a VIP (-s keeps a field)",
            "css_vip_deleteuser <steamid or accountid>  - remove a VIP",
            "css_reload_vip_player <steamid>  - reload a player's VIP data from the database",
            "css_vip_reload  - reload the VIPCore configuration",
            "css_vip_help  - show this list",
            "============================="
        };

        foreach (var line in lines)
            ReplyToCommand(controller, line);
    }

    private void CreateMenu(CCSPlayerController? player)
    {
        if (player == null) return;

        if (!IsCoreEnableConVar.Value || !Players.TryGet(player, out var vip))
        {
            PrintToChat(player, Localizer["vip.NoAccess"]);
            return;
        }

        var menu = VipApi.CreateMenu(Localizer["menu.Title", vip.Group]);
        if (Config.Groups.TryGetValue(vip.Group, out var vipGroup))
        {
            var sortedFeatures = FeatureRegistry.All.Where(setting => setting.Value.FeatureType is not FeatureType.Hide)
                .OrderBy(setting => Array.IndexOf(_sortedItems, setting.Key))
                .ThenBy(setting => setting.Key);

            foreach (var (key, feature) in sortedFeatures)
            {
                if (!vipGroup.Values.TryGetValue(key, out var featureValue)) continue;
                if (string.IsNullOrEmpty(featureValue.ToString())) continue;
                if (!vip.FeatureStates.TryGetValue(key, out var featureState)) continue;

                var value = string.Empty;
                if (feature.FeatureType is FeatureType.Toggle)
                {
                    value = featureState switch
                    {
                        FeatureState.Enabled => $"{Localizer["chat.Enabled"]}",
                        FeatureState.Disabled => $"{Localizer["chat.Disabled"]}",
                        FeatureState.NoAccess => $"{Localizer["chat.NoAccess"]}",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }

                var featureType = feature.FeatureType;

                menu.AddMenuOption(
                    Localizer[key] + (featureType == FeatureType.Selectable
                        ? string.Empty
                        : $" [{value}]"),
                    (controller, _) =>
                    {
                        var result = VipApi.PlayerUseFeature(player, key, featureState, featureType);

                        if (result == HookResult.Handled || result == HookResult.Stop)
                        {
                            CreateMenu(player);
                            return;
                        }

                        var returnState = featureState;
                        if (featureType != FeatureType.Selectable)
                        {
                            returnState = featureState switch
                            {
                                FeatureState.Enabled => FeatureState.Disabled,
                                FeatureState.Disabled => FeatureState.Enabled,
                                _ => returnState
                            };

                            VipApi.PrintToChat(player,
                                $"{Localizer[key]}: {(returnState == FeatureState.Enabled ? $"{Localizer["chat.Enabled"]}" : $"{Localizer["chat.Disabled"]}")}");

                            if (featureType is FeatureType.Toggle)
                                VipApi.SetPlayerCookie(player.SteamID, key, (int)returnState);
                        }

                        vip.FeatureStates[key] = returnState;
                        feature.OnSelectItem?.Invoke(controller, returnState);

                        if (CoreConfig.ReOpenMenuAfterItemClick && featureType != FeatureType.Selectable)
                        {
                            CreateMenu(controller);
                        }
                    }, featureState == FeatureState.NoAccess || FeatureRegistry.IsForcedDisabled(key));
            }
        }

        menu.Open(player);
    }


    private string BuildConnectionString()
    {
        var connection = CoreConfig.Connection;
        var builder = new MySqlConnectionStringBuilder
        {
            Database = connection.Database,
            UserID = connection.User,
            Password = connection.Password,
            Server = connection.Host,
            Port = (uint)connection.Port,
            Pooling = true,
            MinimumPoolSize = 0,
            MaximumPoolSize = 640,
            ConnectionIdleTimeout = 30
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
    }

    private void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            PrintToChat(controller, msg);
        else
            Server.PrintToConsole($"[VIPCore] {msg}");
    }

    public void PrintToChat(CCSPlayerController player, string msg)
    {
        if (!player.IsValid) return;

        player.PrintToChat($"{Localizer["vip.Prefix"]} {msg}");
    }

    public void PrintToChatAll(string msg)
    {
        Server.PrintToChatAll($"{Localizer["vip.Prefix"]} {msg}");
    }

    public void PrintLogError(string? message, params object?[] args)
    {
        if (!CoreConfig.VipLogging) return;

        Logger.LogError($"{message}", args);
    }

    public void PrintLogInfo(string? message, params object?[] args)
    {
        if (!CoreConfig.VipLogging) return;

        Logger.LogInformation($"{message}", args);
    }

    public void PrintLogWarning(string? message, params object?[] args)
    {
        if (!CoreConfig.VipLogging) return;

        Logger.LogWarning($"{message}", args);
    }

    private string GetTimeUnitName => CoreConfig.TimeMode switch
    {
        0 => "second",
        1 => "minute",
        2 => "hours",
        3 => "days",
        _ => throw new KeyNotFoundException("No such number was found!")
    };

    public int CalculateEndTimeInSeconds(int time) => DateTime.UtcNow.AddSeconds(CoreConfig.TimeMode switch
    {
        1 => time * 60,
        2 => time * 3600,
        3 => time * 86400,
        _ => time
    }).GetUnixEpoch();
}