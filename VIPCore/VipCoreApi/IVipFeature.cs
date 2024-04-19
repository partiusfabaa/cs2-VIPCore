using CounterStrikeSharp.API.Core;
using static VipCoreApi.IVipCoreApi;

namespace VipCoreApi;

public interface IVipFeature
{
    string Feature { get; }
    IVipCoreApi Api { get; set; }
}

public abstract class VipFeatureBase : IVipFeature
{
    public abstract string Feature { get; }
    public IVipCoreApi Api { get; set; }

    public string CoreConfigDirectory => Api.CoreConfigDirectory;

    public string ModulesConfigDirectory => Api.ModulesConfigDirectory;

    public string GetDatabaseConnectionString => Api.GetDatabaseConnectionString;
    
    protected VipFeatureBase(IVipCoreApi api)
    {
        Api = api;

        api.OnPlayerSpawn += OnPlayerSpawn;
        api.PlayerLoaded += OnPlayerLoaded;
        api.PlayerRemoved += OnPlayerRemoved;
    }

    public virtual void OnPlayerSpawn(CCSPlayerController player)
    {
    }

    public virtual void OnPlayerLoaded(CCSPlayerController player, string group)
    {
    }

    public virtual void OnPlayerRemoved(CCSPlayerController player, string group)
    {
    }
    
    public virtual void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
    }

    public List<(string feautre, object value)> GetAllRegisteredFeatures() => Api.GetAllRegisteredFeatures().ToList();

    public bool IsClientVip(CCSPlayerController player) => Api.IsClientVip(player);

    public bool PlayerHasFeature(CCSPlayerController player) => Api.PlayerHasFeature(player, Feature);

    public FeatureState GetPlayerFeatureState(CCSPlayerController player) => Api.GetPlayerFeatureState(player, Feature);

    public void SetPlayerFeatureState(CCSPlayerController player, FeatureState newState) =>
        Api.SetPlayerFeatureState(player, Feature, newState);

    public T GetFeatureValue<T>(CCSPlayerController player) => Api.GetFeatureValue<T>(player, Feature);

    public string GetClientVipGroup(CCSPlayerController player) => Api.GetClientVipGroup(player);

    public string[] GetVipGroups() => Api.GetVipGroups();

    public void UpdateClientVip(CCSPlayerController player, string name = "", string group = "", int time = -1) =>
        Api.UpdateClientVip(player, name, group, time);

    public void SetClientVip(CCSPlayerController player, string group, int time) =>
        Api.SetClientVip(player, group, time);

    public void GiveClientVip(CCSPlayerController player, string group, int time) =>
        Api.GiveClientVip(player, group, time);

    public void GiveClientTemporaryVip(CCSPlayerController player, string group, int time) =>
        Api.GiveClientTemporaryVip(player, group, time);

    public void RemoveClientVip(CCSPlayerController player) => Api.RemoveClientVip(player);

    public void SetPlayerCookie<T>(ulong steamId64, string key, T value) => Api.SetPlayerCookie(steamId64, key, value);

    public T GetPlayerCookie<T>(ulong steamId64, string key) => Api.GetPlayerCookie<T>(steamId64, key);

    public T LoadConfig<T>(string name, string path) => Api.LoadConfig<T>(name, path);

    public T LoadConfig<T>(string name) => Api.LoadConfig<T>(name);

    public void PrintToChat(CCSPlayerController player, string message) => Api.PrintToChat(player, message);

    public void PrintToChatAll(string message) => Api.PrintToChatAll(message);

    public string GetTranslatedText(string name, params object[] args) => Api.GetTranslatedText(name, args);

    public bool IsPistolRound() => Api.IsPistolRound();
}