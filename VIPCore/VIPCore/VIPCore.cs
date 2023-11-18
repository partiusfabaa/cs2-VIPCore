using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using MySqlConnector;
using VipCoreApi;
using ChatMenu = CounterStrikeSharp.API.Modules.Menu.ChatMenu;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace VIPCore;

public class VipCore : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Core";
    public override string ModuleVersion => "v1.0.0";

    private string _dbConnectionString = string.Empty;
    private Cfg? _cfg;
    private readonly ChatMenu?[] _vipMenu = new ChatMenu?[Server.MaxPlayers];

    public Config? Config;
    public readonly User?[] Users = new User[Server.MaxPlayers];
    public readonly Dictionary<string, Action<CCSPlayerController>> UserSettings = new();
    public bool IsCoreLoad;

    public VipCore()
    {
        AddApi<IVipCoreApi>(new VipCoreApi(this));
    }

    public override void Load(bool hotReload)
    {
        IsCoreLoad = true;
        _cfg = new Cfg(this);
        Config = _cfg!.LoadConfig();
        _dbConnectionString = BuildConnectionString();
        Task.Run(() => CreateTable(_dbConnectionString));

        RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            _vipMenu[slot + 1] = new ChatMenu("[\x0CVIP Menu\x01]");
            Task.Run(() => OnClientConnectedAsync(slot, Utilities.GetPlayerFromSlot(slot)));
        });

        RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            Users[slot + 1] = null;
            _vipMenu[slot + 1] = null;
        });

        CreateMenu();

        AddTimer(300, () => Task.Run(RemoveExpiredUsers), TimerFlags.REPEAT);
    }

    // [ConsoleCommand("Test")]
    // public void OncMDtEST(CCSPlayerController? player, CommandInfo infi)
    // {
    //     if (player == null) return;
    //
    //     var user = Users[player.EntityIndex!.Value.Value];
    //
    //     if (user != null)
    //     {
    //         Server.PrintToChatAll($"SteamId: {user.steamid} | Group: {user.vip_group}");
    //     }
    // }

    private async Task OnClientConnectedAsync(int playerSlot, CCSPlayerController player)
    {
        var msg = await RemoveExpiredUsers();
        PrintToServer(msg, ConsoleColor.DarkGreen);

        var user = await GetUserFromDb(new SteamID(player.SteamID));

        if (user == null)
        {
            Console.WriteLine("USER == NULL");
            return;
        }

        Users[playerSlot + 1] = new User
        {
            steamid = user.steamid,
            vip_group = user.vip_group,
            start_vip_time = user.start_vip_time,
            end_vip_time = user.end_vip_time
        };

        Console.WriteLine("ADD USER TO USERS");
    }

    [ConsoleCommand("css_vip_adduser")]
    public void OnCmdAddUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var splitCmdArgs = ParseCommandArguments(command.ArgString);

        if (splitCmdArgs.Length is > 3 or < 3)
        {
            PrintToServer("Usage: css_vip_adduser <steamid> <vipgroup> <time_second>", ConsoleColor.Red);
            return;
        }

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);
        var vipGroup = ExtractValueInQuotes(splitCmdArgs[1]);
        var endVipTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[2]));

        if (!Config!.Groups.ContainsKey(vipGroup))
        {
            PrintToServer("This VIP group was not found!", ConsoleColor.DarkRed);
            return;
        }

        Task.Run(() => AddUserToDb(new User
        {
            steamid = steamId,
            vip_group = vipGroup,
            start_vip_time = DateTime.UtcNow.GetUnixEpoch(),
            end_vip_time = endVipTime == 0 ? 0 : DateTime.UtcNow.AddSeconds(endVipTime).GetUnixEpoch()
        }));
    }

    [ConsoleCommand("css_vip_deleteuser")]
    public void OnCmdDeleteVipUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var splitCmdArgs = ParseCommandArguments(command.ArgString);

        if (splitCmdArgs.Length is < 1 or > 1)
        {
            ReplyToCommand(controller, "Using: css_vip_deleteuser <steamid>");
            return;
        }

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);

        Task.Run(() => RemoveUserFromDb(steamId));
    }

    [RequiresPermissions("@css/root", "@vip/vip")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        if (_cfg != null) Config = _cfg.LoadConfig();

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
                PrintToChat(player, "You do not have access to this command!");
                return;
            }

            var index = player.EntityIndex!.Value.Value;

            _vipMenu[index]!.MenuOptions.Clear();

            if (Config != null)
            {
                if (Config.Groups.TryGetValue(Users[index]!.vip_group, out var vipGroup))
                {
                    foreach (var setting in UserSettings)
                    {
                        if (vipGroup.Values.TryGetValue(setting.Key, out var featureValue))
                        {
                            if (string.IsNullOrEmpty(featureValue.ToString())) return;

                            _vipMenu[index]!.AddMenuOption(setting.Key,
                                (controller, _) => setting.Value(controller));
                        }
                    }
                }
            }

            ChatMenus.OpenMenu(player, _vipMenu[index]!);
        });
    }

    private string[] ParseCommandArguments(string argString)
    {
        var parse = Regex.Matches(argString, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value.Trim('"'))
            .ToArray();

        return parse;
    }

    private string ExtractValueInQuotes(string input)
    {
        var match = Regex.Match(input, @"""([^""]*)""");

        return match.Success ? match.Groups[1].Value : input;
    }

    private string BuildConnectionString()
    {
        Console.WriteLine("Building connection string");
        if (Config != null)
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

        return string.Empty;
    }

    private async Task CreateTable(string connectionString)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(connectionString);
            dbConnection.Open();

            var createVipUsersTable = @"
            CREATE TABLE IF NOT EXISTS `vipcore_users` (
                `steamid` VARCHAR(255)  NOT NULL PRIMARY KEY,
                `vip_group` VARCHAR(255) NOT NULL,
                `start_vip_time` BIGINT NOT NULL,
                `end_vip_time` BIGINT NOT NULL
            );";

            await dbConnection.ExecuteAsync(createVipUsersTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task AddUserToDb(User user)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vipcore_users WHERE steamid = @SteamId", new { SteamId = user.steamid });

            if (existingUser != null)
            {
                PrintToServer("User already exists", ConsoleColor.Yellow);
                return;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO vipcore_users (steamid, vip_group, start_vip_time, end_vip_time)
                VALUES (@steamid, @vip_group, @start_vip_time, @end_vip_time);", user);

            PrintToServer($"Player '{user.steamid}' has been successfully added", ConsoleColor.Green);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task RemoveUserFromDb(string steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vipcore_users WHERE steamid = @SteamId", new { SteamId = steamId });

            if (existingUser == null)
            {
                PrintToServer("User does not exist", ConsoleColor.Red);
                return;
            }

            await connection.ExecuteAsync(@"
            DELETE FROM vipcore_users
            WHERE steamid = @SteamId;", new { SteamId = steamId });

            PrintToServer($"Player '{steamId}' has been successfully removed", ConsoleColor.Red);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<User?> GetUserFromDb(SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM `vipcore_users` WHERE `steamid` = @SteamId", new { SteamId = steamId.SteamId2 });

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
            await using var connection = new MySqlConnection(_dbConnectionString);

            var expiredUsers = await connection.QueryAsync<User>(
                "SELECT * FROM vipcore_users WHERE end_vip_time < @CurrentTime AND end_vip_time > 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            foreach (var user in expiredUsers)
            {
                await connection.ExecuteAsync("DELETE FROM vipcore_users WHERE steamid = @SteamId",
                    new { SteamId = user.steamid });

                Console.WriteLine($"User {user.steamid} has been removed due to expired VIP status.");
            }

            return "Expired users removed successfully.";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    public async Task<string> GetVipGroupFromDatabase(string steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT vip_group FROM vipcore_users WHERE steamid = @SteamId",
                new { SteamId = steamId });

            if (user != null) return user.vip_group;

            PrintToServer("User not found", ConsoleColor.DarkRed);
            return string.Empty;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public bool IsUserActiveVip(CCSPlayerController player)
    {
        var index = player.EntityIndex!.Value.Value;
        var user = Users[index];
        if (user == null) return false;

        if (user.end_vip_time != 0 && DateTime.UtcNow.GetUnixEpoch() > user.end_vip_time)
        {
            Users[index] = null;
            return false;
        }

        return user.end_vip_time == 0 || DateTime.UtcNow.GetUnixEpoch() < user.end_vip_time;
    }

    private void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            PrintToChat(controller, msg);
        else
            PrintToServer($"{msg}", ConsoleColor.DarkMagenta);
    }

    public void PrintToChat(CCSPlayerController player, string msg)
    {
        player.PrintToChat($"\x08[ \x0CVIPCore \x08] {msg}");
    }

    private void PrintToServer(string msg, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[VIPCore] {msg}");
        Console.ResetColor();
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

    public VipCoreApi(VipCore vipCore)
    {
        _vipCore = vipCore;
    }

    public void RegisterFeature(string feature, Action<CCSPlayerController> selectItem)
    {
        foreach (var config in _vipCore.Config!.Groups)
        {
            if (feature != null)
            {
                config.Value.Values.TryAdd(feature, string.Empty);
                foreach (var keyValuePair in config.Value.Values)
                {
                    if (string.IsNullOrEmpty(keyValuePair.Value.ToString())) continue;

                    _vipCore.UserSettings.TryAdd(feature, selectItem);
                }
            }
        }

        Console.WriteLine($"Feature '{feature}' registered successfully");
    }

    public void UnRegisterFeature(string feature)
    {
        foreach (var config in _vipCore.Config!.Groups)
        {
            if (feature != null)
            {
                config.Value.Values.Remove(feature);
                _vipCore.UserSettings.Remove(feature);
            }
        }

        Console.WriteLine($"Feature '{feature}' unregistered successfully");
    }

    public bool IsClientVip(CCSPlayerController player)
    {
        return _vipCore.IsUserActiveVip(player);
    }

    public bool IsClientFeature(CCSPlayerController player, string feature)
    {
        var index = player.EntityIndex!.Value.Value;
        var user = _vipCore.Users[index];

        if (user is null or { vip_group: null }) return false;

        if (_vipCore.Config == null) return false;

        if (_vipCore.Config.Groups.TryGetValue(user.vip_group, out var vipGroup))
        {
            return vipGroup.Values.ContainsKey(feature);
        }

        Console.WriteLine("Couldn't find VipGroup in Config.Groups.");
        return false;
    }

    public string GetClientVipGroup(CCSPlayerController player)
    {
        return Task.Run(() => _vipCore.GetVipGroupFromDatabase(new SteamID(player.SteamID).SteamId2)).Result;
    }

    public void GiveClientVip(CCSPlayerController player, string group, int time)
    {
        Task.Run(() => GiveClientVipAsync(player, group, time));
    }

    public void RemoveClientVip(CCSPlayerController player)
    {
        Task.Run(() => RemoveClientVipAsync(player));
    }

    public void PrintToChat(CCSPlayerController player, string message)
    {
        _vipCore.PrintToChat(player, message);
    }

    public bool VipCoreLoad()
    {
        return _vipCore.IsCoreLoad;
    }

    private async Task GiveClientVipAsync(CCSPlayerController player, string group, int timeSeconds)
    {
        var steamId = new SteamID(player.SteamID);
        await _vipCore.AddUserToDb(new User
        {
            steamid = steamId.SteamId2,
            vip_group = group,
            start_vip_time = DateTime.UtcNow.GetUnixEpoch(),
            end_vip_time = timeSeconds == 0 ? timeSeconds : DateTime.UtcNow.AddSeconds(timeSeconds).GetUnixEpoch()
        });

        var user = await _vipCore.GetUserFromDb(steamId);

        if (user != null)
        {
            _vipCore.Users[player.EntityIndex!.Value.Value] = new User
            {
                steamid = steamId.SteamId2,
                vip_group = user.vip_group,
                start_vip_time = user.start_vip_time,
                end_vip_time = user.end_vip_time
            };
        }
    }

    private async Task RemoveClientVipAsync(CCSPlayerController player)
    {
        await _vipCore.RemoveUserFromDb(new SteamID(player.SteamID).SteamId2);
        _vipCore.Users[player.EntityIndex!.Value.Value] = null;
    }

    public T GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        var user = _vipCore.Users[player.EntityIndex!.Value.Value];

        if (user == null || string.IsNullOrEmpty(user.vip_group))
            return default(T)!;

        if (_vipCore.Config?.Groups.TryGetValue(user.vip_group, out var vipGroup) == true)
        {
            if (vipGroup.Values.TryGetValue(feature, out var value))
            {
                Console.WriteLine($"Checking feature: {feature} - {value}");
                try
                {
                    var stringValue = value.ToString();
                    var deserializedValue = JsonSerializer.Deserialize<T>(stringValue!);
                    return deserializedValue!;
                }
                catch (JsonException)
                {
                    Console.WriteLine($"Failed to deserialize feature '{feature}' value: {value}");
                }
            }
        }

        Console.WriteLine($"Feature not found, returning default value: {string.Empty}");
        return default(T)!;
    }
}

public class User
{
    public required string steamid { get; set; }
    public required string vip_group { get; set; }
    public int start_vip_time { get; set; }
    public int end_vip_time { get; set; }
}

// public int GetFeatureIntValue(CCSPlayerController player, string feature)
// {
//     return IsClientFeature(player, feature) ? int.Parse(GetFeatureValue(player, feature)) : int.MinValue;
// }
//
// public float GetFeatureFloatValue(CCSPlayerController player, string feature)
// {
//     return IsClientFeature(player, feature) ? float.Parse(GetFeatureValue(player, feature)) : float.MinValue;
// }
//
// public string GetFeatureStringValue(CCSPlayerController player, string feature)
// {
//     return IsClientFeature(player, feature) ? GetFeatureValue(player, feature) : string.Empty;
// }
//
// public bool GetFeatureBoolValue(CCSPlayerController player, string feature)
// {
//     return IsClientFeature(player, feature) && int.Parse(GetFeatureValue(player, feature)) == 1;
// }

// private string GetFeatureValue(CCSPlayerController player, string feature)
// {
//     var user = _vipCore.Users[player.EntityIndex!.Value.Value];
//
//     if (user == null || string.IsNullOrEmpty(user.vip_group)) return string.Empty;
//
//     if (_vipCore.Config?.Groups.TryGetValue(user.vip_group, out var vipGroup) == true)
//     {
//         if (vipGroup.Values.TryGetValue(feature, out var value))
//         {
//             Console.WriteLine($"Checking feature: {feature} - {value}");
//             return value;
//         }
//     }
//
//     Console.WriteLine($"Feature not found, returning default value: {string.Empty}");
//     return string.Empty;
// }