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
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIPCore;

public class VipCore : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => "v1.2.9";

    public Config Config { get; set; } = null!;
    public CoreConfig CoreConfig { get; set; } = null!;
    public VipCoreApi VipApi { get; set; } = null!;

    public Database Database = null!;

    public readonly ConcurrentDictionary<ulong, User> Users = new();
    public readonly ConcurrentDictionary<string, Feature> Features = new();
    private readonly PluginCapability<IVipCoreApi> _pluginCapability = new("vipcore:core");

    public readonly FakeConVar<bool> IsCoreEnableConVar = new("css_vip_enable", "", true);

    public string DbConnectionString = string.Empty;

    public readonly bool[] IsClientVip = new bool[70];

    private string[] _sortedItems = [];

    public override void Load(bool hotReload)
    {
        VipApi = new VipCoreApi(this);
        Capabilities.RegisterPluginCapability(_pluginCapability, () => VipApi);
        Server.NextWorldUpdate(() => VipApi.CoreReady());

        LoadConfig();

        DbConnectionString = BuildConnectionString();
        Database = new Database(this, DbConnectionString);

        Task.Run(() => Database.CreateTable());

        RegisterEventHandlers();
        SetupTimers();

        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) => CreateMenu(player));
    }

    private void LoadConfig()
    {
        Config = VipApi.LoadConfig<Config>("vip", VipApi.CoreConfigDirectory);
        CoreConfig = VipApi.LoadConfig<CoreConfig>("vip_core", VipApi.CoreConfigDirectory);


        var sortMenuPath = Path.Combine(VipApi.CoreConfigDirectory, "sort_menu.txt");
        
        if(!File.Exists(sortMenuPath))
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

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;
            if (player == null || !IsClientVip[player.Slot])
                return HookResult.Continue;

            IsClientVip[player.Slot] = false;
            if (Users.TryGetValue(player.SteamID, out var user))
            {
                foreach (var featureState in user.FeatureState)
                {
                    VipApi.SetPlayerCookie(player.SteamID, featureState.Key, (int)featureState.Value);
                }
            }

            Users.Remove(player.SteamID, out var _);

            var authAccId = player.AuthorizedSteamID;
            if (authAccId != null)
            {
                var playerName = player.PlayerName;
                Task.Run(() => Database.UpdateUserVip(authAccId.AccountId, name: playerName));
            }

            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
    }

    private void SetupTimers()
    {
        AddTimer(300.0f, () =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player.IsValid))
            {
                var authId = player.AuthorizedSteamID;
                if (authId == null) continue;

                Task.Run(() => Database.RemoveExpiredUsers(player, authId));

                IsClientVip[player.Slot] = IsUserActiveVip(player);
            }
        }, TimerFlags.REPEAT);
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.Handle == IntPtr.Zero || player.UserId == null)
            return HookResult.Continue;
        
        if (player.IsBot || !IsClientVip[player.Slot])
            return HookResult.Continue;

        AddTimer(Config.Delay, () =>
        {
            if (player.Connected != PlayerConnectedState.PlayerConnected) return;

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

    private async Task OnClientAuthorizedAsync(CCSPlayerController player, SteamID steamId)
    {
        try
        {
            var userFromDb = await Database.GetUserFromDb(steamId.AccountId);
            if (userFromDb == null) return;

            Users.Remove(steamId.SteamId64, out _);
            foreach (var user in userFromDb.OfType<User>().Where(user => user.sid == CoreConfig.ServerId))
            {
                Users.TryAdd(steamId.SteamId64, user);
                SetClientFeature(steamId.SteamId64, user.group);

                var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(user.expires);

                await Server.NextFrameAsync(() =>
                {
                    VipApi.OnPlayerLoaded(player, user.group);
                    IsClientVip[player.Slot] = IsUserActiveVip(player);

                    AddTimer(5.0f, () => PrintToChat(player,
                        Localizer["vip.WelcomeToTheServer", user.name] + (user.expires == 0
                            ? string.Empty
                            : Localizer["vip.Expires", user.group, timeRemaining.ToString("G")])));
                });
                return;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.ToString());
        }
    }

    public void SetClientFeature(ulong steamId, string vipGroup)
    {
        foreach (var feature in Features)
        {
            if (!Config.Groups.TryGetValue(vipGroup, out var group)) continue;
            if (!Users.TryGetValue(steamId, out var user)) return;

            if (!group.Values.TryGetValue(feature.Key, out _))
            {
                user.FeatureState[feature.Key] = FeatureState.NoAccess;
                continue;
            }

            var cookie = VipApi.GetPlayerCookie<int>(steamId, feature.Key);
            var cookieValue = cookie == 2 ? 0 : cookie;
            user.FeatureState[feature.Key] = (FeatureState)cookieValue;
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

    [ConsoleCommand("css_vip_adduser")]
    public void OnCmdAddUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        if (command.ArgCount is > 4 or < 4)
        {
            PrintLogInfo("Usage: css_vip_adduser {usage}", $"<steamid or accountid> <group> <time_{GetTimeUnitName}>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
            return;

        var vipGroup = command.GetArg(2);
        var endVipTime = Convert.ToInt32(command.GetArg(3));

        if (!Config.Groups.ContainsKey(vipGroup))
        {
            PrintLogError("This {VIP} group was not found!", "VIP");
            return;
        }

        var username = player == null ? "unknown" : player.PlayerName;

        var user = CreateNewUser(accountId, username, vipGroup, endVipTime);
        Task.Run(() => Database.AddUserToDb(user));

        if (player != null)
        {
            Users.TryAdd(player.SteamID, user);
            SetClientFeature(player.SteamID, user.group);
            VipApi.OnPlayerLoaded(player, user.group);
        }
    }

    [ConsoleCommand("css_vip_deleteuser")]
    public void OnCmdDeleteVipUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        if (command.ArgCount is < 2 or > 2)
        {
            ReplyToCommand(controller, "Using: css_vip_deleteuser <steamid or accountid>");
            return;
        }

        var steamId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (steamId == -1)
            return;

        if (player != null)
        {
            VipApi.OnPlayerRemoved(player, Users[player.SteamID].group);
            Users.Remove(player.SteamID, out _);
        }

        Task.Run(() => Database.RemoveUserFromDb(steamId));
    }

    [ConsoleCommand("css_vip_updateuser")]
    public void OnCmdUpdateUserGroup(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        if (command.ArgCount is > 4 or < 4)
        {
            PrintLogInfo("Usage: css_vip_updateuser {usage}\n{t}", "<steamid or accountid> [group or -s] [time or -s]",
                "if you don't want to update something, don't leave it blank, write `-` or `-s`\nExample of updating time: css_vip_updateuser \"STEAM_0:0:123456\" -s 3600");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
            return;

        var vipGroup = command.GetArg(2);

        if (vipGroup is not ("-" or "-s"))
        {
            if (!Config.Groups.ContainsKey(vipGroup))
            {
                PrintLogError("This {VIP} group was not found!", "VIP");
                return;
            }
        }
        else
            vipGroup = string.Empty;

        var time = int.TryParse(command.GetArg(3), out var arg) ? arg : -1;

        if (player != null)
        {
            if (!Users.TryGetValue(player.SteamID, out var user)) return;

            user.group = vipGroup;
        }

        Task.Run(() => Database.UpdateUserVip(accountId, group: vipGroup, time: time));
    }

    [CommandHelper(1, "<steamid>")]
    [ConsoleCommand("css_reload_vip_player")]
    public void OnCommandVipReloadInfractions(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        var target = Utils.GetPlayerFromSteamId(command.GetArg(1));

        if (target == null) return;
        if (target.AuthorizedSteamID == null) return;

        OnClientAuthorizedAsync(target, target.AuthorizedSteamID);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        LoadConfig();

        const string msg = "configuration successfully rebooted!";

        ReplyToCommand(controller, msg);
    }
    
    private void CreateMenu(CCSPlayerController? player)
{
    if (player == null) return;

    if (!IsClientVip[player.Slot])
    {
        PrintToChat(player, Localizer["vip.NoAccess"]);
        return;
    }

    if (!Users.TryGetValue(player.SteamID, out var user)) return;

    var menu = VipApi.CreateMenu(Localizer["menu.Title", user.group]);
    if (Config.Groups.TryGetValue(user.group, out var vipGroup))
    {
        var sortedFeatures = Features.Where(setting => setting.Value.FeatureType is not FeatureType.Hide)
                                     .OrderBy(setting => Array.IndexOf(_sortedItems, setting.Key))
                                     .ThenBy(setting => setting.Key);

        foreach (var (key, feature) in sortedFeatures)
        {
            if (!vipGroup.Values.TryGetValue(key, out var featureValue)) continue;
            if (string.IsNullOrEmpty(featureValue.ToString())) continue;
            if (!user.FeatureState.TryGetValue(key, out var featureState)) continue;

            var value = featureState switch
            {
                FeatureState.Enabled => $"{Localizer["chat.Enabled"]}",
                FeatureState.Disabled => $"{Localizer["chat.Disabled"]}",
                FeatureState.NoAccess => $"{Localizer["chat.NoAccess"]}",
                _ => throw new ArgumentOutOfRangeException()
            };

            var featureType = feature.FeatureType;

            menu.AddMenuOption(
                Localizer[key] + (featureType == FeatureType.Selectable
                    ? string.Empty
                    : $" [{value}]"),
                (controller, _) =>
                {
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
                    }

                    user.FeatureState[key] = returnState;
                    feature.OnSelectItem?.Invoke(controller, returnState);
                    
                    if (CoreConfig.ReOpenMenuAfterItemClick && featureType != FeatureType.Selectable)
                    {
                        CreateMenu(controller);
                    }
                }, featureState == FeatureState.NoAccess);
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

    public bool IsPlayerVip(CCSPlayerController player)
    {
        return IsClientVip[player.Slot];
    }

    private bool IsUserActiveVip(CCSPlayerController player)
    {
        if (!IsCoreEnableConVar.Value || !Utils.IsValidEntity(player) || !player.IsValid || player.IsBot)
            return false;

        var authorizedSteamId = player.AuthorizedSteamID;
        if (authorizedSteamId == null)
        {
            PrintLogError("{steamid} is null", "AuthorizedSteamId");
            return false;
        }

        if (!Users.TryGetValue(authorizedSteamId.SteamId64, out var user))
            return false;

        if (user.expires != 0 && DateTime.UtcNow.GetUnixEpoch() > user.expires)
        {
            Users.Remove(authorizedSteamId.SteamId64, out _);
            return false;
        }

        return user.expires == 0 || DateTime.UtcNow.GetUnixEpoch() < user.expires;
    }

    private void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            PrintToChat(controller, msg);
        else
            PrintLogInfo($"{msg}");
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

public class User
{
    public int account_id { get; set; }
    public required string name { get; set; }
    public int lastvisit { get; set; }
    public int sid { get; set; }
    public required string group { get; set; }
    public int expires { get; set; }
    public Dictionary<string, FeatureState> FeatureState { get; set; } = new();
}

public class PlayerCookie
{
    public ulong SteamId64 { get; set; }
    public Dictionary<string, object> Features { get; set; } = new();
}

public class Feature
{
    public FeatureType FeatureType { get; set; }
    public Action<CCSPlayerController, FeatureState>? OnSelectItem { get; set; }
}

public static class GetUnixTime
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}