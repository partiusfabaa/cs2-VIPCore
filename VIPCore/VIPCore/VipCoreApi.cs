using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;
using VIPCore.Services;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;

namespace VIPCore;

public class VipCoreApi : IVipCoreApi
{
    private readonly Plugin _plugin;
    private readonly PlayersManager _players;
    private readonly FeatureManager _featureManager;
    private readonly CookieService _cookies;
    private readonly DatabaseProvider _dbProvider;
    private readonly Config<VipConfig> _coreConfig;
    private readonly Config<GroupsConfig> _groupsConfig;

    public VipCoreApi(
        Plugin plugin,
        PlayersManager players,
        FeatureManager featureManager,
        CookieService cookies,
        DatabaseProvider dbProvider,
        Config<VipConfig> coreConfig,
        Config<GroupsConfig> groupsConfig)
    {
        _plugin = plugin;
        _players = players;
        _featureManager = featureManager;
        _cookies = cookies;
        _dbProvider = dbProvider;
        _coreConfig = coreConfig;
        _groupsConfig = groupsConfig;

        players.PlayerLoaded += player =>
            Invoke(OnPlayerAuthorized, h => h(player.Controller, player.Group));
        players.PlayerRemoved += (player, group) =>
            Invoke(OnPlayerRemoved, h => h(player.Controller, group));
        players.PlayerDisconnect += player =>
            Invoke(OnPlayerDisconnect, h => h(player.Controller, player.IsVip));
    }

    public string CoreConfigDirectory => ConfigSystem.CoreConfigDirectory;
    public string ModulesConfigDirectory => Path.Combine(CoreConfigDirectory, "Modules/");
    public string DatabaseConnectionString => _dbProvider.ConnectionString;
    public int ServerId => _coreConfig.Value.ServerId;

    public IFeatureManager FeatureManager => _featureManager;

    public FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        if (!IsPlayerVip(player, out var vipPlayer)) return FeatureState.NoAccess;

