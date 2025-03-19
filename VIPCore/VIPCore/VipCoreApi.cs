using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Menu;
using CS2ScreenMenuAPI.Internal;
using FabiusTimer.Configs;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Models;
using VIPCore.Player;
using VIPCore.Services;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIPCore;

public class VipCoreApi(
    Plugin plugin,
    PlayersManager playersManager,
    IFeatureManager featureManager,
    DatabaseProvider dbProvider,
    DatabaseManager databaseManager,
    Config<VipConfig> vipConfig,
    Config<GroupsConfig> groupsConfig) : IVipCoreApi
{
    private Dictionary<ulong, PlayerCookie> _playersCookie = [];

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public string CoreConfigDirectory => Path.Combine(Application.RootDirectory, "configs/plugins/VIPCore/");
    public string ModulesConfigDirectory => Path.Combine(CoreConfigDirectory, "Modules/");
    public string DatabaseConnectionString => dbProvider.ConnectionString;
    public int ServerId => vipConfig.Value.ServerId;

    public IFeatureManager FeatureManager => featureManager;

    public bool IsPlayerVip(CCSPlayerController player)
    {
        return playersManager.TryGetPlayer(player, out var vipPlayer) && vipPlayer.IsVip;
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        if (!playersManager.TryGetPlayer(player, out var vipPlayer) ||
            vipPlayer.Group?.ContainsKey(feature) == false) return false;

        return true;
    }

    public T? GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        if (!playersManager.TryGetPlayer(player, out var vipPlayer)) return default;

        var data = vipPlayer.Data;
        if (data is null) return default;

        if (vipPlayer.Group is null) return default;

        if (vipPlayer.Group.TryGetValue(feature, out var featureData))
        {
            if (featureData is T value)
            {
                return value;
            }

            var custom = ((JsonElement)featureData).Deserialize<T>()!;
            vipPlayer.Group[feature] = custom;

            return (T?)custom;
        }

        return default;
    }

    public FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        if (!playersManager.TryGetPlayer(player, out var vipPlayer) || vipPlayer.Data == null)
            return FeatureState.NoAccess;

        var findFeature = featureManager.FindByName(feature);
        if (findFeature is null)
            return FeatureState.NoAccess;

        return vipPlayer.FeatureStates.TryGetValue(findFeature, out var state) ? state : FeatureState.NoAccess;
    }

    public void SetPlayerFeatureState(CCSPlayerController player, string feature, FeatureState newState)
    {
        if (!playersManager.TryGetPlayer(player, out var vipPlayer) || vipPlayer.Data == null)
            return;

        var findFeature = featureManager.FindByName(feature);
        if (findFeature is null)
            return;

        if (vipPlayer.FeatureStates.ContainsKey(findFeature))
        {
            vipPlayer.FeatureStates[findFeature] = newState;
        }
    }

    public string GetPlayerVipGroup(CCSPlayerController player)
    {
        if (!playersManager.TryGetPlayer(player, out var vipPlayer) || vipPlayer.Data == null)
            return string.Empty;

        return vipPlayer.Data.Group;
    }

    public string[] GetVipGroups()
    {
        return groupsConfig.Value.Keys.ToArray();
    }

    public void UpdatePlayerVip(CCSPlayerController player, string name = "", string group = "", int time = -1)
    {
        if (!IsPlayerVip(player))
        {
            plugin.Logger.LogError("Player {player} is not vip", player.PlayerName);
            return;
        }

        var accountId = player.AuthorizedSteamID?.AccountId;
        if (!accountId.HasValue) return;

        Task.Run(() => playersManager.UpdateUser(accountId.Value, name, group, time));
    }

    public void SetPlayerVip(CCSPlayerController player, string group, int time)
    {
    }

    public void GivePlayerVip(CCSPlayerController player, string group, int time)
    {
        throw new NotImplementedException();
    }

    public void GivePlayerTemporaryVip(CCSPlayerController player, string group, int time)
    {
        throw new NotImplementedException();
    }

    public void RemovePlayerVip(CCSPlayerController player)
    {
        throw new NotImplementedException();
    }

    public void SetPlayerCookie<T>(ulong steamId64, string key, T value)
    {
        if (!_playersCookie.TryGetValue(steamId64, out var cookie))
        {
            cookie = new PlayerCookie
            {
                SteamId64 = steamId64,
                Features = new Dictionary<string, object>()
            };

            _playersCookie[steamId64] = cookie;
        }

        cookie.Features[key] = value!;
    }

    public T GetPlayerCookie<T>(ulong steamId64, string key)
    {
        if (_playersCookie.TryGetValue(steamId64, out var cookie) &&
            cookie.Features.TryGetValue(key, out var featureValue))
        {
            try
            {
                switch (featureValue)
                {
                    case T typedValue:
                        return typedValue;
                    case JsonElement jsonElement:
                        return jsonElement.Deserialize<T>(_jsonSerializerOptions)!;
                }

                var jsonString = featureValue.ToString();
                if (jsonString != null)
                {
                    return JsonSerializer.Deserialize<T>(jsonString, _jsonSerializerOptions)!;
                }
            }
            catch (Exception e)
            {
                plugin.Logger.LogError($"Failed to deserialize cookie value: {key}, {e}");
            }
        }

        return default!;
    }


    public void LoadCookies()
    {
        var filePath = Path.Combine(CoreConfigDirectory, "vip_core_cookie.json");

        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "[]");
            return;
        }

        var fileContent = File.ReadAllText(filePath);
        var cookies = JsonSerializer.Deserialize<List<PlayerCookie>>(fileContent, _jsonSerializerOptions);

        if (cookies != null)
        {
            _playersCookie = cookies.ToDictionary(c => c.SteamId64, c => c);
        }
    }

    public void SaveCookies()
    {
        var filePath = Path.Combine(CoreConfigDirectory, "vip_core_cookie.json");

        var cookiesList = _playersCookie.Values.ToList();
        var jsonContent = JsonSerializer.Serialize(cookiesList, _jsonSerializerOptions);

        File.WriteAllText(filePath, jsonContent);
    }

    public void PrintToChat(CCSPlayerController player, string message)
    {
        playersManager.PrintToChat(player, message);
    }

    public void PrintToChatAll(string message)
    {
        plugin.PrintToChatAll(message);
    }

    public string GetTranslatedText(CCSPlayerController player, string name, params object[] args)
    {
        return plugin.Localizer.ForPlayer(player, name, args);
    }

    public string GetTranslatedText(string name, params object[] args)
    {
        return plugin.Localizer[name, args];
    }

    public bool IsPistolRound()
    {
        var gamerules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        var halftime = ConVar.Find("mp_halftime")!.GetPrimitiveValue<bool>();
        var maxrounds = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

        if (gamerules == null) return false;
        return gamerules.TotalRoundsPlayed == 0 ||
               (halftime && maxrounds / 2 == gamerules.TotalRoundsPlayed) ||
               gamerules.GameRestart;
    }

    public T LoadConfig<T>(string name, string path)
    {
        var configFilePath = Path.Combine(path, $"{name}.json");

        if (!File.Exists(configFilePath))
        {
            var defaultConfig = Activator.CreateInstance<T>();

            File.WriteAllText(configFilePath,
                JsonSerializer.Serialize(defaultConfig, _jsonSerializerOptions));
            return defaultConfig;
        }

        var configJson = File.ReadAllText(configFilePath);
        var config = JsonSerializer.Deserialize<T>(configJson, _jsonSerializerOptions);

        if (config == null)
            throw new FileNotFoundException($"File {name}.json not found or cannot be deserialized");

        return config;
    }

    public T LoadConfig<T>(string name)
    {
        return LoadConfig<T>(name, ModulesConfigDirectory);
    }

    public IMenu CreateMenu(string title)
    {
        return vipConfig.Value.MenuType switch
        {
            "center" => new CenterHtmlMenu(title, plugin),
            "chat" => new ChatMenu(title),
            _ => new CenterHtmlMenu(title, plugin)
        };
    }

    public ScreenMenu CreateScreenMenu(string title)
    {
        return new ScreenMenu(title, plugin);
    }

    public event OnPlayerAuthorizedDelegate? OnPlayerAuthorized;
    public event OnPlayerDisconnectDelegate? OnPlayerDisconnect;
    public event OnPlayerSpawnDelegate? OnPlayerSpawn;
    public event OnPlayerUseFeatureDelegate? OnPlayerUseFeature;
    public event Action? OnCoreReady;

    public void InvokeOnPlayerAuthorized(CCSPlayerController player)
    {
        var group = GetPlayerVipGroup(player);
        if (string.IsNullOrEmpty(group)) return;

        OnPlayerAuthorized?.Invoke(player, group);
    }

    public void InvokeOnPlayerDisconnect(CCSPlayerController player, bool vip)
    {
        OnPlayerDisconnect?.Invoke(player, vip); 
    }

    public void InvokeOnPlayerSpawn(CCSPlayerController player, bool isVip)
    {
        OnPlayerSpawn?.Invoke(player, isVip);
    }

    public void InvokeOnPlayerUseFeature(PlayerUseFeatureEventArgs args)
    {
        OnPlayerUseFeature?.Invoke(args);
    }

    public void InvokeOnCoreReady()
    {
        OnCoreReady?.Invoke();
    }
}