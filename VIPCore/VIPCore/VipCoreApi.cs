using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;
using VipCoreApi;

namespace VIPCore;

public class VipCoreApi : IVipCoreApi
{
    private readonly VipCore _vipCore;

    //public event Action? OnCoreReady;
    public event Action<CCSPlayerController>? OnPlayerSpawn;
    public event Action<CCSPlayerController, string>? PlayerLoaded;
    public event Action<CCSPlayerController, string>? PlayerRemoved;
    public event Action? OnCoreReady;

    public string GetTranslatedText(string name, params object[] args) => _vipCore.Localizer[name, args];

    public string CoreConfigDirectory { get; }
    public string ModulesConfigDirectory => Path.Combine(CoreConfigDirectory, "Modules/");
    public string GetDatabaseConnectionString => _vipCore.DbConnectionString;

    public VipCoreApi(VipCore vipCore, string directory)
    {
        _vipCore = vipCore;
        CoreConfigDirectory = new DirectoryInfo(directory).Parent?.Parent?.FullName + "/configs/plugins/VIPCore/";
    }

    public void CoreReady()
    {
        OnCoreReady?.Invoke();
    }

    public IVipCoreApi.FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        return user.FeatureState.GetValueOrDefault(feature, IVipCoreApi.FeatureState.NoAccess);
    }

    public void RegisterFeature(VipFeatureBase vipFeatureBase,
        IVipCoreApi.FeatureType featureType = IVipCoreApi.FeatureType.Toggle)//, Action<CCSPlayerController, FeatureState>? selectItem = null)
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            if (vipFeatureBase.Feature == null || string.IsNullOrEmpty(vipFeatureBase.Feature)) continue;
            config.Value.Values.TryAdd(vipFeatureBase.Feature, string.Empty);
            foreach (var keyValuePair in config.Value.Values)
            {
                if (string.IsNullOrEmpty(keyValuePair.Value.ToString())) continue;

                _vipCore.Features.TryAdd(vipFeatureBase.Feature, new Feature
                {
                    FeatureType = featureType,
                    OnSelectItem = vipFeatureBase.OnSelectItem
                });
            }
        }

        _vipCore.PrintLogInfo("Feature '{feature}' registered successfully", vipFeatureBase.Feature);
    }

    public void UnRegisterFeature(VipFeatureBase vipFeatureBase)
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            if (vipFeatureBase.Feature != null)
            {
                config.Value.Values.Remove(vipFeatureBase.Feature);
                _vipCore.Features.Remove(vipFeatureBase.Feature, out _);
            }
        }

        _vipCore.PrintLogInfo(
            "Feature '{feature}' unregistered successfully", vipFeatureBase.Feature);
    }

    public IEnumerable<(string feature, object value)> GetAllRegisteredFeatures()
    {
        foreach (var config in _vipCore.Config.Groups)
        {
            foreach (var keyValuePair in config.Value.Values)
            {
                yield return (keyValuePair.Key, keyValuePair.Value);
            }
        }
    }

    public bool IsClientVip(CCSPlayerController player)
    {
        return _vipCore.IsUserActiveVip(player);
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user)) return false;

        if (user is null or { group: null }) return false;

        if (!_vipCore.Config.Groups.TryGetValue(user.group, out var vipGroup)) return false;

        foreach (var vipGroupValue in vipGroup.Values.Where(vipGroupValue => vipGroupValue.Key == feature))
        {
            return !string.IsNullOrEmpty(vipGroupValue.Value.ToString());
        }

        return false;
    }

    public string GetClientVipGroup(CCSPlayerController player)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        return user.group;
    }

    public void UpdateClientVip(CCSPlayerController player, string name = "", string group = "", int time = -1)
    {
        var steamId =
            new SteamID(player.AuthorizedSteamID == null ? player.SteamID : player.AuthorizedSteamID.SteamId64);

        Task.Run(() => _vipCore.Database.UpdateUserVip(steamId.AccountId, name, group, time));

        var user = _vipCore.CreateNewUser(steamId.AccountId, name, group, time);
        if (!_vipCore.Users.TryAdd(steamId.SteamId64, user))
        {
            _vipCore.Users[steamId.SteamId64] = user;
        }
            
        OnPlayerLoaded(player, group);
    }

    public void SetClientVip(CCSPlayerController player, string group, int time)
    {
        var name = player.PlayerName;

        var authSteamId = player.AuthorizedSteamID;
        if (authSteamId == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var accountId = authSteamId.AccountId;
        var steamId64 = authSteamId.SteamId64;

        OnPlayerLoaded(player, group);
        Task.Run(() => SetClientVipAsync(name, accountId, group, time, steamId64));
    }

    private async Task SetClientVipAsync(string name, int accountId, string group, int time,
        ulong steamId64)
    {
        try
        {
            var user = _vipCore.CreateNewUser(accountId, name, group, time);
            await _vipCore.Database.UpdateUserInDb(user);
            
            if (_vipCore.Users.ContainsKey(steamId64))
            {
                _vipCore.Users[steamId64] = user;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void GiveClientVip(CCSPlayerController player, string group, int time)
    {
        var name = player.PlayerName;

        var authSteamId = player.AuthorizedSteamID;
        if (authSteamId == null)
        {
            _vipCore.Logger.LogError($"AuthorizedSteamId is null");
            return;
        }

        var accountId = authSteamId.AccountId;
        var steamId64 = authSteamId.SteamId64;

        OnPlayerLoaded(player, group);
        Task.Run(() => GiveClientVipAsync(name, accountId, group, time, steamId64));
    }

    public void RemoveClientVip(CCSPlayerController player)
    {
        var steamId = new SteamID(player.SteamID);

        if (!_vipCore.Users.TryGetValue(steamId.SteamId64, out var user))
            throw new InvalidOperationException("player not found");

        OnPlayerRemoved(player, user.group);
        Task.Run(() => RemoveClientVipAsync(steamId));
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

    private async Task GiveClientVipAsync(string username, int accountId, string group, int timeSeconds,
        ulong steamId64)
    {
        try
        {
            var user = _vipCore.CreateNewUser(accountId, username, group, timeSeconds);
            await _vipCore.Database.AddUserToDb(user);

            _vipCore.Users.TryAdd(steamId64, user);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task RemoveClientVipAsync(SteamID steamId)
    {
        try
        {
            await _vipCore.Database.RemoveUserFromDb(steamId.AccountId);
            _vipCore.Users.Remove(steamId.SteamId64, out _);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public T GetFeatureValue<T>(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("User not found.");

        if (_vipCore.Config.Groups.TryGetValue(user.group, out var vipGroup))
        {
            if (vipGroup.Values.TryGetValue(feature, out var value))
            {
                _vipCore.PrintLogInfo(
                    "Checking feature: {feature} - {value}", feature, value);
                try
                {
                    return ((JsonElement)value).Deserialize<T>()!;
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
            var defaultJson =
                JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, defaultJson);
        }

        var configJson = File.ReadAllText(configFilePath);
        var config = JsonSerializer.Deserialize<T>(configJson);

        if (config == null)
            throw new FileNotFoundException($"File {name}.json not found or cannot be deserialized");

        return config;
    }

    public T LoadConfig<T>(string name)
    {
        return LoadConfig<T>(name, ModulesConfigDirectory);
    }

    public void SetPlayerCookie<T>(ulong steamId64, string key, T value)
    {
        var cookies = LoadCookies();

        if (value != null)
        {
            var existingCookie = cookies.FirstOrDefault(c => c.SteamId64 == steamId64);

            if (existingCookie != null)
                existingCookie.Features[key] = value;
            else
            {
                var newCookie = new PlayerCookie
                {
                    SteamId64 = steamId64,
                    Features = new Dictionary<string, object> { { key, value } }
                };
                cookies.Add(newCookie);
            }

            SaveCookies(cookies);
        }
    }

    public T GetPlayerCookie<T>(ulong steamId64, string key)
    {
        var cookies = LoadCookies();

        var cookie = cookies.FirstOrDefault(c => c.SteamId64 == steamId64);

        if (cookie != null && cookie.Features.TryGetValue(key, out var jsonElement))
        {
            try
            {
                var stringValue = jsonElement.ToString();
                var deserializedValue = (T)Convert.ChangeType(stringValue, typeof(T))!;
                return deserializedValue;
            }
            catch (Exception)
            {
                _vipCore.PrintLogError("Failed to deserialize feature '{feature}' value.", key);
            }
        }

        return default!;
    }

    private string GetCookiesFilePath()
    {
        return Path.Combine(CoreConfigDirectory, "vip_core_cookie.json");
    }

    private List<PlayerCookie> LoadCookies()
    {
        var filePath = GetCookiesFilePath();
        return File.Exists(filePath)
            ? JsonSerializer.Deserialize<List<PlayerCookie>>(File.ReadAllText(filePath)) ?? new List<PlayerCookie>()
            : new List<PlayerCookie>();
    }

    private void SaveCookies(List<PlayerCookie> cookies)
    {
        File.WriteAllText(GetCookiesFilePath(), JsonSerializer.Serialize(cookies));
    }
}