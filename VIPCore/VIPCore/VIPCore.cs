using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
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
    public override string ModuleVersion => "v1.2.3";

    private Cfg _cfg = null!;
    public Config Config { get; set; } = null!;
    public ConfigVipCoreSettings CoreConfig { get; set; } = null!;
    public VipCoreApi VipApi { get; set; } = null!;

    public Database Database = null!;

    public readonly ConcurrentDictionary<ulong, User> Users = new();
    public readonly ConcurrentDictionary<string, Feature> Features = new();
    private readonly PluginCapability<IVipCoreApi> _pluginCapability = new("vipcore:core");

    public string DbConnectionString = string.Empty;

    public override void Load(bool hotReload)
    {
        VipApi = new VipCoreApi(this, ModuleDirectory);
        Capabilities.RegisterPluginCapability(_pluginCapability, () => VipApi);
        Server.NextWorldUpdate(() => VipApi.CoreReady());

        _cfg = new Cfg(this);

        Config = _cfg.LoadConfig();
        CoreConfig = _cfg.LoadVipSettingsConfig();

        DbConnectionString = BuildConnectionString();
        Database = new Database(this, DbConnectionString);

        Task.Run(() => Database.CreateTable());

        RegisterEventHandlers();
        SetupTimers();

        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) => CreateMenu(player));
    }

    private void RegisterEventHandlers()
    {
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            Task.Run(() => OnClientAuthorizedAsync(player, id));
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;
            if (player == null || !IsUserActiveVip(player))
                return HookResult.Continue;

            if (Users.TryGetValue(player.SteamID, out var user))
            {
                foreach (var featureState in user.FeatureState)
                {
                    VipApi.SetPlayerCookie(player.SteamID, featureState.Key, (int)featureState.Value);
                }

                Users.Remove(player.SteamID, out var _);
            }

            var authAccId = player.AuthorizedSteamID;
            if (authAccId != null)
            {
                Task.Run(() => Database.UpdateUserVip(authAccId.AccountId, name: player.PlayerName));
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
            }
        }, TimerFlags.REPEAT);
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid.Handle == IntPtr.Zero || @event.Userid.UserId == null)
            return HookResult.Continue;

        var player = @event.Userid;

        if (player.IsBot || !Users.ContainsKey(player.SteamID) || !VipApi.IsClientVip(player))
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
            Server.NextFrame(() => { Database.RemoveExpiredUsers(player, steamId); });
            await ProcessUserInformationAsync(player, steamId);
        }
        catch (Exception e)
        {
            Logger.LogError(e.ToString());
        }
    }

    private async Task ProcessUserInformationAsync(CCSPlayerController player, SteamID steamId)
    {
        var userFromDb = await Database.GetUserFromDb(steamId.AccountId);
        if (userFromDb == null) return;

        Users.Remove(steamId.SteamId64, out _);
        foreach (var user in userFromDb.OfType<User>().Where(user => user.sid == CoreConfig.ServerId))
        {
            var title = Localizer["menu.Title", user.group];
            user.Menu = CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(title) : new ChatMenu(title);
            AddClientToUsers(steamId.SteamId64, user);
            SetClientFeature(steamId.SteamId64, user.group);

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(user.expires);

            Server.NextFrame(() =>
            {
                VipApi.OnPlayerLoaded(player, user.group);

                AddTimer(5.0f, () => PrintToChat(player,
                    Localizer["vip.WelcomeToTheServer", user.name] + (user.expires == 0
                        ? string.Empty
                        : Localizer["vip.Expires", user.group, timeRemaining.ToString("G")])));
            });
            return;
        }
    }

    private void AddClientToUsers(ulong steamId, User user)
    {
        Users.TryAdd(steamId, user);
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
        var title = Localizer["menu.Title", group];
        return new User
        {
            account_id = accountId,
            name = username,
            lastvisit = DateTime.UtcNow.GetUnixEpoch(),
            sid = CoreConfig.ServerId,
            group = group,
            expires = endTime == 0 ? 0 : CalculateEndTimeInSeconds(endTime),
            Menu = CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(title) : new ChatMenu(title)
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
            AddClientToUsers(player.SteamID, user);
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

        if (command.ArgCount is > 3 or < 3)
        {
            PrintLogInfo("Usage: css_vip_updateuser {usage}", "<steamid or accountid> [group or -s] [time or -s]",
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
            var chatMenu = user.Menu;
            if (chatMenu != null)
                chatMenu.Title = Localizer["menu.Title", vipGroup];
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

        Task.Run(() => ProcessUserInformationAsync(target, target.AuthorizedSteamID));
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        Config = _cfg.LoadConfig();
        CoreConfig = _cfg.LoadVipSettingsConfig();

        const string msg = "configuration successfully rebooted!";

        ReplyToCommand(controller, msg);
    }

    private void CreateMenu(CCSPlayerController? player)
    {
        if (player == null) return;

        if (!IsUserActiveVip(player))
        {
            PrintToChat(player, Localizer["vip.NoAccess"]);
            return;
        }

        if (!Users.TryGetValue(player.SteamID, out var user)) return;

        var title = Localizer["menu.Title", user.group];
        user.Menu = CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(title) : new ChatMenu(title);

        if (user.Menu == null)
        {
            Console.WriteLine("user.Menu == null");
            return;
        }

        user.Menu.MenuOptions.Clear();
        if (Config.Groups.TryGetValue(user.group, out var vipGroup))
        {
            foreach (var setting in Features.Where(setting => setting.Value.FeatureType is not FeatureType.Hide))
            {
                if (!vipGroup.Values.TryGetValue(setting.Key, out var featureValue)) continue;
                if (string.IsNullOrEmpty(featureValue.ToString())) continue;
                if (!user.FeatureState.TryGetValue(setting.Key, out var featureState)) continue;

                var value = featureState switch
                {
                    FeatureState.Enabled => $"{Localizer["chat.Enabled"]}",
                    FeatureState.Disabled => $"{Localizer["chat.Disabled"]}",
                    FeatureState.NoAccess => $"{Localizer["chat.NoAccess"]}",
                    _ => throw new ArgumentOutOfRangeException()
                };

                var featureType = setting.Value.FeatureType;

                user.Menu.AddMenuOption(
                    Localizer[setting.Key] + (featureType == FeatureType.Selectable
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
                                $"{Localizer[setting.Key]}: {(returnState == FeatureState.Enabled ? $"{Localizer["chat.Enabled"]}" : $"{Localizer["chat.Disabled"]}")}");
                        }

                        user.FeatureState[setting.Key] = returnState;
                        setting.Value.OnSelectItem?.Invoke(controller, returnState);
                        
                        if (CoreConfig.ReOpenMenuAfterItemClick)
                            CreateMenu(controller);
                        else
                            MenuManager.CloseActiveMenu(player);
                    }, featureState == FeatureState.NoAccess);
            }
        }

        if (CoreConfig.UseCenterHtmlMenu)
            MenuManager.OpenCenterHtmlMenu(this, player, (CenterHtmlMenu)user.Menu);
        else
            MenuManager.OpenChatMenu(player, (ChatMenu)user.Menu);
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
            Port = (uint)connection.Port
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
    }

    public bool IsUserActiveVip(CCSPlayerController player)
    {
        if (!Utils.IsValidEntity(player) || !player.IsValid || player.IsBot ||
            player.Connected != PlayerConnectedState.PlayerConnected) return false;

        var authorizedSteamId = player.AuthorizedSteamID;
        if (authorizedSteamId == null)
        {
            PrintLogError("{steamid} is null", "AuthorizedSteamId");
            return false;
        }

        if (!Users.TryGetValue(authorizedSteamId.SteamId64, out var user)) return false;

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

    private int CalculateEndTimeInSeconds(int time) => DateTime.UtcNow.AddSeconds(CoreConfig.TimeMode switch
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
    public IMenu? Menu { get; set; }
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