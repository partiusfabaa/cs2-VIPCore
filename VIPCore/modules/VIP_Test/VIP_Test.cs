using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using MySqlConnector;
using VipCoreApi;

namespace VIP_Test;

public class VipTest : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Test";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "vip_test_count";
    private IVipCoreApi? _api;
    private Config _config = null!;
    
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;
        _config = LoadConfig();
        Task.Run(CreateVipTestTable);
    }

    [ConsoleCommand("css_viptest")]
    public void OnCommandVipTest(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        if (!_config.VipTestEnabled) return;

        if (_api.IsClientVip(controller))
        {
            _api.PrintToChat(controller, _api.GetTranslatedText("vip.AlreadyVipPrivileges"));
            return;
        }

        var authorizedSteamId = controller.AuthorizedSteamID;

        if (authorizedSteamId == null) return;

        Task.Run(() => GivePlayerVipTest(controller, authorizedSteamId, _config));
    }

    private async void GivePlayerVipTest(CCSPlayerController player, SteamID steamId, Config vipTest)
    {
        var vipTestEndTime = await GetEndTime(steamId.SteamId2);
        var vipTestCount = _api.GetPlayerCookie<int>(steamId.SteamId64, Feature);
        
        if (vipTestCount >= vipTest.VipTestCount)
        {
            Server.NextFrame(() =>
                _api.PrintToChat(player, _api.GetTranslatedText("viptest.YouCanNoLongerTakeTheVip")));
            return;
        }

        if (vipTestEndTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(vipTestEndTime) - DateTimeOffset.UtcNow;
            var timeRemainingFormatted =
                $"{(time.Days == 0 ? "" : $"{time.Days}d")} {time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";

            Server.NextFrame(() =>
                _api.PrintToChat(player, _api.GetTranslatedText("viptest.RetakenThrough", timeRemainingFormatted)));
            return;
        }

        var coolDownTime = DateTimeOffset.UtcNow.AddSeconds(vipTest.VipTestCooldown).ToUnixTimeSeconds();
        var endTime = DateTimeOffset.UtcNow.AddSeconds(vipTest.VipTestDuration).ToUnixTimeSeconds();

        await AddUserOrUpdateVipTestAsync(steamId.SteamId2, (int)coolDownTime);
        _api.SetPlayerCookie(steamId.SteamId64, Feature, vipTestCount + 1);

        var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(endTime) - DateTimeOffset.UtcNow;

        Server.NextFrame(() =>
        {
            _api.PrintToChat(player,
                _api.GetTranslatedText("viptest.SuccessfullyPassed",
                    timeRemaining.ToString(timeRemaining.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss")));
            _api.GiveClientVip(player, vipTest.VipTestGroup, vipTest.VipTestDuration);
        });
    }

    private async Task AddUserOrUpdateVipTestAsync(string steamId, int endTime)
    {
        if (await IsUserInVipTest(steamId))
        {
            await UpdateUserVipTestCount(steamId, endTime);
            return;
        }

        await AddUserToVipTest(steamId, endTime);
    }

    private async Task AddUserToVipTest(string steamId, long endTime)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_api.GetDatabaseConnectionString);
            dbConnection.Open();

            var insertUserQuery = @"
            INSERT INTO `vipcore_test` (`steamid`, `end_time`)
            VALUES (@SteamId, @EndTime);";

            await dbConnection.ExecuteAsync(insertUserQuery,
                new { SteamId = steamId, EndTime = endTime });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateUserVipTestCount(string steamId, long endTime)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_api.GetDatabaseConnectionString);
            dbConnection.Open();

            var updateCountQuery = @"
            UPDATE `vipcore_test`
            SET `end_time` = @EndTime
            WHERE `steamid` = @SteamId;";

            await dbConnection.ExecuteAsync(updateCountQuery,
                new { SteamId = steamId, EndTime = endTime });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task<long> GetEndTime(string steamId)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_api.GetDatabaseConnectionString);
            dbConnection.Open();
    
            var result = await dbConnection.QuerySingleOrDefaultAsync<long>(@"
            SELECT `end_time` FROM `vipcore_test` WHERE `steamid` = @SteamId;",
                new { SteamId = steamId });
    
            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 0;
        }
    }

    private async Task<bool> IsUserInVipTest(string steamId)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_api.GetDatabaseConnectionString);
            dbConnection.Open();

            var checkUserQuery = @"
            SELECT COUNT(*)
            FROM `vipcore_test`
            WHERE `steamid` = @SteamId;";

            var count = dbConnection.ExecuteScalarAsync<int>(checkUserQuery, new { SteamId = steamId }).Result;

            return count > 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private async Task CreateVipTestTable()
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_api.GetDatabaseConnectionString);
            dbConnection.Open();

            var createKeysTable = @"
            CREATE TABLE IF NOT EXISTS `vipcore_test` (
                `steamid` VARCHAR(255) NOT NULL PRIMARY KEY,
                `end_time` BIGINT NOT NULL
            );";

            await dbConnection.ExecuteAsync(createKeysTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private Config LoadConfig()
    {
        var configPath = Path.Combine(_api.ModulesConfigDirectory, "vip_test.json");

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            VipTestEnabled = true,
            VipTestDuration = 3600,
            VipTestCooldown = 86400,
            VipTestGroup = "group_name",
            VipTestCount = 2
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return config;
    }
}

public class Config
{
    public bool VipTestEnabled { get; init; }
    public int VipTestDuration { get; init; }
    public int VipTestCooldown { get; init; }
    public required string VipTestGroup { get; init; }
    public int VipTestCount { get; init; }
}