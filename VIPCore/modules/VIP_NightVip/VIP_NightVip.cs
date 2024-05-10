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
    public string VIPGroup { get; set; } = "VIP";
    public string PluginStartTime { get; set; } = "20:00:00";
    public string PluginEndTime { get; set; } = "06:00:00";
}

public class VIP_NightVip : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Night VIP";
    public override string ModuleVersion => "v1.0";
    public override string ModuleDescription => "Gives VIP between a certain period of time.";
    private IVipCoreApi? _api;
    private static readonly string ConfigFileName = "vip_night.json";
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public VIP_NightVipConfig _config { get; set; } = null!;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;
        
        _config = LoadConfig();

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
        Server.NextFrame(() =>
		{
            foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.PlayerPawn.IsValid))
            {
                GiveVIPIfNotAlready(player);
            }
        });
    }

    private void GiveVIPIfNotAlready(CCSPlayerController player)
    {
        if (_api == null) return;

        var currentTime = DateTime.Parse(DateTime.Now.ToString("HH:mm:ss"));
        var startTime = DateTime.Parse(_config.PluginStartTime);
        var endTime = DateTime.Parse(_config.PluginEndTime);

        if ((currentTime >= startTime || currentTime < endTime) && !_api.IsClientVip(player))
        {
            _api.GiveClientVip(player, _config.VIPGroup, -1);
            _api.PrintToChat(player, $" \x02[NightVIP] \x01You are receiving \x06VIP\x01 because it's \x07VIP Night \x01time.");
        }
    }

    private void RemoveVIPIfInGroup(CCSPlayerController player)
    {
        if (_api == null || !_api.IsClientVip(player)) return;

        var playerGroup = _api.GetClientVipGroup(player);
        if (playerGroup == _config.VIPGroup)
            _api.RemoveClientVip(player);
    }

    private VIP_NightVipConfig LoadConfig()
    {
        var configPath = Path.Combine(_api.ModulesConfigDirectory, ConfigFileName);

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        return JsonSerializer.Deserialize<VIP_NightVipConfig>(File.ReadAllText(configPath))!;
    }

    private VIP_NightVipConfig CreateConfig(string configPath)
    {
        var config = new VIP_NightVipConfig
        {
            VIPGroup = "VIP",
            PluginStartTime = "20:00:00",
            PluginEndTime = "06:00:00"
        };

        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return config;
    }

    public override void Unload(bool hotReload)
    {
    }
}