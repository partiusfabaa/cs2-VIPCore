using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using VipCoreApi;

namespace VIP_NightVip;

public class VIP_NightVipConfig
{
    public string VIPGroup { get; set; } = "vip";
    public string PluginStartTime { get; set; } = "20:00:00";
    public string PluginEndTime { get; set; } = "08:00:00";
    public string Timezone { get; set; } = "UTC";
    public int CheckTimer { get; set; } = 10;
    public string VipGrantedMessage { get; set; } = "You are receiving VIP because it's VIP Night time.";
    public string Tag { get; set; } = "[NightVIP]";
}

[MinimumApiVersion(276)]
public class VIP_NightVip : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Night VIP";
    public override string ModuleVersion => "v1.2";
    public override string ModuleDescription => "Gives VIP between a certain period of time.";
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    private VIP_NightVipConfig Config = null!;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        Config = _api.LoadConfig<VIP_NightVipConfig>("vip_night") ?? CreateConfig("vip_night");

        Console.WriteLine($"Configuration loaded: {JsonSerializer.Serialize(Config)}");

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (!IsPlayerValid(player)) return HookResult.Continue;

            GiveVIP(player);
            return HookResult.Continue;
        });

        AddTimer(Config.CheckTimer, CheckAndGiveVIP, TimerFlags.REPEAT);
    }

    private void CheckAndGiveVIP()
    {
        if (_api == null) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsPlayerValid(player)) continue;

            if (!_api.IsClientVip(player))
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
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(Config.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            Console.WriteLine($"Invalid timezone: {Config.Timezone}. Defaulting to UTC.");
            timeZoneInfo = TimeZoneInfo.Utc;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error with timezone: {ex.Message}. Defaulting to UTC.");
            timeZoneInfo = TimeZoneInfo.Utc;
        }

        var currentTimeInTimeZone = TimeZoneInfo.ConvertTimeFromUtc(currentTime, timeZoneInfo);
        var startTime = TimeSpan.Parse(Config.PluginStartTime);
        var endTime = TimeSpan.Parse(Config.PluginEndTime);

        bool isVipTime = startTime < endTime
            ? currentTimeInTimeZone.TimeOfDay >= startTime && currentTimeInTimeZone.TimeOfDay < endTime
            : currentTimeInTimeZone.TimeOfDay >= startTime || currentTimeInTimeZone.TimeOfDay < endTime;

        if (!isVipTime || _api.IsClientVip(player)) return;

        var remainingTime = CalculateRemainingVipTime(endTime, currentTimeInTimeZone.TimeOfDay);
        _api.GiveClientTemporaryVip(player, Config.VIPGroup, (int)remainingTime);
        _api.PrintToChat(player, $" \x02{Config.Tag} \x01{Config.VipGrantedMessage}");
    }

    private double CalculateRemainingVipTime(TimeSpan endTime, TimeSpan currentTime)
    {
        return endTime > currentTime
            ? (endTime - currentTime).TotalMinutes
            : (TimeSpan.FromHours(24) - currentTime + endTime).TotalMinutes;
    }

    private bool IsPlayerValid(CCSPlayerController? player)
    {
        return player != null
            && player.IsValid
            && !player.IsBot
            && !player.IsHLTV
            && player.PlayerPawn.IsValid
            && player.PawnIsAlive;
    }

    private VIP_NightVipConfig CreateConfig(string configPath)
    {
        var config = new VIP_NightVipConfig
        {
            VIPGroup = "vip",
            PluginStartTime = "20:00:00",
            PluginEndTime = "08:00:00",
            Timezone = "UTC",
            CheckTimer = 10,
            VipGrantedMessage = "You are receiving VIP because it's VIP Night time.",
            Tag = "[NightVIP]"
        };

        try
        {
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write configuration file: {ex.Message}");
        }

        return config;
    }
}
