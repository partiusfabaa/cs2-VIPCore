using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using VipCoreApi;

namespace VIP_NightVip;

public class NightVipConfig
{
    public string VIPGroup { get; set; } = "vip";
    public string PluginStartTime { get; set; } = "20:00:00";
    public string PluginEndTime { get; set; } = "08:00:00";
    public string Timezone { get; set; } = "UTC";
    public int CheckTimer { get; set; } = 10;
    public string VipGrantedMessage { get; set; } = "You are receiving VIP because it's VIP Night time.";
    public string Tag { get; set; } = "[NightVIP]";
}

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Night VIP";
    public override string ModuleVersion => "v2.0.0";
    public override string ModuleDescription => "Gives VIP between a certain period of time.";

    private IVipCoreApi? _api;
    private NightVipConfig? _config;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _config = _api.LoadConfig<NightVipConfig>("vip_night");

        Console.WriteLine($"Configuration loaded: {JsonSerializer.Serialize(_config)}");

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (!IsPlayerValid(player)) return HookResult.Continue;

            GiveVIP(player);
            return HookResult.Continue;
        });

        AddTimer(_config.CheckTimer, CheckAndGiveVIP, TimerFlags.REPEAT);
    }

    private void CheckAndGiveVIP()
    {
        if (_api == null) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsPlayerValid(player)) continue;

            if (!_api.IsPlayerVip(player))
                GiveVIP(player);
        }
    }

    private void GiveVIP(CCSPlayerController? player)
    {
        if (_api == null || !IsPlayerValid(player) || player == null) return;

        var currentTime = DateTime.UtcNow;
        TimeZoneInfo timeZoneInfo;
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(_config.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            Console.WriteLine($"Invalid timezone: {_config.Timezone}. Defaulting to UTC.");
            timeZoneInfo = TimeZoneInfo.Utc;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error with timezone: {ex.Message}. Defaulting to UTC.");
            timeZoneInfo = TimeZoneInfo.Utc;
        }

        var currentTimeInTimeZone = TimeZoneInfo.ConvertTimeFromUtc(currentTime, timeZoneInfo);
        var startTime = TimeSpan.Parse(_config.PluginStartTime);
        var endTime = TimeSpan.Parse(_config.PluginEndTime);

        bool isVipTime = startTime < endTime
            ? currentTimeInTimeZone.TimeOfDay >= startTime && currentTimeInTimeZone.TimeOfDay < endTime
            : currentTimeInTimeZone.TimeOfDay >= startTime || currentTimeInTimeZone.TimeOfDay < endTime;

        if (!isVipTime || _api.IsPlayerVip(player)) return;

        var remainingTime = CalculateRemainingVipTime(endTime, currentTimeInTimeZone.TimeOfDay);
        _api.GivePlayerTemporaryVip(player, _config.VIPGroup, (int)remainingTime);
        _api.PrintToChat(player, $" \x02{_config.Tag} \x01{_config.VipGrantedMessage}");
    }

    private double CalculateRemainingVipTime(TimeSpan endTime, TimeSpan currentTime)
    {
        return endTime > currentTime
            ? (endTime - currentTime).TotalMinutes
            : (TimeSpan.FromHours(24) - currentTime + endTime).TotalMinutes;
    }

    private bool IsPlayerValid(CCSPlayerController? player)
    {
        return player != null && player is
            { IsValid: true, IsBot: false, IsHLTV: false, PlayerPawn.IsValid: true, PawnIsAlive: true };
    }
}