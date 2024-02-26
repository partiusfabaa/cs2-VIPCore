using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using Microsoft.Extensions.Logging;
using Modularity;
using MySqlConnector;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;
using ChatMenu = CounterStrikeSharp.API.Modules.Menu.ChatMenu;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace VIPCore;

public class VipCore : BasePlugin, ICorePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => "v1.1.8";

    public string DbConnectionString = string.Empty;

    private Cfg? _cfg;
    public Config Config = null!;
    public ConfigVipCoreSettings CoreConfig = null!;
    public VipCoreApi VipApi = null!;

    //public readonly User?[] Users = new User[65];
    public readonly Dictionary<ulong, User> Users = new();
    public readonly Dictionary<string, Feature> Features = new();

    public override void Load(bool hotReload)
    {
        if (hotReload)
        {
            _cfg = new Cfg(this);
            LoadCore(new PluginApis());
            Logger.LogWarning(
                "Hot reload completed. Be aware of potential issues. Consider {restart} for a clean state",
                "restarting");
            Config = _cfg.LoadConfig();
            CoreConfig = _cfg.LoadVipSettingsConfig();
        }

        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            Task.Run(() => OnClientAuthorizedAsync(player, slot, id));
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (player == null) return HookResult.Continue;

            if (!IsUserActiveVip(player))
                return HookResult.Continue;

            foreach (var featureState in Users[player.SteamID].FeatureState)
            {
                VipApi.SetPlayerCookie(player.SteamID, featureState.Key, (int)featureState.Value);
            }

            Users.Remove(player.SteamID);

            var playerName = player.PlayerName;
            if (player.AuthorizedSteamID == null) return HookResult.Continue;
            var authAccId = player.AuthorizedSteamID.AccountId;
            Task.Run(() => UpdateUserNameInDb(authAccId, playerName));

            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);

        CreateMenu();

        AddTimer(300.0f, () => Task.Run(() =>
        {
            Server.NextFrame(() =>
            {
                foreach (var player in Utilities.GetPlayers())
                {
                    if (player.AuthorizedSteamID == null) continue;

                    RemoveExpiredUsers(player, player.AuthorizedSteamID);
                }
            });
        }), TimerFlags.REPEAT);
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid.Handle == IntPtr.Zero || @event.Userid.UserId == null) return HookResult.Continue;

        var player = @event.Userid;

        if (player.IsBot) return HookResult.Continue;
        if (!Users.ContainsKey(player.SteamID)) return HookResult.Continue;
        if (!VipApi.IsClientVip(player)) return HookResult.Continue;

        AddTimer(Config.Delay, () =>
        {
            if (player.Connected != PlayerConnectedState.PlayerConnected) return;

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
            Server.NextFrame(() => { RemoveExpiredUsers(player, steamId); });

            await ProcessUserInformationAsync(player, steamId, playerSlot);
            //var user = await GetUserFromDb(steamId.AccountId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task ProcessUserInformationAsync(CCSPlayerController player, SteamID steamId, int slot)
    {
        var userFromDb = await GetUserFromDb(steamId.AccountId);

        Users.Remove(steamId.SteamId64);

        foreach (var user in userFromDb.OfType<User>().Where(user => user.sid == CoreConfig.ServerId))
        {
            AddClientToUsers(steamId.SteamId64, user);
            SetClientFeature(steamId.SteamId64, user.group, (uint)(slot + 1));

            Server.NextFrame(() => VipApi.OnPlayerLoaded(player, user.group));

            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(user.expires);

            Server.NextFrame(() =>
            {
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
        var title = Localizer["menu.Title", user.group];
        Users.Add(steamId, new User
        {
            account_id = user.account_id,
            name = user.name,
            lastvisit = user.lastvisit,
            sid = user.sid,
            group = user.group,
            expires = user.expires,
            Menu = CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(title) : new ChatMenu(title)
        });
    }

    public void SetClientFeature(ulong steamId, string vipGroup, uint index)
    {
        foreach (var feature in Features)
        {
            if (Config.Groups.TryGetValue(vipGroup, out var group))
            {
                if (!Users.TryGetValue(steamId, out var user)) return;

                if (!group.Values.ContainsKey(feature.Key))
                {
                    user.FeatureState[feature.Key] = FeatureState.NoAccess;
                    continue;
                }

                var cookie = VipApi.GetPlayerCookie<int>(steamId, feature.Key);

                var cookieValue = cookie == 2 ? 0 : cookie;
                user.FeatureState[feature.Key] = (FeatureState)cookieValue;
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

        if (steamId.Contains("STEAM_") || steamId.StartsWith("765611"))
        {
            player = GetPlayerFromSteamId(steamId);

            if (steamId.StartsWith("765611"))
            {
                var steamIdAsUlong = ulong.Parse(steamId);

                if (player == null) return new SteamID(steamIdAsUlong).AccountId;
                var authorizedSteamId = player.AuthorizedSteamID;

                return authorizedSteamId == null ? new SteamID(steamIdAsUlong).AccountId : authorizedSteamId.AccountId;
            }
            else
            {
                if (player == null) return new SteamID(steamId).AccountId;

                var authorizedSteamId = player.AuthorizedSteamID;
                if (authorizedSteamId == null) return new SteamID(steamId).AccountId;

                return authorizedSteamId.AccountId;
            }
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

        foreach (var keyValuePair in Config.Groups)
        {
            Console.WriteLine(keyValuePair.Key);
        }
        
        Console.WriteLine(command.GetArg(1));
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
            sid = CoreConfig.ServerId,
            group = vipGroup,
            expires = endVipTime == 0 ? 0 : CalculateEndTimeInSeconds(endVipTime)
        };

        Task.Run(() => AddUserToDb(user));

        if (player != null)
        {
            AddClientToUsers(player.SteamID, user);
            SetClientFeature(player.SteamID, user.group, player.Index);
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

        var steamId = GetSteamIdFromCommand(command.GetArg(1), out var player);
        if (steamId == -1)
            return;

        if (player != null)
        {
            VipApi.OnPlayerRemoved(player, Users[player.SteamID]!.group);
            Users.Remove(player.SteamID);
        }

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
            if (!Users.TryGetValue(player.SteamID, out var user)) return;

            user.group = vipGroup;
            var chatMenu = user.Menu;
            if (chatMenu != null)
                chatMenu.Title = Localizer["menu.Title", vipGroup];
        }

        Task.Run(() => UpdateUserVip(steamId, group: vipGroup));
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
            Users[player.SteamID].expires = calculateTime;
        }

        Task.Run(() => UpdateUserTimeInDb(steamId, time == 0 ? 0 : calculateTime));
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

    // [RequiresPermissions("@css/root")]
    // [ConsoleCommand("css_refresh_vips")]
    // public void OnCommandRefreshVips(CCSPlayerController? player, CommandInfo command)
    // {
    //     foreach (var players in Utilities.GetPlayers()
    //                  .Where(u => u.AuthorizedSteamID != null && u.PlayerPawn.Value != null))
    //     {
    //         Server.NextFrame(() => ProcessUserInformationAsync(players, players.AuthorizedSteamID, players.Slot));
    //     }
    //
    //     const string msg = "VIP players have been successfully reloaded";
    //
    //     ReplyToCommand(player, msg);
    // }

    [CommandHelper(1, "<steamid>")]
    [ConsoleCommand("css_reload_vip_player")]
    public void OnCommandVipReloadInfractions(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        var target = GetPlayerFromSteamId(command.GetArg(1));

        if (target == null) return;

        ProcessUserInformationAsync(target, target.AuthorizedSteamID, target.Slot);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        if (_cfg != null)
        {
            Config = _cfg.LoadConfig();
            CoreConfig = _cfg.LoadVipSettingsConfig();
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

            if (!Users.TryGetValue(player.SteamID, out var user)) return;

            var title = Localizer["menu.Title", user.group];
            user.Menu = CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(Localizer[title]) : new ChatMenu(title);
            
            if (user.Menu == null)
            {
                Console.WriteLine("user?.Menu == null");
                return;
            }

            user.Menu.MenuOptions.Clear();

            if (Config.Groups.TryGetValue(user.group, out var vipGroup))
            {
                foreach (var setting in Features.Where(
                             setting => setting.Value.FeatureType is
                                 FeatureType.Toggle or FeatureType.Selectable))
                {
                    if (!vipGroup.Values.TryGetValue(setting.Key, out var featureValue)) continue;
                    if (string.IsNullOrEmpty(featureValue.ToString())) continue;
                    if (!user.FeatureState.TryGetValue(setting.Key, out var featureState)) continue;
                    //var featureState = user.FeatureState[setting.Key];

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
                                returnState = featureState == FeatureState.Enabled
                                    ? FeatureState.Disabled
                                    : FeatureState.Enabled;

                                VipApi.PrintToChat(player,
                                    $"{Localizer[setting.Key]}: {(returnState == FeatureState.Enabled ? $"{Localizer["chat.Enabled"]}" : $"{Localizer["chat.Disabled"]}")}");
                            }

                            user.FeatureState[setting.Key] = returnState;
                            setting.Value.OnSelectItem?.Invoke(controller, returnState);
                        },
                        featureState == FeatureState.NoAccess);
                }
            }

            if (CoreConfig.UseCenterHtmlMenu)
                MenuManager.OpenCenterHtmlMenu(this, player, (CenterHtmlMenu)user.Menu);
            else
                MenuManager.OpenChatMenu(player, (ChatMenu)user.Menu);
        });
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
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid",
                new { AccId = accountId, sid = CoreConfig.ServerId });

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
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = user.account_id,
                    sid = CoreConfig.ServerId
                });

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

    public async Task UpdateUserInDb(User user)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = user.account_id,
                    sid = CoreConfig.ServerId
                });

            if (existingUser == null)
            {
                PrintLogWarning("User does not exist");
                return;
            }

            await connection.ExecuteAsync(@"
            UPDATE 
                vip_users
            SET 
                name = @name,
                lastvisit = @lastvisit,
                `group` = @group,
                expires = @expires
            WHERE account_id = @account_id AND sid = @sid;", user);

            PrintLogInfo("Player '{name} [{accId}]' has been successfully updated", user.name, user.account_id);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task UpdateUserVip(int accountId, string name = "", string group = "", int time = -1)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = accountId,
                    sid = CoreConfig.ServerId
                });

            if (existingUser == null)
            {
                PrintLogWarning($"User with account ID '{accountId}' does not exist");
                return;
            }

            if (!string.IsNullOrEmpty(name))
                existingUser.name = name;

            if (!string.IsNullOrEmpty(group))
                existingUser.group = group;


            if (time > -1)
                existingUser.expires = DateTime.UtcNow.AddSeconds(time).GetUnixEpoch();

            await connection.ExecuteAsync(@"
            UPDATE 
                vip_users
            SET 
                name = @name,
                `group` = @group,
                expires = @expires
            WHERE account_id = @account_id AND sid = @sid;", existingUser);

            PrintLogInfo($"Player '{existingUser.name} [{accountId}]' VIP information has been successfully updated");
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
            await connection.ExecuteAsync(
                "UPDATE vip_users SET expires = @expires WHERE account_id = @account_id AND sid = @sid;", existingUser);
            PrintLogInfo("Player '{name} [{accId}]' expiration time has been updated to {expires}", existingUser.name,
                existingUser.account_id, newExpires);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    //
    // private async Task UpdateUserGroupInDb(int accountId, string newGroup)
    // {
    //     var existingUser = await GetExistingUserFromDb(accountId);
    //
    //     if (existingUser == null)
    //         return;
    //
    //     try
    //     {
    //         existingUser.group = newGroup;
    //         await using var connection = new MySqlConnection(DbConnectionString);
    //         await connection.ExecuteAsync(
    //             " UPDATE vip_users SET `group` = @group WHERE account_id = @account_id AND sid = @sid;",
    //             existingUser);
    //
    //         PrintLogInfo("Player '{name} [{accId}]' group has been updated to {group}", existingUser.name,
    //             existingUser.account_id, newGroup);
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine(e);
    //     }
    // }

    private async Task UpdateUserNameInDb(int accountId, string newName)
    {
        var existingUser = await GetExistingUserFromDb(accountId);
        if (existingUser == null)
            return;
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            existingUser.name = newName;
            await connection.ExecuteAsync(
                "UPDATE vip_users SET name = @name WHERE account_id = @account_id AND sid = @sid;",
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
        WHERE account_id = @AccId AND sid = @sid;", new { AccId = accId, sid = CoreConfig.ServerId });

            PrintLogInfo("Player {name}[{accId}] has been successfully removed", existingUser.name, accId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<List<User?>> GetUserFromDb(int accId)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);
            await connection.OpenAsync();
            var user = await connection.QueryAsync<User?>(
                "SELECT * FROM `vip_users` WHERE `account_id` = @AccId AND sid = @sid AND (expires > @CurrTime OR expires = 0)",
                new { AccId = accId, sid = CoreConfig.ServerId, CurrTime = DateTime.UtcNow.GetUnixEpoch() }
            );

            return user.ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    private async Task RemoveExpiredUsers(CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(DbConnectionString);

            var expiredUsers = await connection.QueryAsync<User>(
                "SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid AND expires < @CurrentTime AND expires > 0",
                new
                {
                    AccId = steamId.AccountId,
                    sid = CoreConfig.ServerId,
                    CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

            foreach (var user in expiredUsers)
            {
                await connection.ExecuteAsync("DELETE FROM vip_users WHERE account_id = @AccId AND sid = @sid",
                    new
                    {
                        AccId = user.account_id,
                        user.sid
                    });

                Server.NextFrame(() =>
                {
                    var authSteamId = player.AuthorizedSteamID;
                    if (authSteamId != null && authSteamId.AccountId == user.account_id)
                        PrintToChat(player, Localizer["vip.Expired", user.group]);

                    VipApi.OnPlayerRemoved(player, user.group);
                });

                PrintLogInfo("User '{name} [{accId}]' has been removed due to expired VIP status.", user.name,
                    user.account_id);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
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
        var authorizedSteamId = player.AuthorizedSteamID;

        if (authorizedSteamId == null)
        {
            PrintLogError("{steamid} is null", "AuthorizedSteamId");
            return false;
        }

        if (!Users.TryGetValue(authorizedSteamId.SteamId64, out var user)) return false;

        if (user.expires != 0 && DateTime.UtcNow.GetUnixEpoch() > user.expires)
        {
            Users.Remove(authorizedSteamId.SteamId64);
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

    public void LoadCore(IApiRegisterer apiRegisterer)
    {
        VipApi = new VipCoreApi(this, ModuleDirectory);
        apiRegisterer.Register<IVipCoreApi>(VipApi);
        _cfg = new Cfg(this);
        if (_cfg != null)
        {
            Config = _cfg.LoadConfig();
            CoreConfig = _cfg.LoadVipSettingsConfig();
        }

        DbConnectionString = BuildConnectionString();
        Task.Run(() => CreateTable(DbConnectionString));
    }

    private static CCSPlayerController? GetPlayerFromSteamId(string steamId)
    {
        return Utilities.GetPlayers().FirstOrDefault(u =>
            u.AuthorizedSteamID != null &&
            (u.AuthorizedSteamID.SteamId2.ToString().Equals(steamId) ||
             u.AuthorizedSteamID.SteamId64.ToString().Equals(steamId) ||
             u.AuthorizedSteamID.AccountId.ToString().Equals(steamId)));
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
    public event Action<CCSPlayerController, string>? PlayerLoaded;
    public event Action<CCSPlayerController, string>? PlayerRemoved;

    public string GetTranslatedText(string name, params object[] args) => _vipCore.Localizer[name, args];

    public string CoreConfigDirectory { get; }
    public string ModulesConfigDirectory => Path.Combine(CoreConfigDirectory, "Modules/");
    public string GetDatabaseConnectionString => _vipCore.DbConnectionString;

    public VipCoreApi(VipCore vipCore, string moduleDirectory)
    {
        _vipCore = vipCore;
        CoreConfigDirectory = new DirectoryInfo(moduleDirectory).Parent?.Parent?.Parent?.Parent?.FullName +
                              "/configs/plugins/VIPCore/";
    }

    public FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        return user.FeatureState.GetValueOrDefault(feature, FeatureState.NoAccess);
    }

    public void RegisterFeature(VipFeatureBase vipFeatureBase,
        FeatureType featureType = FeatureType.Toggle,
        Action<CCSPlayerController, FeatureState>? selectItem = null)
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            if (vipFeatureBase.Feature == null || string.IsNullOrEmpty(vipFeatureBase.Feature)) continue;
            config.Value.Values.TryAdd(vipFeatureBase.Feature, string.Empty);
            foreach (var keyValuePair in config.Value.Values)
            {
                if (string.IsNullOrEmpty(keyValuePair.Value.ToString())) continue;

                _vipCore.Features.TryAdd(vipFeatureBase.Feature, new Feature
                {
                    FeatureType = featureType,
                    OnSelectItem = selectItem
                });
            }
        }

        _vipCore.PrintLogInfo("Feature '{feature}' registered successfully", vipFeatureBase.Feature);
    }

    public void UnRegisterFeature(VipFeatureBase vipFeatureBase)
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            if (vipFeatureBase.Feature != null)
            {
                config.Value.Values.Remove(vipFeatureBase.Feature);
                _vipCore.Features.Remove(vipFeatureBase.Feature);
            }
        }

        _vipCore.PrintLogInfo(
            "Feature '{feature}' unregistered successfully", vipFeatureBase.Feature);
    }

    public IEnumerable<(string feature, object value)> GetAllRegisteredFeatures()
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            foreach (var keyValuePair in config.Value.Values)
            {
                yield return (keyValuePair.Key, keyValuePair.Value);
            }
        }
    }

    public bool IsClientVip(CCSPlayerController player)
    {
        return _vipCore.IsUserActiveVip(player);
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user)) return false;

        if (user is null or { group: null }) return false;

        if (!_vipCore.Config.Groups.TryGetValue(user.group, out var vipGroup)) return false;

        foreach (var vipGroupValue in vipGroup.Values.Where(vipGroupValue => vipGroupValue.Key == feature))
        {
            return !string.IsNullOrEmpty(vipGroupValue.Value.ToString());
        }

        return false;
    }

    public string GetClientVipGroup(CCSPlayerController player)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        return user.group;
    }

    public void UpdateClientVip(CCSPlayerController player, string name = "", string group = "", int time = -1)
    {
        var index = player.Index;
        var steamId =
            new SteamID(player.AuthorizedSteamID == null ? player.SteamID : player.AuthorizedSteamID.SteamId64);

        Task.Run(() => _vipCore.UpdateUserVip(steamId.AccountId, name, group, time));
        Task.Run(() => UpdateUsers(name, steamId.AccountId, index, steamId.SteamId64));
        OnPlayerLoaded(player, group);
    }

    public void SetClientVip(CCSPlayerController player, string group, int time)
    {
        var index = player.Index;
        var name = player.PlayerName;

        var authSteamId = player.AuthorizedSteamID;
        if (authSteamId == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var accountId = authSteamId.AccountId;
        var steamId64 = authSteamId.SteamId64;

        OnPlayerLoaded(player, group);
        Task.Run(() => SetClientVipAsync(name, accountId, index, group, time, steamId64));
    }

    private async Task SetClientVipAsync(string name, int accountId, uint index, string group, int time,
        ulong steamId64)
    {
        try
        {
            await _vipCore.UpdateUserInDb(new User
            {
                account_id = accountId,
                name = name,
                lastvisit = DateTime.UtcNow.GetUnixEpoch(),
                sid = _vipCore.CoreConfig.ServerId,
                group = group,
                expires = time == 0 ? 0 : DateTime.UtcNow.AddSeconds(time).GetUnixEpoch()
            });

            await UpdateUsers(name, accountId, index, steamId64);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void GiveClientVip(CCSPlayerController player, string group, int time)
    {
        var index = player.Index;
        var name = player.PlayerName;

        var authSteamId = player.AuthorizedSteamID;
        if (authSteamId == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var accountId = authSteamId.AccountId;
        var steamId64 = authSteamId.SteamId64;

        OnPlayerLoaded(player, group);
        Task.Run(() => GiveClientVipAsync(name, accountId, index, group, time, steamId64));
    }

    public void RemoveClientVip(CCSPlayerController player)
    {
        var index = player.Index;
        var steamId = new SteamID(player.SteamID);

        if (!_vipCore.Users.TryGetValue(steamId.SteamId64, out var user))
            throw new InvalidOperationException("player not found");

        OnPlayerRemoved(player, user.group);
        Task.Run(() => RemoveClientVipAsync(index, steamId));
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

    public void OnPlayerLoaded(CCSPlayerController player, string group)
    {
        PlayerLoaded?.Invoke(player, group);
    }

    public void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        PlayerRemoved?.Invoke(player, group);
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
                sid = _vipCore.CoreConfig.ServerId,
                group = group,
                expires = timeSeconds == 0 ? 0 : DateTime.UtcNow.AddSeconds(timeSeconds).GetUnixEpoch()
            });

            await UpdateUsers(username, accountId, index, steamId64);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateUsers(string username, int accountId, uint index, ulong steamId64)
    {
        foreach (var user in await _vipCore.GetUserFromDb(accountId))
        {
            if (user != null)
            {
                if (user.sid != _vipCore.CoreConfig.ServerId) continue;

                _vipCore.Users[steamId64] = new User
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
                return;
            }

            _vipCore.PrintLogError(
                "This user '{username} [{accountId}]' already has VIP", username, accountId);
            return;
        }
    }

    private async Task RemoveClientVipAsync(uint index, SteamID steamId)
    {
        try
        {
            await _vipCore.RemoveUserFromDb(steamId.AccountId);
            _vipCore.Users.Remove(steamId.SteamId64);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public T GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("User not found.");

        if (_vipCore.Config.Groups.TryGetValue(user.group, out var vipGroup))
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

    public void SetPlayerCookie<T>(ulong steamId64, string key, T value)
    {
        var cookies = LoadCookies();

        if (value != null)
        {
            var existingCookie = cookies.FirstOrDefault(c => c.SteamId64 == steamId64);

            if (existingCookie != null)
                existingCookie.Features[key] = value;
            else
            {
                var newCookie = new PlayerCookie
                {
                    SteamId64 = steamId64,
                    Features = new Dictionary<string, object> { { key, value } }
                };
                cookies.Add(newCookie);
            }

            SaveCookies(cookies);
        }
    }

    public T GetPlayerCookie<T>(ulong steamId64, string key)
    {
        var cookies = LoadCookies();

        var cookie = cookies.FirstOrDefault(c => c.SteamId64 == steamId64);

        if (cookie != null && cookie.Features.TryGetValue(key, out var jsonElement))
        {
            try
            {
                var stringValue = jsonElement.ToString();
                var deserializedValue = (T)Convert.ChangeType(stringValue, typeof(T))!;
                return deserializedValue;
            }
            catch (Exception)
            {
                _vipCore.PrintLogError("Failed to deserialize feature '{feature}' value.", key);
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