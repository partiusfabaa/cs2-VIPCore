using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using VIPCore.Models;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIPCore;

public class VipCoreApi : IVipCoreApi
{
    private readonly VipCore _vipCore;
    private Dictionary<ulong, PlayerCookie> _playersCookie = [];

    public event Action<CCSPlayerController>? OnPlayerSpawn;
    public event Action<CCSPlayerController, string>? PlayerLoaded;
    public event Action<CCSPlayerController, string>? PlayerRemoved;
    public event Action? OnCoreReady;
    public event Func<CCSPlayerController, string, FeatureState, FeatureType, HookResult?>? OnPlayerUseFeature;

    public string GetTranslatedText(string name, params object[] args) => _vipCore.Localizer[name, args];

    public string CoreConfigDirectory { get; }
    public string ModulesConfigDirectory => Path.Combine(CoreConfigDirectory, "Modules/");
    public string GetDatabaseConnectionString => _vipCore.DbConnectionString;
    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { 
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public VipCoreApi(VipCore vipCore)
    {
        _vipCore = vipCore;
        CoreConfigDirectory = Path.Combine(Application.RootDirectory, "configs/plugins/VIPCore/");
    }

    public void CoreReady()
    {
        OnCoreReady?.Invoke();
    }

    public int GetServerId()
    {
        return _vipCore.CoreConfig.ServerId;
    }

    public void RegisterFeature(VipFeatureBase vipFeatureBase, FeatureType featureType = FeatureType.Toggle)
    {
        if (string.IsNullOrEmpty(vipFeatureBase.Feature)) return;

        foreach (var config in _vipCore.Config.Groups)
            config.Value.Values.TryAdd(vipFeatureBase.Feature, string.Empty);

        _vipCore.FeatureRegistry.Register(vipFeatureBase.Feature, new Feature
        {
            FeatureType = featureType,
            OnSelectItem = vipFeatureBase.OnSelectItem
        });

        foreach (var vip in _vipCore.Players.Records)
            _vipCore.ApplyFeatureStates(vip, onlyMissing: true);

        _vipCore.PrintLogInfo("Feature '{feature}' registered successfully", vipFeatureBase.Feature);
    }

    public void UnRegisterFeature(VipFeatureBase vipFeatureBase)
    {
        if (string.IsNullOrEmpty(vipFeatureBase.Feature)) return;

        foreach (var config in _vipCore.Config.Groups)
            config.Value.Values.Remove(vipFeatureBase.Feature);

        _vipCore.FeatureRegistry.Unregister(vipFeatureBase.Feature);

        _vipCore.PrintLogInfo(
            "Feature '{feature}' unregistered successfully", vipFeatureBase.Feature);
    }

    public IEnumerable<(string feature, object value)> GetAllRegisteredFeatures()
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            foreach (var (key, value) in config.Value.Values)
            {
                yield return (key, value);
            }
        }
    }

    public FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Players.TryGetRecord(player, out var vip))
            throw new InvalidOperationException("player not found");

        return vip.IsExpired
            ? FeatureState.NoAccess
            : vip.FeatureStates.GetValueOrDefault(feature, FeatureState.NoAccess);
    }

    public void SetPlayerFeatureState(CCSPlayerController player, string feature, FeatureState newState)
    {
        if (!_vipCore.Players.TryGetRecord(player, out var vip))
            throw new InvalidOperationException("player not found");

        if (!vip.FeatureStates.ContainsKey(feature))
            throw new InvalidOperationException("feature not found");

        vip.FeatureStates[feature] = newState;
    }

    public void DisableAllFeatures()
    {
        _vipCore.FeatureRegistry.DisableAll();
    }

    public void EnableAllFeatures()
    {
        _vipCore.FeatureRegistry.EnableAll();
    }

    public bool IsClientVip(CCSPlayerController player)
    {
        return _vipCore.Players.IsVip(player);
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        if (_vipCore.FeatureRegistry.IsForcedDisabled(feature)) return false;

        if (!_vipCore.Players.TryGet(player, out var vip)) return false;

        if (!_vipCore.Config.Groups.TryGetValue(vip.Group, out var vipGroup)) return false;

        return vipGroup.Values.TryGetValue(feature, out var value) && !string.IsNullOrEmpty(value.ToString());
    }

    public string GetClientVipGroup(CCSPlayerController player)
    {
        if (!_vipCore.Players.TryGetRecord(player, out var vip))
            throw new InvalidOperationException("player not found");

        return vip.Group;
    }

    public string[] GetVipGroups()
    {
        var groups = _vipCore.Config.Groups;
        if (groups.Count == 0)
            return [];

        return groups.Keys.ToArray();
    }

    public void UpdateClientVip(CCSPlayerController player, string name = "", string group = "", int time = -1)
    {
        var steamId =
            new SteamID(player.AuthorizedSteamID == null ? player.SteamID : player.AuthorizedSteamID.SteamId64);

        Task.Run(() => _vipCore.Database.UpdateUserVip(steamId.AccountId, name, group, time));

        if (_vipCore.Players.TryGetRecord(player, out var vip))
            _vipCore.UpdateOnlineVip(player, vip, name, group, time);
    }

    public void SetClientVip(CCSPlayerController player, string group, int time)
    {
        var authSteamId = player.AuthorizedSteamID;
        if (authSteamId == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var user = _vipCore.CreateNewUser(authSteamId.AccountId, player.PlayerName, group, time);
        _vipCore.AddVip(player, user);
    }

    public void GiveClientTemporaryVip(CCSPlayerController player, string group, int time)
    {
        GiveClientVip(player, group, time, true);
    }

    public void GiveClientVip(CCSPlayerController player, string group, int time)
    {
        GiveClientVip(player, group, time, false);
    }

    private void GiveClientVip(CCSPlayerController player, string group, int time, bool isTemporary)
    {
        if (IsClientVip(player))
            throw new Exception($"Player {player.PlayerName} already has a VIP");

        var authSteamId = player.AuthorizedSteamID;
        if (authSteamId == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var user = _vipCore.CreateNewUser(authSteamId.AccountId, player.PlayerName, group, time);
        _vipCore.Players.Set(player, VipPlayer.FromUser(user, authSteamId.SteamId64, isTemporary));

        if (!isTemporary)
        {
            Task.Run(() => _vipCore.Database.AddUserToDb(user));
        }
    }

    public void RemoveClientVip(CCSPlayerController player)
    {
        if (_vipCore.Players.TryGetRecord(player, out var vip) && _vipCore.Players.Remove(player))
        {
            if (vip.IsTemporary) return;
        }
        else
        {
            _vipCore.PrintLogWarning("RemoveClientVip: player {name} is not loaded as VIP, removing from database only",
                player.PlayerName);
        }

        var accountId = new SteamID(player.SteamID).AccountId;
        Task.Run(() => _vipCore.Database.RemoveUserFromDb(accountId));
    }

    public void PrintToChat(CCSPlayerController player, string message)
    {
        _vipCore.PrintToChat(player, message);
    }

    public void PrintToChatAll(string message)
    {
        _vipCore.PrintToChatAll(message);
    }

    public bool IsPistolRound()
    {
        var gamerules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        var halftime = ConVar.Find("mp_halftime")!.GetPrimitiveValue<bool>();
        var maxrounds = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

        if (gamerules == null) return false;
        return gamerules.TotalRoundsPlayed == 0 || (halftime && maxrounds / 2 == gamerules.TotalRoundsPlayed) ||
               gamerules.GameRestart;
    }

    // public void Startup()
    // {
    //     OnCoreReady?.Invoke();
    // }

    public void PlayerSpawn(CCSPlayerController player)
    {
        OnPlayerSpawn?.Invoke(player);
    }

    public void OnPlayerLoaded(CCSPlayerController player, string group)
    {
        PlayerLoaded?.Invoke(player, group);
    }

    public void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        PlayerRemoved?.Invoke(player, group);
    }

    public HookResult? PlayerUseFeature(CCSPlayerController player, string feature, FeatureState state,
        FeatureType type)
    {
        return OnPlayerUseFeature?.Invoke(player, feature, state, type);
    }

    public T GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Players.TryGetRecord(player, out var vip))
            throw new InvalidOperationException("User not found.");

        if (_vipCore.Config.Groups.TryGetValue(vip.Group, out var vipGroup))
        {
            if (vipGroup.Values.TryGetValue(feature, out var value))
            {
                _vipCore.PrintLogInfo(
                    "Checking feature: {feature} - {value}", feature, value);
                try
                {
                    return ((JsonElement)value).Deserialize<T>(_jsonSerializerOptions)!;
                }
                catch (JsonException)
                {
                    _vipCore.PrintLogError(
                        "Failed to deserialize feature '{feature}' value: {value}", feature, value);
                    throw new JsonException($"Failed to deserialize feature '{feature}' value: {value}");
                }
            }
        }

        _vipCore.PrintLogError("Feature not found, returning default value: {empty}", "Empty");
        throw new KeyNotFoundException($"Feature '{feature}' not found.");
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
        return _vipCore.CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(title, _vipCore) : new ChatMenu(title);
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
                _vipCore.PrintLogError($"Failed to deserialize cookie value: {key}, {e}");
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
}