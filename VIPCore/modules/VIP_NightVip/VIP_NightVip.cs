using System;
using System.IO;
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
    public bool Debug { get; set; } = false;
}

[MinimumApiVersion(346)]
public class VIP_NightVip : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Night VIP";
    public override string ModuleVersion => "v1.3";
    public override string ModuleDescription => "Gives VIP between a certain period of time.";

    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    private VIP_NightVipConfig Config = null!;

    private TimeZoneInfo _timeZoneInfo = TimeZoneInfo.Utc;
    private TimeSpan _startTime;
    private TimeSpan _endTime;
    private bool _timeConfigValid = true;

    private bool _debugEnabled = false;

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        const string configName = "vip_night";
        
        _api = PluginCapability.Get();
        if (_api == null)
        {
            ForceLogError("VipCoreApi not available. Plugin disabled.");
            return;
        }
        
        Config = _api.LoadConfig<VIP_NightVipConfig>(configName) ?? CreateConfig(configName);
        _debugEnabled = Config.Debug;

        var configPath = GetConfigPath(configName);
        
        try
        {
            string updatedJson = JsonSerializer.Serialize(Config, PrettyJsonOptions);

            if (File.Exists(configPath))
            {
                string existingJson = File.ReadAllText(configPath);

                if (!string.Equals(existingJson, updatedJson, StringComparison.Ordinal))
                {
                    File.WriteAllText(configPath, updatedJson);
                    ForceLogInfo("Configuration updated.");
                }
                else
                {
                    ForceLogInfo("Configuration is up to date.");
                }
            }
            else
            {
                File.WriteAllText(configPath, updatedJson);
                ForceLogInfo("Configuration file missing. A new one has been created.");
            }
        }
        catch (Exception ex)
        {
            ForceLogError($"Failed to auto-update config: {ex.Message}");
        }
        
        try
        {
            _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(Config.Timezone);
            LogInfo($"Loaded timezone: {Config.Timezone}");
        }
        catch (TimeZoneNotFoundException)
        {
            LogError($"Invalid timezone: {Config.Timezone}. Defaulting to UTC.");
            _timeZoneInfo = TimeZoneInfo.Utc;
        }
        catch (Exception ex)
        {
            LogError($"Unexpected timezone error: {ex.Message}. Defaulting to UTC.");
            _timeZoneInfo = TimeZoneInfo.Utc;
        }
        
        try
        {
            _startTime = TimeSpan.Parse(Config.PluginStartTime);
            _endTime = TimeSpan.Parse(Config.PluginEndTime);
            LogInfo($"Parsed time interval: StartTime={_startTime}, EndTime={_endTime}");
        }
        catch (FormatException)
        {
            LogError($"Invalid time format in config. Start: {Config.PluginStartTime}, End: {Config.PluginEndTime}. Disabling VIP time check.");
            _timeConfigValid = false;
        }
        
        if (Config.CheckTimer <= 0)
        {
            LogError($"Invalid CheckTimer value: {Config.CheckTimer}. Defaulting to 10 seconds.");
            Config.CheckTimer = 10;
        }
        else
        {
            LogInfo($"CheckTimer set to {Config.CheckTimer} seconds.");
        }
        
        ForceLogInfo("Configuration loaded:");
        ForceLogInfo($"  VIPGroup: {Config.VIPGroup}");
        ForceLogInfo($"  PluginStartTime: {Config.PluginStartTime}");
        ForceLogInfo($"  PluginEndTime: {Config.PluginEndTime}");
        ForceLogInfo($"  Timezone: {Config.Timezone}");
        ForceLogInfo($"  CheckTimer: {Config.CheckTimer}");
        ForceLogInfo($"  VipGrantedMessage: {Config.VipGrantedMessage}");
        ForceLogInfo($"  Tag: {Config.Tag}");
        ForceLogInfo($"  Debug: {Config.Debug}");

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (!IsPlayerValid(player)) return HookResult.Continue;

            GiveVIP(player);
            return HookResult.Continue;
        });

        AddTimer(Config.CheckTimer, CheckAndGiveVIP, TimerFlags.REPEAT);
        LogInfo("NightVIP timer started.");
    }

    private void CheckAndGiveVIP()
    {
        if (_api == null) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsPlayerValid(player)) continue;
            if (_api.IsClientVip(player)) continue;

            GiveVIP(player);
        }
    }

    private void GiveVIP(CCSPlayerController? player)
    {
        if (_api == null || !_timeConfigValid || !IsPlayerValid(player) || player == null)
            return;

        var currentTimeInTimeZone = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZoneInfo);
        var now = currentTimeInTimeZone.TimeOfDay;

        bool isVipTime = _startTime < _endTime
            ? now >= _startTime && now < _endTime
            : now >= _startTime || now < _endTime;

        if (!isVipTime || _api.IsClientVip(player))
            return;

        var remainingMinutes = CalculateRemainingVipTimeMinutes(_endTime, now);
        _api.GiveClientTemporaryVip(player, Config.VIPGroup, remainingMinutes);
        _api.PrintToChat(player, $" \x02{Config.Tag} \x01{Config.VipGrantedMessage}");

        LogInfo($"Gave temporary VIP ({Config.VIPGroup}) to {player?.PlayerName} for {remainingMinutes} minutes.");
    }

    private int CalculateRemainingVipTimeMinutes(TimeSpan endTime, TimeSpan currentTime)
    {
        double minutes = endTime > currentTime
            ? (endTime - currentTime).TotalMinutes
            : (TimeSpan.FromHours(24) - currentTime + endTime).TotalMinutes;

        return Math.Max(1, (int)Math.Ceiling(minutes));
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
    
    private string GetConfigPath(string configName)
    {
        var baseDir = Path.Combine(
            Server.GameDirectory,
            "csgo",
            "addons",
            "counterstrikesharp",
            "configs",
            "plugins",
            "VIPCore",
            "Modules"
        );

        Directory.CreateDirectory(baseDir);
        var fullPath = Path.Combine(baseDir, $"{configName}.json");
        
        ForceLogInfo($"Configuration path: {fullPath}");
        return fullPath;
    }

    private VIP_NightVipConfig CreateConfig(string configName)
    {
        var config = new VIP_NightVipConfig
        {
            VIPGroup = "vip",
            PluginStartTime = "20:00:00",
            PluginEndTime = "08:00:00",
            Timezone = "UTC",
            CheckTimer = 10,
            VipGrantedMessage = "You are receiving VIP because it's VIP Night time.",
            Tag = "[NightVIP]",
            Debug = false
        };

        try
        {
            var configPath = GetConfigPath(configName);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, PrettyJsonOptions));
            ForceLogInfo($"Default config created at path: {configPath}");
        }
        catch (Exception ex)
        {
            ForceLogError($"Failed to write configuration file for '{configName}': {ex.Message}");
        }

        return config;
    }

    private string LogTag => Config?.Tag ?? "[NightVIP]";

    private void LogInfo(string message)
    {
        if (!_debugEnabled)
            return;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{LogTag} {message}");
        Console.ResetColor();
    }

    private void LogError(string message)
    {
        if (!_debugEnabled)
            return;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"{LogTag} {message}");
        Console.ResetColor();
    }

    private void ForceLogInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{LogTag} {message}");
        Console.ResetColor();
    }

    private void ForceLogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"{LogTag} {message}");
        Console.ResetColor();
    }
}