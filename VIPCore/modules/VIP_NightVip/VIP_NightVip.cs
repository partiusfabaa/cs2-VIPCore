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
    public int CheckTimer { get; set;} = 10;
}

[MinimumApiVersion(240)]
public class VIP_NightVip : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Night VIP";
    public override string ModuleVersion => "v1.0";
    public override string ModuleDescription => "Gives VIP between a certain period of time.";
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    private VIP_NightVipConfig Config = null!;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        Config = _api.LoadConfig<VIP_NightVipConfig>("vip_night");

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || !player.PlayerPawn.IsValid || !player.PawnIsAlive)
                return HookResult.Continue;

            GiveVIP(player);

            return HookResult.Continue;
        });

        AddTimer(Config.CheckTimer, ()=>
        {
            CheckAndGiveVIP();
        }, TimerFlags.REPEAT );

    }
    private void CheckAndGiveVIP()
    {
        if (_api == null) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || !player.PlayerPawn.IsValid || !player.PawnIsAlive)
                continue;

            if (!_api.IsClientVip(player))
                GiveVIP(player);
        }
    }

    private void GiveVIP(CCSPlayerController? player)
    {
        if (_api == null || player == null) return;

        var currentTime = DateTime.UtcNow;
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(Config.Timezone);
        var currentTimeInTimeZone = TimeZoneInfo.ConvertTimeFromUtc(currentTime, timeZoneInfo);

        var startTime = TimeSpan.Parse(Config.PluginStartTime);
        var endTime = TimeSpan.Parse(Config.PluginEndTime);

        bool isVipTime;
        if (startTime < endTime)
            isVipTime = currentTimeInTimeZone.TimeOfDay >= startTime && currentTimeInTimeZone.TimeOfDay < endTime;
        else
            isVipTime = currentTimeInTimeZone.TimeOfDay >= startTime || currentTimeInTimeZone.TimeOfDay < endTime;

        if (isVipTime && !_api.IsClientVip(player) && player.IsValid && !player.IsBot && !player.IsHLTV && player != null && player.PawnIsAlive)
        {
            var remainingTime = (endTime > currentTimeInTimeZone.TimeOfDay) ? (endTime - currentTimeInTimeZone.TimeOfDay).TotalMinutes : (TimeSpan.FromHours(24) - currentTimeInTimeZone.TimeOfDay + endTime).TotalMinutes;

            _api.GiveClientTemporaryVip(player, Config.VIPGroup, (int)remainingTime);
            _api.PrintToChat(player, $" \x02[NightVIP] \x01You are receiving \x06VIP\x01 because it's \x07VIP Night \x01time.");
        }
    }

    private VIP_NightVipConfig CreateConfig(string configPath)
    {
        var config = new VIP_NightVipConfig
        {
            VIPGroup = "vip",
            PluginStartTime = "20:00:00",
            PluginEndTime = "08:00:00",
            Timezone = "UTC",
            CheckTimer = 10
        };

        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return config;
    }
}