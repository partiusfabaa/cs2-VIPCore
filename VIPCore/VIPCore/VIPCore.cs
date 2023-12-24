using System.Text.Json;
using System.Xml;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Extensions.Logging;
using Modularity;
using MySqlConnector;
using VipCoreApi;
using ChatMenu = CounterStrikeSharp.API.Modules.Menu.ChatMenu;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace VIPCore;

public class VipCore : BasePlugin, ICorePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => "v1.0.0";

    public string DbConnectionString = string.Empty;

    private Cfg? _cfg;
    public Config Config = null!;
    private ConfigVipCoreSettings _coreSetting = null!;
    public VipCoreApi VipApi = null!;


    private readonly bool?[] _vipStatusExpired = new bool?[65];
    public readonly User?[] Users = new User[65];
    public readonly Dictionary<string, Feature> Features = new();

    public override void Load(bool hotReload)
    {
        _cfg = new Cfg(this);

        if (hotReload)
        {
            LoadCore(new PluginApis());
            Logger.LogWarning(
                "Hot reload completed. Be aware of potential issues. Consider {restart} for a clean state",
                "restarting");
            Config = _cfg.LoadConfig();
            _coreSetting = _cfg.LoadVipSettingsConfig();
        }

        RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            Config = _cfg.LoadConfig();
            _vipStatusExpired[slot + 1] = false;
        });
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            Task.Run(() => OnClientAuthorizedAsync(player, slot, id));
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (!IsUserActiveVip(player))
                return HookResult.Continue;

            foreach (var featureState in Users[player.Index]!.FeatureState)
            {
                VipApi.SetPlayerCookie(player.SteamID, featureState.Key, (int)featureState.Value);
            }

            Users[player.Index] = null;

            var playerName = player.PlayerName;
            if (player.AuthorizedSteamID == null) return HookResult.Continue;
            var authAccId = player.AuthorizedSteamID.AccountId;
            Task.Run(() => UpdateUserNameInDb(authAccId, playerName));

            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);

        CreateMenu();

        AddTimer(300.0f, () => Task.Run(RemoveExpiredUsers), TimerFlags.REPEAT);
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid.Handle == IntPtr.Zero || @event.Userid.UserId == null) return HookResult.Continue;
        var player = @event.Userid;
        if (player.IsBot || !player.IsValid) return HookResult.Continue;
        var user = Users[player.Index];
        if (user == null) return HookResult.Continue;
        if (!VipApi.IsClientVip(player)) return HookResult.Continue;

        AddTimer(Config.Delay, () =>
        {
            var playerPawn = player.PlayerPawn.Value;

            if (playerPawn == null) return;
            if (!playerPawn.IsValid ||
                player.TeamNum is not ((int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist)) return;

            try
            {
                VipApi.PlayerSpawn(player);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in VipApi.PlayerSpawn: {ex}");
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    //private void Startup()
    //{
    //    VipApi.Startup();
    //}

    private async Task OnClientAuthorizedAsync(CCSPlayerController player, int playerSlot, SteamID steamId)
    {
        try
        {
            var msg = await RemoveExpiredUsers();
            PrintLogInfo(msg);

            var user = await GetUserFromDb(steamId.AccountId);

            if (user == null)
            {
                Console.WriteLine("USER == NULL");
                return;
            }

            //if (user.sid != _coreSetting.ServerId) return;
            
            AddClientToUsers((uint)(playerSlot + 1), user);
            SetClientFeature(steamId.SteamId64, user.group, (uint)(playerSlot + 1));

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(user.expires);

            Server.NextFrame(() =>
            {
                PrintToChat(player,
                    Localizer["vip.WelcomeToTheServer", user.name] +
                    Localizer["vip.Expires", timeRemaining.ToString("G")]);
            });

            Console.WriteLine("ADD USER TO USERS");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void AddClientToUsers(uint index, User user)
    {
        Users[index] = new User
        {
            account_id = user.account_id,
            name = user.name,
            lastvisit = user.lastvisit,
            sid = user.sid,
            group = user.group,
            expires = user.expires,
            Menu = new ChatMenu(Localizer["menu.Title", user.group])
        };
    }

    public void SetClientFeature(ulong steamId, string vipGroup, uint index)
    {
        foreach (var feature in Features)
        {
            if (Config.Groups.TryGetValue(vipGroup, out var group))
            {
                if (!group.Values.ContainsKey(feature.Key))
                {
                    Users[index]!.FeatureState[feature.Key] = IVipCoreApi.FeatureState.NoAccess;
                    continue;
                }

                var cookie = VipApi.GetPlayerCookie<int>(steamId, feature.Key);

                var cookieValue = cookie == 2 ? 0 : cookie;
                Users[index]!.FeatureState[feature.Key] = (IVipCoreApi.FeatureState)cookieValue;
            }
        }
    }

    private int GetSteamIdFromCommand(string steamId, out CCSPlayerController? player)
    {
        player = null;

        if (steamId.Contains("STEAM_1"))
        {
            PrintLogError("please change the first digit in your {steamid}. example: {steam1} to {steam0}",
                "SteamID", "STEAM_1:", "STEAM_0:");
            return -1;
        }

        if (steamId.Contains("STEAM_") || steamId.Contains("765611"))
        {
            player = GetPlayerFromSteamId(steamId);

            if (player == null)
            {
                PrintLogError("Player not found");
                return -1;
            }

            var authorizedSteamId = player.AuthorizedSteamID;

            if (authorizedSteamId == null)
            {
                PrintLogError("AuthorizedSteamId is null");
                return -1;
            }

            steamId = authorizedSteamId.AccountId.ToString();
        }

        return int.Parse(steamId);
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

        var steamId = GetSteamIdFromCommand(command.GetArg(1), out var player);
        if (steamId == -1)
            return;

        var vipGroup = command.GetArg(2);
        var endVipTime = Convert.ToInt32(command.GetArg(3));

        if (!Config.Groups.ContainsKey(vipGroup))
        {
            PrintLogError("This {VIP} group was not found!", "VIP");
            return;
        }

        var username = player == null ? "unknown" : player.PlayerName;

        var user = new User
        {
            account_id = steamId,
            name = username,
            lastvisit = DateTime.UtcNow.GetUnixEpoch(),
            sid = 0, //_coreSetting.ServerId,
            group = vipGroup,
            expires = endVipTime == 0 ? 0 : CalculateEndTimeInSeconds(endVipTime)
        };
        
        Task.Run(() => AddUserToDb(user));

        if (player != null)
        {
            AddClientToUsers(player.Index, user);
            SetClientFeature(player.SteamID, user.group, player.Index);
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

        var steamId = GetSteamIdFromCommand(command.GetArg(1), out var player);
        if (steamId == -1)
            return;

        if (player != null)
            Users[player.Index] = null;

        Task.Run(() => RemoveUserFromDb(steamId));
    }

    [ConsoleCommand("css_vip_updategroup")]
    public void OnCmdUpdateUserGroup(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        if (command.ArgCount is > 3 or < 3)
        {
            PrintLogInfo("Usage: css_vip_updategroup {usage}", "<steamid or accountid> <group>");
            return;
        }

        var steamId = GetSteamIdFromCommand(command.GetArg(1), out var player);
        if (steamId == -1)
            return;

        var vipGroup = command.GetArg(2);

        if (!Config.Groups.ContainsKey(vipGroup))
        {
            PrintLogError("This {VIP} group was not found!", "VIP");
            return;
        }

        if (player != null)
        {
            if (Users[player.Index] != null)
            {
                Users[player.Index]!.group = vipGroup;
                var chatMenu = Users[player.Index]!.Menu;
                if (chatMenu != null)
                    chatMenu.Title = Localizer["menu.Title", vipGroup];
            }
        }

        Task.Run(() => UpdateUserGroupInDb(steamId, vipGroup));
    }

    [ConsoleCommand("css_vip_updatetime")]
    public void OnCmdUpdateUserTime(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        if (command.ArgCount is > 3 or < 3)
        {
            PrintLogInfo("Usage: css_vip_updatetime {usage}", $"<steamid or accountid> <time_{GetTimeUnitName}>");
            return;
        }

        var steamId = GetSteamIdFromCommand(command.GetArg(1), out var player);
        if (steamId == -1)
            return;

        var time = int.Parse(command.GetArg(2));
        var calculateTime = CalculateEndTimeInSeconds(time);

        if (player != null)
        {
            if (Users[player.Index] != null)
            {
                Users[player.Index]!.expires = calculateTime;
            }
        }

        Task.Run(() => UpdateUserTimeInDb(steamId, time == 0 ? 0 : calculateTime));
    }

    private string GetTimeUnitName => _coreSetting.TimeMode switch
    {
        0 => "second",
        1 => "minute",
        2 => "hours",
        3 => "days",
        _ => throw new KeyNotFoundException("No such number was found!")
    };

    private int CalculateEndTimeInSeconds(int time) => DateTime.UtcNow.AddSeconds(_coreSetting.TimeMode switch
    {
        1 => time * 60,
        2 => time * 3600,
        3 => time * 86400,
        _ => time
    }).GetUnixEpoch();

    [RequiresPermissions("@css/root", "@vip/vip")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        if (_cfg != null)
        {
            Config = _cfg.LoadConfig();
        }

        const string msg = "configuration successfully rebooted!";

        ReplyToCommand(controller, msg);
    }

    private void CreateMenu()
    {
        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) =>
        {
            if (player == null) return;

            if (!IsUserActiveVip(player))
            {
                PrintToChat(player, Localizer["vip.NoAccess"]);
                return;
            }

            var user = Users[player.Index];

            if (user?.Menu == null)
            {
                Console.WriteLine("user?.Menu == null");
                return;
            }
            user.Menu.MenuOptions.Clear();

            if (Config.Groups.TryGetValue(user.group, out var vipGroup))
            {
                foreach (var setting in Features.Where(
                             setting => setting.Value.FeatureType is
                                 IVipCoreApi.FeatureType.Toggle or IVipCoreApi.FeatureType.Selectable))
                {
                    if (!vipGroup.Values.TryGetValue(setting.Key, out var featureValue)) continue;
                    if (string.IsNullOrEmpty(featureValue.ToString())) continue;
                    if (!user.FeatureState.TryGetValue(setting.Key, out var featureState)) continue;
                    //var featureState = user.FeatureState[setting.Key];

                    var value = featureState switch
                    {
                        IVipCoreApi.FeatureState.Enabled => $"{Localizer["chat.Enabled"]}",
                        IVipCoreApi.FeatureState.Disabled => $"{Localizer["chat.Disabled"]}",
                        IVipCoreApi.FeatureState.NoAccess => $"{Localizer["chat.NoAccess"]}",
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    user.Menu.AddMenuOption(
                        Localizer[setting.Key] + $" [{value}]",
                        (controller, _) =>
                        {
                            var returnState = featureState;
                            if (setting.Value.FeatureType != IVipCoreApi.FeatureType.Selectable)
                            {
                                returnState = featureState == IVipCoreApi.FeatureState.Enabled
                                    ? IVipCoreApi.FeatureState.Disabled
                                    : IVipCoreApi.FeatureState.Enabled;

                                VipApi.PrintToChat(player,
                                    $"{Localizer[setting.Key]}: {(returnState == IVipCoreApi.FeatureState.Enabled ? $"{Localizer["chat.Enabled"]}" : $"{Localizer["chat.Disabled"]}")}");
                            }

                            user.FeatureState[setting.Key] = returnState;
                            setting.Value.OnSelectItem?.Invoke(controller, returnState);
                        },
                        featureState == IVipCoreApi.FeatureState.NoAccess);
                }
            }

            ChatMenus.OpenMenu(player, user.Menu);
        });
    }

    private string BuildConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Database = Config.Connection.Database,
            UserID = Config.Connection.User,
            Password = Config.Connection.Password,
            Server = Config.Connection.Host,
            Port = 3306
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
    }

    private async Task CreateTable(string connectionString)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(connectionString);
            dbConnection.Open();

            var createVipUsersTable = @"
            CREATE TABLE IF NOT EXISTS `vip_users` (
            `account_id` BIGINT NOT NULL PRIMARY KEY,
            `name` VARCHAR(64) NOT NULL,
            `lastvisit` BIGINT NOT NULL,
            `sid` BIGINT NOT NULL,
            `group` VARCHAR(64) NOT NULL,
            `expires` BIGINT NOT NULL
             );";

            await dbConnection.ExecuteAsync(createVipUsersTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task<User?> GetExistingUserFromDb(int accountId)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId", new { AccId = accountId });

            if (existingUser != null) return existingUser;

            PrintLogError("User not found");
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public async Task AddUserToDb(User user)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId", new { AccId = user.account_id });

            if (existingUser != null)
            {
                PrintLogWarning("User already exists");
                return;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO vip_users (account_id, name, lastvisit, sid, `group`, expires)
                VALUES (@account_id, @name, @lastvisit, @sid, @group, @expires);", user);

            PrintLogInfo("Player '{name} [{accId}]' has been successfully added", user.name, user.account_id);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateUserTimeInDb(int accountId, int newExpires)
    {
        var existingUser = await GetExistingUserFromDb(accountId);

        if (existingUser == null)
            return;

        try
        {
            existingUser.expires = newExpires;
            await using var connection = new MySqlConnection(DbConnectionString);
            await connection.ExecuteAsync("UPDATE vip_users SET expires = @expires WHERE account_id = @account_id;",
                existingUser);
            PrintLogInfo("Player '{name} [{accId}]' expiration time has been updated to {expires}", existingUser.name,
                existingUser.account_id, newExpires);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateUserGroupInDb(int accountId, string newGroup)
    {
        var existingUser = await GetExistingUserFromDb(accountId);

        if (existingUser == null)
            return;

        try
        {
            existingUser.group = newGroup;
            await using var connection = new MySqlConnection(DbConnectionString);
            await connection.ExecuteAsync(" UPDATE vip_users SET `group` = @group WHERE account_id = @account_id;",
                existingUser);

            PrintLogInfo("Player '{name} [{accId}]' group has been updated to {group}", existingUser.name,
                existingUser.account_id, newGroup);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateUserNameInDb(int accountId, string newName)
    {
        var existingUser = await GetExistingUserFromDb(accountId);
        if (existingUser == null)
            return;
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            existingUser.name = newName;
            await connection.ExecuteAsync("UPDATE vip_users SET name = @name WHERE account_id = @account_id;",
                existingUser);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task RemoveUserFromDb(int accId)
    {
        var existingUser = await GetExistingUserFromDb(accId);

        if (existingUser == null)
            return;

        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            await connection.ExecuteAsync(@"
            DELETE FROM vip_users
            WHERE account_id = @AccId;", new { AccId = accId });

            PrintLogInfo("Player '{accId}' has been successfully removed", accId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<User?> GetUserFromDb(int accId)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);
            await connection.OpenAsync();
            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM `vip_users` WHERE `account_id` = @AccId", new { AccId = accId });

            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    private async Task<string> RemoveExpiredUsers()
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            var expiredUsers = await connection.QueryAsync<User>(
                "SELECT * FROM vip_users WHERE expires < @CurrentTime AND expires > 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            foreach (var user in expiredUsers)
            {
                await connection.ExecuteAsync("DELETE FROM vip_users WHERE account_id = @AccId",
                    new { AccId = user.account_id });

                Server.NextFrame(() =>
                {
                    foreach (var player in Utilities.GetPlayers().Where(u =>
                                 u.AuthorizedSteamID != null &&
                                 u.AuthorizedSteamID.AccountId == user.account_id))
                    {
                        PrintToChat(player, Localizer["vip.Expired"]);
                    }
                });

                PrintLogInfo("User '{name} [{accId}]' has been removed due to expired VIP status.", user.name,
                    user.account_id);
            }

            return "Expired users removed successfully.";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    // public async Task<string> GetVipGroupFromDatabase(string steamId)
    // {
    //     try
    //     {
    //         await using var connection = new MySqlConnection(_dbConnectionString);
    //
    //         var user = await connection.QueryFirstOrDefaultAsync<User>(
    //             "SELECT group FROM vip_users WHERE account_id = @SteamId",
    //             new { SteamId = steamId });
    //
    //         if (user != null) return user.group;
    //
    //         PrintLogError("User not found");
    //         return string.Empty;
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine(e);
    //         throw;
    //     }
    // }

    public bool IsUserActiveVip(CCSPlayerController player)
    {
        var index = player.Index;
        var user = Users[index];
        if (user == null) return false;

        if (user.expires != 0 && DateTime.UtcNow.GetUnixEpoch() > user.expires)
        {
            Users[index] = null;
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
        if (!_coreSetting.VipLogging) return;

        Logger.LogError($"{message}", args);
    }

    public void PrintLogInfo(string? message, params object?[] args)
    {
        if (!_coreSetting.VipLogging) return;

        Logger.LogInformation($"{message}", args);
    }

    public void PrintLogWarning(string? message, params object?[] args)
    {
        if (!_coreSetting.VipLogging) return;

        Logger.LogWarning($"{message}", args);
    }

    public void LoadCore(IApiRegisterer apiRegisterer)
    {
        VipApi = new VipCoreApi(this, ModuleDirectory);
        apiRegisterer.Register<IVipCoreApi>(VipApi);
        if (_cfg != null)
        {
            Config = _cfg.LoadConfig();
            _coreSetting = _cfg.LoadVipSettingsConfig();
        }

        DbConnectionString = BuildConnectionString();
        Task.Run(() => CreateTable(DbConnectionString));
    }

    private CCSPlayerController? GetPlayerFromSteamId(string steamId)
    {
        return Utilities.GetPlayers().FirstOrDefault(u =>
            u.AuthorizedSteamID != null &&
            (u.AuthorizedSteamID.SteamId2.ToString().Equals(steamId, StringComparison.OrdinalIgnoreCase) ||
             u.AuthorizedSteamID.SteamId64.ToString().Equals(steamId, StringComparison.OrdinalIgnoreCase)));
    }
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

public class VipCoreApi : IVipCoreApi
{
    private readonly VipCore _vipCore;

    //public event Action? OnCoreReady;
    public event Action<CCSPlayerController>? OnPlayerSpawn;

    public string GetTranslatedText(string name, params object[] args) => _vipCore.Localizer[name, args];

    public string CoreConfigDirectory { get; }
    public string ModulesConfigDirectory => Path.Combine(CoreConfigDirectory, "Modules/");
    public string GetDatabaseConnection => _vipCore.DbConnectionString;

    public VipCoreApi(VipCore vipCore, string moduleDirectory)
    {
        _vipCore = vipCore;
        CoreConfigDirectory = new DirectoryInfo(moduleDirectory).Parent?.Parent?.Parent?.Parent?.FullName +
                              "/configs/plugins/VIPCore/";
    }

    public IVipCoreApi.FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        var user = _vipCore.Users[player.Index];

        return user == null
            ? throw new InvalidOperationException("player not found")
            : user.FeatureState.GetValueOrDefault(feature, IVipCoreApi.FeatureState.NoAccess);
    }

    public void RegisterFeature(string feature,
        IVipCoreApi.FeatureType featureType = IVipCoreApi.FeatureType.Toggle,
        Action<CCSPlayerController, IVipCoreApi.FeatureState>? selectItem = null)
    {
        foreach (var config in _vipCore.Config!.Groups)
        {
            if (feature != null)
            {
                config.Value.Values.TryAdd(feature, string.Empty);
                foreach (var keyValuePair in config.Value.Values)
                {
                    if (string.IsNullOrEmpty(keyValuePair.Value.ToString())) continue;

                    _vipCore.Features.TryAdd(feature, new Feature
                    {
                        FeatureType = featureType,
                        OnSelectItem = selectItem
                    });
                }
            }
        }

        _vipCore.PrintLogInfo("Feature '{feature}' registered successfully", feature);
    }

    public void UnRegisterFeature(string feature)
    {
        foreach (var config in _vipCore.Config!.Groups)
        {
            if (feature != null)
            {
                config.Value.Values.Remove(feature);
                _vipCore.Features.Remove(feature);
            }
        }

        _vipCore.PrintLogInfo(
            "Feature '{feature}' unregistered successfully", feature);
    }

    public bool IsClientVip(CCSPlayerController player)
    {
        return _vipCore.IsUserActiveVip(player);
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        var index = player.Index;
        var user = _vipCore.Users[index];

        if (user is null or { group: null }) return false;

        if (_vipCore.Config.Groups.TryGetValue(user.group, out var vipGroup))
        {
            return vipGroup.Values.ContainsKey(feature);
        }

        Console.WriteLine("Couldn't find VipGroup in Config.Groups.");
        return false;
    }

    public string GetClientVipGroup(CCSPlayerController player)
    {
        var user = _vipCore.Users[player.Index];

        return user == null ? throw new InvalidOperationException("player not found") : user.group;
    }

    public void GiveClientVip(CCSPlayerController player, string group, int time)
    {
        var index = player.Index;
        var name = player.PlayerName;
        if (player.AuthorizedSteamID == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var authSteamId = player.AuthorizedSteamID;
        var accountId = authSteamId.AccountId;
        var steamId64 = authSteamId.SteamId64;

        Task.Run(() => GiveClientVipAsync(name, accountId, index, group, time, steamId64));
    }

    public void RemoveClientVip(CCSPlayerController player)
    {
        Task.Run(() => RemoveClientVipAsync(player));
    }

    public void PrintToChat(CCSPlayerController player, string message)
    {
        _vipCore.PrintToChat(player, message);
    }

    public void PrintToChatAll(string message)
    {
        _vipCore.PrintToChatAll(message);
    }

    public bool IsPistolRound()
    {
        var gamerules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        var halftime = ConVar.Find("mp_halftime")!.GetPrimitiveValue<bool>();
        var maxrounds = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

        if (gamerules == null) return false;
        return gamerules.TotalRoundsPlayed == 0 || (halftime && maxrounds / 2 == gamerules.TotalRoundsPlayed) ||
               gamerules.GameRestart;
    }

    // public void Startup()
    // {
    //     OnCoreReady?.Invoke();
    // }

    public void PlayerSpawn(CCSPlayerController player)
    {
        OnPlayerSpawn?.Invoke(player);
    }

    private async Task GiveClientVipAsync(string username, int accountId, uint index, string group, int timeSeconds,
        ulong steamId64)
    {
        try
        {
            await _vipCore.AddUserToDb(new User
            {
                account_id = accountId,
                name = username,
                lastvisit = DateTime.UtcNow.GetUnixEpoch(),
                sid = 0,
                group = group,
                expires = timeSeconds == 0 ? timeSeconds : DateTime.UtcNow.AddSeconds(timeSeconds).GetUnixEpoch()
            });

            var user = await _vipCore.GetUserFromDb(accountId);

            if (user != null)
            {
                _vipCore.Users[index] = new User
                {
                    account_id = accountId,
                    name = user.name,
                    lastvisit = user.lastvisit,
                    sid = user.sid,
                    group = user.group,
                    expires = user.expires,
                    Menu = new ChatMenu(_vipCore.Localizer["menu.Title", user.group])
                };
                _vipCore.SetClientFeature(steamId64, user.group, index);
            }
            else
                _vipCore.PrintLogError(
                    "This user '{username} [{accountId}]' already has VIP", username, accountId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task RemoveClientVipAsync(CCSPlayerController player)
    {
        try
        {
            Server.NextFrame(() => _vipCore.RemoveUserFromDb(new SteamID(player.SteamID).AccountId));
            _vipCore.Users[player.Index] = null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public T GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        var user = _vipCore.Users[player.Index];

        if (user == null || string.IsNullOrEmpty(user.group))
            throw new InvalidOperationException("User or user's group not found.");

        if (_vipCore.Config?.Groups.TryGetValue(user.group, out var vipGroup) == true)
        {
            if (vipGroup.Values.TryGetValue(feature, out var value))
            {
                _vipCore.PrintLogInfo(
                    "Checking feature: {feature} - {value}", feature, value);
                try
                {
                    return ((JsonElement)value).Deserialize<T>()!;
                }
                catch (JsonException)
                {
                    _vipCore.PrintLogError(
                        "Failed to deserialize feature '{feature}' value: {value}", feature, value);
                    throw new JsonException($"Failed to deserialize feature '{feature}' value: {value}");
                }
            }
        }

        _vipCore.PrintLogError("Feature not found, returning default value: {empty}", "Empty");
        throw new KeyNotFoundException($"Feature '{feature}' not found.");
    }

    public T LoadConfig<T>(string name, string path)
    {
        var configFilePath = Path.Combine(path, $"{name}.json");

        if (!File.Exists(configFilePath))
        {
            var defaultConfig = Activator.CreateInstance<T>();
            var defaultJson =
                JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, defaultJson);
        }

        var configJson = File.ReadAllText(configFilePath);
        var config = JsonSerializer.Deserialize<T>(configJson);

        if (config == null)
            throw new FileNotFoundException($"File {name}.json not found or cannot be deserialized");

        return config;
    }

    public T LoadConfig<T>(string name)
    {
        return LoadConfig<T>(name, ModulesConfigDirectory);
    }

    public void SetPlayerCookie<T>(ulong steamId64, string featureName, T value)
    {
        var cookies = LoadCookies();

        if (value != null)
        {
            var existingCookie = cookies.FirstOrDefault(c => c.SteamId64 == steamId64);

            if (existingCookie != null)
                existingCookie.Features[featureName] = value;
            else
            {
                var newCookie = new PlayerCookie
                {
                    SteamId64 = steamId64,
                    Features = new Dictionary<string, object> { { featureName, value } }
                };
                cookies.Add(newCookie);
            }

            SaveCookies(cookies);
        }
    }

    public T GetPlayerCookie<T>(ulong steamId64, string featureName)
    {
        var cookies = LoadCookies();

        var cookie = cookies.FirstOrDefault(c => c.SteamId64 == steamId64);

        if (cookie != null && cookie.Features.TryGetValue(featureName, out var jsonElement))
        {
            try
            {
                var stringValue = jsonElement.ToString();
                var deserializedValue = (T)Convert.ChangeType(stringValue, typeof(T))!;
                return deserializedValue!;
            }
            catch (Exception)
            {
                _vipCore.PrintLogError("Failed to deserialize feature '{feature}' value.", featureName);
            }
        }

        return default!;
    }

    private string GetCookiesFilePath()
    {
        return Path.Combine(CoreConfigDirectory, "vip_core_cookie.json");
    }

    private List<PlayerCookie> LoadCookies()
    {
        var filePath = GetCookiesFilePath();
        return File.Exists(filePath)
            ? JsonSerializer.Deserialize<List<PlayerCookie>>(File.ReadAllText(filePath)) ?? new List<PlayerCookie>()
            : new List<PlayerCookie>();
    }

    private void SaveCookies(List<PlayerCookie> cookies)
    {
        File.WriteAllText(GetCookiesFilePath(), JsonSerializer.Serialize(cookies));
    }
}

public class User
{
    public int account_id { get; set; }
    public required string name { get; set; }
    public int lastvisit { get; set; }
    public int sid { get; set; }
    public required string group { get; set; }
    public int expires { get; set; }
    public ChatMenu? Menu { get; set; }
    public Dictionary<string, IVipCoreApi.FeatureState> FeatureState { get; set; } = new();
}

public class PlayerCookie
{
    public ulong SteamId64 { get; set; }
    public Dictionary<string, object> Features { get; set; } = new();
}

public class Feature
{
    public IVipCoreApi.FeatureType FeatureType { get; set; }
    public Action<CCSPlayerController, IVipCoreApi.FeatureState>? OnSelectItem { get; set; }
}