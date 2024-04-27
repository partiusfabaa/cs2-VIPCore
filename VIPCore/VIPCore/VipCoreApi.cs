using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

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

    public VipCoreApi(VipCore vipCore)
    {
        _vipCore = vipCore;
        CoreConfigDirectory = Path.Combine(Application.RootDirectory, "configs/plugins/VIPCore/");
    }

    public void CoreReady()
    {
        OnCoreReady?.Invoke();
    }

    public void RegisterFeature(VipFeatureBase vipFeatureBase, FeatureType featureType = FeatureType.Toggle)
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
            foreach (var (key, value) in config.Value.Values)
            {
                yield return (key, value);
            }
        }
    }

    public FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        return user.FeatureState.GetValueOrDefault(feature, FeatureState.NoAccess);
    }

    public void SetPlayerFeatureState(CCSPlayerController player, string feature, FeatureState newState)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        if (!user.FeatureState.ContainsKey(feature))
            throw new InvalidOperationException("feature not found");

        user.FeatureState[feature] = newState;
    }

    public bool IsClientVip(CCSPlayerController player)
    {
        return _vipCore.IsPlayerVip(player);
    }

    public bool PlayerHasFeature(CCSPlayerController player, string feature)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user)) return false;

        if (user is null or { group: null }) return false;

        if (!_vipCore.Config.Groups.TryGetValue(user.group, out var vipGroup)) return false;

        return vipGroup.Values.Any(vipGroupValue =>
            vipGroupValue.Key == feature && !string.IsNullOrEmpty(vipGroupValue.Value.ToString()));
    }

    public string GetClientVipGroup(CCSPlayerController player)
    {
        if (!_vipCore.Users.TryGetValue(player.SteamID, out var user))
            throw new InvalidOperationException("player not found");

        return user.group;
    }

    public string[] GetVipGroups()
    {
        var groups = _vipCore.Config.Groups;
        if (groups.Count == 0)
            return Array.Empty<string>();

        return groups.Keys.ToArray();
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

        var user = _vipCore.CreateNewUser(accountId, name, group, time);
        _vipCore.Users.TryAdd(steamId64, user);
        _vipCore.SetClientFeature(steamId64, group);
        _vipCore.IsClientVip[player.Slot] = true;

        if (!isTemporary)
        {
            Task.Run(() => _vipCore.Database.AddUserToDb(user));
        }
    }

    public void RemoveClientVip(CCSPlayerController player)
    {
        var steamId = new SteamID(player.SteamID);

        if (!_vipCore.Users.TryGetValue(steamId.SteamId64, out var user))
            throw new InvalidOperationException("player not found");

        OnPlayerRemoved(player, user.group);
        Task.Run(() => RemoveClientVipAsync(steamId));
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

            File.WriteAllText(configFilePath,
                JsonSerializer.Serialize(defaultConfig,
                    new JsonSerializerOptions
                        { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip }));
            return defaultConfig;
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
    
    public IMenu CreateMenu(string title)
    {
        return _vipCore.CoreConfig.UseCenterHtmlMenu ? new CenterHtmlMenu(title, _vipCore) : new ChatMenu(title);
    }

    public void SetPlayerCookie<T>(ulong steamId64, string key, T value)
    {
        var cookies = LoadCookies();

        if (value != null)
        {
            var existingCookie = cookies.Find(c => c.SteamId64 == steamId64);

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

        var cookie = cookies.Find(c => c.SteamId64 == steamId64);

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