        return vipPlayer.FeatureStates.GetValueOrDefault(feature, FeatureState.NoAccess);
    }

    public void SetPlayerFeatureState(CCSPlayerController player, string feature, FeatureState newState)
    {
        if (!_players.TryGetPlayer(player, out var vipPlayer) || !vipPlayer.FeatureStates.ContainsKey(feature))
            return;

        if (newState is not FeatureState.NoAccess && !vipPlayer.HasFeature(feature))
            return;

        vipPlayer.FeatureStates[feature] = newState;
    }

    public bool IsPlayerVip(CCSPlayerController player) => IsPlayerVip(player, out _);

    private bool IsPlayerVip(CCSPlayerController player, out VipPlayer vipPlayer)
    {
        return _players.TryGetPlayer(player, out vipPlayer!) && vipPlayer.IsVip && _plugin.EnableConVar.Value;
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        return IsPlayerVip(player, out var vipPlayer) && vipPlayer.HasFeature(feature);
    }

    public T? GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        if (!_players.TryGetPlayer(player, out var vipPlayer) || !vipPlayer.IsVip ||
            !vipPlayer.Features.TryGetValue(feature, out var raw))
            return default;

        if (raw is T typed) return typed;

        var key = (feature, typeof(T));
        if (vipPlayer.FeatureValues.TryGetValue(key, out var cached)) return (T?)cached;

        try
        {
            var value = raw is JsonElement element
                ? element.Deserialize<T>(ConfigSystem.ConfigJsonOptions)
                : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(raw), ConfigSystem.ConfigJsonOptions);

            vipPlayer.FeatureValues[key] = value;
            return value;
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError("Failed to deserialize feature '{feature}' value '{value}' as {type}: {error}",
                feature, raw, typeof(T).Name, e.Message);
            return default;
        }
    }

    public string GetPlayerVipGroup(CCSPlayerController player)
    {
        return _players.TryGetPlayer(player, out var vipPlayer) && vipPlayer.IsVip ? vipPlayer.Group : string.Empty;
    }

    public string[] GetVipGroups() => _groupsConfig.Value.Keys.ToArray();

    public void UpdatePlayerVip(CCSPlayerController player, string name = "", string group = "", int time = -1)
    {
        if (!string.IsNullOrEmpty(group) && !GroupExists(group)) return;

        _players.UpdateVip(GetAccountId(player), player,
            string.IsNullOrEmpty(name) ? null : name,
            string.IsNullOrEmpty(group) ? null : group,
            time < 0 ? null : _plugin.ExpiresIn(time));
    }

    public void SetPlayerVip(CCSPlayerController player, string group, int time)
    {
        if (!GroupExists(group)) return;

        _players.GiveVip(player, CreateVipData(player, group, time));
    }

    public void GivePlayerVip(CCSPlayerController player, string group, int time)
    {
        GivePlayerVip(player, group, time, false);
    }

    public void GivePlayerTemporaryVip(CCSPlayerController player, string group, int time)
    {
        GivePlayerVip(player, group, time, true);
    }

    private void GivePlayerVip(CCSPlayerController player, string group, int time, bool isTemporary)
    {
        if (IsPlayerVip(player))
            throw new InvalidOperationException($"Player {player.PlayerName} already has a VIP");

        if (!GroupExists(group)) return;

        _players.GiveVip(player, CreateVipData(player, group, time), isTemporary);
    }

    public void RemovePlayerVip(CCSPlayerController player)
    {
        if (!_players.TryGetPlayer(player, out var vipPlayer) || vipPlayer.Data is null)
            _plugin.LogWarning("RemovePlayerVip: player {name} is not loaded as VIP, removing from database only",
                player.PlayerName);

        _players.RemoveVip(GetAccountId(player), player);
    }

    public void SetPlayerCookie<T>(ulong steamId64, string key, T value) => _cookies.Set(steamId64, key, value);

    public T GetPlayerCookie<T>(ulong steamId64, string key) => _cookies.Get<T>(steamId64, key);

    public void PrintToChat(CCSPlayerController player, string message) => _plugin.PrintToChat(player, message);

    public void PrintToChatAll(string message) => _plugin.PrintToChatAll(message);

    public string GetTranslatedText(CCSPlayerController player, string name, params object[] args) =>
        _plugin.Translate(player, name, args);

    public string GetTranslatedText(string name, params object[] args) => _plugin.Localizer[name, args];

    public bool IsPistolRound()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()
            ?.GameRules;
        if (gameRules == null) return false;

        var halftime = ConVar.Find("mp_halftime")!.GetPrimitiveValue<bool>();
        var maxRounds = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

        return gameRules.TotalRoundsPlayed == 0 ||
               (halftime && maxRounds / 2 == gameRules.TotalRoundsPlayed) ||
               gameRules.GameRestart;
    }

    public T LoadConfig<T>(string name, string path)
    {
        var configFilePath = Path.Combine(path, $"{name}.json");

        if (!File.Exists(configFilePath))
        {
            var defaultConfig = Activator.CreateInstance<T>();
            File.WriteAllText(configFilePath, JsonSerializer.Serialize(defaultConfig, ConfigSystem.ConfigJsonOptions));
            return defaultConfig;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(configFilePath), ConfigSystem.ConfigJsonOptions) ??
               throw new JsonException($"File {name}.json cannot be deserialized");
    }

    public T LoadConfig<T>(string name) => LoadConfig<T>(name, ModulesConfigDirectory);

    public IMenu CreateMenu(string title)
    {
        return _coreConfig.Value.MenuType.ToLowerInvariant() switch
        {
            "chat" => new ChatMenu(title),
            "console" => new ConsoleMenu(title),
            _ => new CenterHtmlMenu(title, _plugin)
        };
    }

    public event OnPlayerAuthorizedDelegate? OnPlayerAuthorized;
    public event OnPlayerRemovedDelegate? OnPlayerRemoved;
    public event OnPlayerDisconnectDelegate? OnPlayerDisconnect;
    public event OnPlayerSpawnDelegate? OnPlayerSpawn;
    public event OnPlayerUseFeatureDelegate? OnPlayerUseFeature;
    public event Action? OnCoreReady;

    public void InvokeOnPlayerSpawn(CCSPlayerController player, bool isVip) =>
        Invoke(OnPlayerSpawn, h => h(player, isVip));

    public void InvokeOnPlayerUseFeature(PlayerUseFeatureEventArgs args) =>
        Invoke(OnPlayerUseFeature, h => h(args));

    public void InvokeOnCoreReady() => Invoke(OnCoreReady, h => h());

    private void Invoke<T>(T? handlers, Action<T> invoke) where T : Delegate
    {
        if (handlers is null) return;

        foreach (var handler in handlers.GetInvocationList().Cast<T>())
        {
            try
            {
                invoke(handler);
            }
            catch (Exception e)
            {
                _plugin.Logger.LogError("Exception in {handler}: {e}",
                    $"{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}", e);
            }
        }
    }

    private bool GroupExists(string group)
    {
        if (_groupsConfig.Value.ContainsKey(group)) return true;

        _plugin.Logger.LogError("VIP group '{group}' was not found", group);
        return false;
    }

    private VipData CreateVipData(CCSPlayerController player, string group, int time) =>
        _players.CreateVipData(GetAccountId(player), player.PlayerName, group, _plugin.ExpiresIn(time));

    private static int GetAccountId(CCSPlayerController player) =>
        (player.AuthorizedSteamID ?? new SteamID(player.SteamID)).AccountId;
}
