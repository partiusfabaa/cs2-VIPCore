﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using MySqlConnector;
using VipCoreApi;

namespace VIP_Test;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Test";
    public override string ModuleVersion => "v2.0.0";

    private readonly Func<VipTestConfig, string> _feature = config => $"vip_test_count_{config.Group}";
    private IVipCoreApi? _api;
    private List<VipTestConfig> _config = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _config = _api.LoadConfig<List<VipTestConfig>>("vip_test");
        Task.Run(CreateVipTestTable);
    }

    [ConsoleCommand("css_viptest")]
    public void OnCommandVipTest(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !controller.IsValid) return;

        if (_api!.IsPlayerVip(controller))
        {
            _api.PrintToChat(controller, _api.GetTranslatedText("vip.AlreadyVipPrivileges"));
            return;
        }

        var menu = _api.CreateMenu("VIP Test");
        foreach (var vip in _config)
        {
            menu.AddMenuOption(vip.Group, (p, _) =>
            {
                var authorizedSteamId = p.AuthorizedSteamID;
                if (authorizedSteamId == null) return;

                Task.Run(() => GivePlayerVipTest(p, authorizedSteamId, vip));
            });
        }

        menu.Open(controller);
    }

    private async Task GivePlayerVipTest(CCSPlayerController player, SteamID steamId, VipTestConfig vipTest)
    {
        try
        {
            var vipTestEndTime = await GetEndTime(steamId.SteamId2);
            var vipTestCount = _api!.GetPlayerCookie<int>(steamId.SteamId64, _feature(vipTest));

            if (vipTestCount >= vipTest.Count)
            {
                Server.NextFrame(() =>
                    _api.PrintToChat(player, _api.GetTranslatedText("viptest.YouCanNoLongerTakeTheVip")));
                return;
            }

            if (vipTestEndTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                var timeLeft = DateTimeOffset.FromUnixTimeSeconds(vipTestEndTime) - DateTimeOffset.UtcNow;
                var formattedTime =
                    $"{(timeLeft.Days > 0 ? $"{timeLeft.Days}d " : "")}{timeLeft.Hours:D2}:{timeLeft.Minutes:D2}:{timeLeft.Seconds:D2}";

                Server.NextFrame(() =>
                    _api.PrintToChat(player, _api.GetTranslatedText("viptest.RetakenThrough", formattedTime)));
                return;
            }

            var newCoolDown = DateTimeOffset.UtcNow.AddSeconds(vipTest.Cooldown).ToUnixTimeSeconds();
            await AddUserOrUpdateVipTestAsync(steamId.SteamId2, (int)newCoolDown);

            _api.SetPlayerCookie(steamId.SteamId64, _feature(vipTest), vipTestCount + 1);

            var vipDuration = DateTimeOffset.UtcNow.AddSeconds(vipTest.Duration);
            Server.NextFrame(() =>
            {
                _api.GivePlayerVip(player, vipTest.Group, vipTest.Duration);
                _api.PrintToChat(player, _api.GetTranslatedText("viptest.SuccessfullyPassed", vipTest.Duration.FormatTime()));
                _api.PrintToChat(player, _api.GetTranslatedText("viptest.RemainingAttempts", vipTest.Count - (vipTestCount + 1)));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in GivePlayerVipTest: {e}");
        }
    }

    private async Task AddUserOrUpdateVipTestAsync(string steamId, int endTime)
    {
        try
        {
            if (await IsUserInVipTest(steamId))
                await UpdateUserVipTest(steamId, endTime);
            else
                await AddUserToVipTest(steamId, endTime);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task AddUserToVipTest(string steamId, long endTime)
    {
        try
        {
            await using var db = new MySqlConnection(_api!.DatabaseConnectionString);
            await db.OpenAsync();

            await db.ExecuteAsync(
                "INSERT INTO `vipcore_test` (`steamid`, `end_time`) VALUES (@SteamId, @EndTime)",
                new { SteamId = steamId, EndTime = endTime });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task UpdateUserVipTest(string steamId, long endTime)
    {
        try
        {
            await using var db = new MySqlConnection(_api!.DatabaseConnectionString);
            await db.OpenAsync();

            await db.ExecuteAsync(
                "UPDATE `vipcore_test` SET `end_time` = @EndTime WHERE `steamid` = @SteamId",
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
            await using var db = new MySqlConnection(_api!.DatabaseConnectionString);
            await db.OpenAsync();

            return await db.QuerySingleOrDefaultAsync<long>(
                "SELECT `end_time` FROM `vipcore_test` WHERE `steamid` = @SteamId",
                new { SteamId = steamId });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return -1;
    }

    private async Task<bool> IsUserInVipTest(string steamId)
    {
        try
        {
            await using var db = new MySqlConnection(_api!.DatabaseConnectionString);
            await db.OpenAsync();

            var count = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM `vipcore_test` WHERE `steamid` = @SteamId",
                new { SteamId = steamId });
            
            return count > 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return false;
    }

    private async Task CreateVipTestTable()
    {
        try
        {
            await using var db = new MySqlConnection(_api!.DatabaseConnectionString);
            await db.OpenAsync();
            await db.ExecuteAsync(
                "CREATE TABLE IF NOT EXISTS `vipcore_test` (" +
                "`steamid` VARCHAR(255) NOT NULL PRIMARY KEY, " +
                "`end_time` BIGINT NOT NULL)");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}

public class VipTestConfig
{
    public required string Group { get; init; }
    public int Duration { get; init; }
    public int Cooldown { get; init; }
    public int Count { get; init; }
}

public static class TimeExtensions
{
    public static string FormatTime(this int seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.Days > 0 ? $"{span.Days}d {span.Hours}h" :
            span.Hours > 0 ? $"{span.Hours}h {span.Minutes}m" :
            $"{span.Minutes}m {span.Seconds}s";
    }
}