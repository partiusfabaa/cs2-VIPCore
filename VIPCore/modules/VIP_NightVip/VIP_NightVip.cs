using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Entities;
using VipCoreApi;

namespace VIP_NightVip;

public class VIP_NightVipConfig
{
    public string VIPGroup { get; set; } = "vip";
    public string PluginStartTime { get; set; } = "20:00:00";
    public string PluginEndTime { get; set; } = "08:00:00";
}

public class VIP_NightVip : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Night VIP";
    public override string ModuleVersion => "v1.0";
    public override string ModuleDescription => "Gives VIP between a certain period of time.";
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    private VIP_NightVipConfig Config = null!;
    private readonly HashSet<ulong> _playersGivenVIP = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        Config = _api.LoadConfig<VIP_NightVipConfig>("vip_night");

        AddEventHandlers();
        GiveVIPToAllPlayers();
    }

    private void AddEventHandlers()
    {
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || !player.PlayerPawn.IsValid)
                return HookResult.Continue;

            GiveVIPIfNotAlready(player);

            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && player.IsValid)
            {
                RemoveVIPIfInGroup(player);
            }

            return HookResult.Continue;
        });

        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            GiveVIPToAllPlayers();
            return HookResult.Continue;
        });
    }

    private void GiveVIPToAllPlayers()
    {
        var currentTime = DateTime.Now.TimeOfDay;
        var startTime = TimeSpan.Parse(Config.PluginStartTime);
        var endTime = TimeSpan.Parse(Config.PluginEndTime);

        bool isVipTime;
        if (startTime < endTime)
            isVipTime = currentTime >= startTime && currentTime < endTime;
        else
            isVipTime = currentTime >= startTime || currentTime < endTime;

        if (!isVipTime) return;

        Server.NextFrame(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.PlayerPawn.IsValid))
            {
                GiveVIPIfNotAlready(player);
            }
        });
    }


    private void GiveVIPIfNotAlready(CCSPlayerController player)
    {
        if (_api == null || player == null || player.AuthorizedSteamID == null) return;

        var currentTime = DateTime.Now.TimeOfDay;
        var startTime = TimeSpan.Parse(Config.PluginStartTime);
        var endTime = TimeSpan.Parse(Config.PluginEndTime);

        bool isVipTime;
        if (startTime < endTime)
            isVipTime = currentTime >= startTime && currentTime < endTime;
        else
            isVipTime = currentTime >= startTime || currentTime < endTime;

        if (isVipTime && !_api.IsClientVip(player))
        {
            _api.GiveClientVip(player, Config.VIPGroup, -1);
            _playersGivenVIP.Add(player.AuthorizedSteamID.SteamId64);
            _api.PrintToChat(player, $" \x02[NightVIP] \x01You are receiving \x06VIP\x01 because it's \x07VIP Night \x01time.");
        }
    }

    private void RemoveVIPIfInGroup(CCSPlayerController player)
    {
        if (_api == null || !_api.IsClientVip(player) || player == null || player.AuthorizedSteamID == null) return;

        var playerGroup = _api.GetClientVipGroup(player);
        if (playerGroup == Config.VIPGroup && _playersGivenVIP.Contains(player.AuthorizedSteamID.SteamId64))
        {
            _api.RemoveClientVip(player);
            _playersGivenVIP.Remove(player.AuthorizedSteamID.SteamId64);
        }
    }

    private VIP_NightVipConfig CreateConfig(string configPath)
    {
        var config = new VIP_NightVipConfig
        {
            VIPGroup = "vip",
            PluginStartTime = "20:00:00",
            PluginEndTime = "08:00:00"
        };

        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return config;
    }

    public override void Unload(bool hotReload)
    {
    }
}