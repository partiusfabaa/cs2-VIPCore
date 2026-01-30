using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Random;

public class VIP_Random : BasePlugin
{
    public override string ModuleName => "[VIP] Random";
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleDescription => "After x rounds from the map start, select random VIP.";
    public override string ModuleVersion => "1.0.1";

    private IVipCoreApi? _vipApi;
    private Config _config = null!;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    private CCSPlayerController? RandomVIP;
    private int _currentRound;
    private bool _vipAssigned; // Flag to track if VIP has been assigned

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        base.OnAllPluginsLoaded(hotReload);
        _vipApi = PluginCapability.Get();
        if (_vipApi == null)
        {
            Server.PrintToChatAll("VIP API not found.");
            return;
        }
        _config = LoadConfig();
    }

    private Config LoadConfig()
    {
        var configPath = Path.Combine(_vipApi!.ModulesConfigDirectory, "vip_random.json");
        if (!File.Exists(configPath))
        {
            return CreateConfig(configPath);
        }
        var configJson = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<Config>(configJson) ?? CreateConfig(configPath);
    }

    private Config CreateConfig(string configPath)
    {
        var defaultConfig = new Config
        {
            RandomVIPGroup = "vip_group_name",
            RandomVIPRound = 4,
            RandomVIPMinPlayers = 1
        };
        File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
        return defaultConfig;
    }

    private void SaveConfig()
    {
        var configPath = Path.Combine(_vipApi!.ModulesConfigDirectory, "vip_random.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        int roundInterval = _config.RandomVIPRound;
        int minPlayers = _config.RandomVIPMinPlayers;
        _currentRound++;
        if (_currentRound >= roundInterval)
        {
            if (Utilities.GetPlayers().Count >= minPlayers)
            {
                // Only assign VIP if one hasn't been assigned yet
                if (!_vipAssigned)
                {
                    GetRandomVIP();
                    _currentRound = 0; // Reset the round counter after selecting a VIP
                    _vipAssigned = true; // Set the flag to indicate a VIP has been assigned
                }
            }
            else
            {
                Server.PrintToChatAll(Localizer["prefix"] + Localizer["vip.not.enough"]);
            }
        }
        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == RandomVIP)
        {
            _vipApi?.RemoveClientVip(RandomVIP!);
            _vipAssigned = false; // Reset the VIP assignment flag if the VIP disconnects
        }
        return HookResult.Continue;
    }

    public void OnMapStart(string mapName)
    {
        _currentRound = 0;
        _vipAssigned = false; // Reset the flag for the new map
    }

    public void GetRandomVIP()
    {
        var player = GetRandomPlayer();
        if (player != null && player.IsValid)
        {
            // Check if the player is already a VIP
            if (_vipApi != null && !_vipApi.IsClientVip(player))
            {
                string localizedMessage = Localizer["vip.selected"];
                string message = localizedMessage.Replace("{playerName}", player.PlayerName);
                Server.PrintToChatAll(Localizer["prefix"] + message);
                player.PrintToChat(Localizer["prefix"] + Localizer["vip.player.message"]);
                _vipApi.GiveClientVip(player, _config.RandomVIPGroup, 1800); // Adding a duration parameter
                RandomVIP = player;
                _vipAssigned = true; // Set the flag to indicate a VIP has been assigned
            }
            else
            {
                Server.PrintToChatAll(Localizer["prefix"] + Localizer["vip.already.vip"]);
            }
        }
    }

    private CCSPlayerController? GetRandomPlayer()
    {
        var players = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && p.PlayerPawn != null && p.PlayerPawn.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();
        if (players.Count == 0) return null;
        var rand = new Random();
        var randomIndex = rand.Next(players.Count);
        return players[randomIndex];
    }
}

public class Config
{
    public required string RandomVIPGroup { get; init; }
    public int RandomVIPRound { get; init; }
    public int RandomVIPMinPlayers { get; init; }
}