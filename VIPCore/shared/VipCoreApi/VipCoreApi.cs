using CounterStrikeSharp.API.Core;

namespace VipCoreApi;

public interface IVipCoreApi
{
    enum FeatureState
    {
        Enabled,
        Disabled,
        NoAccess
    }

    enum FeatureType
    {
        Toggle,
        Selectable,
        Hide
    }
    
    public string CoreConfigDirectory { get; }
    public string ModulesConfigDirectory { get; }
    public string GetDatabaseConnection { get; }
    public FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature);

    public void RegisterFeature(string feature, FeatureType featureType = FeatureType.Toggle,
        Action<CCSPlayerController, FeatureState>? selectItem = null);

    public void UnRegisterFeature(string feature);
    public bool IsClientVip(CCSPlayerController player);
    public bool PlayerHasFeature(CCSPlayerController player, string feature);
    public T GetFeatureValue<T>(CCSPlayerController player, string feature);
    public string GetClientVipGroup(CCSPlayerController player);
    public void GiveClientVip(CCSPlayerController player, string group, int time);
    public void RemoveClientVip(CCSPlayerController player);
    public void SetPlayerCookie<T>(ulong steamId64, string featureName, T value);
    public T GetPlayerCookie<T>(ulong steamId64, string featureName);
    public void PrintToChat(CCSPlayerController player, string message);
    public void PrintToChatAll(string message);
    public string GetTranslatedText(string name, params object[] args);
    public bool IsPistolRound();
    public T LoadConfig<T>(string name, string path);
    public T LoadConfig<T>(string name);
    public event Action<CCSPlayerController>? OnPlayerSpawn;
    public event Action<CCSPlayerController, string>? PlayerLoaded;
    public event Action<CCSPlayerController, string>? PlayerRemoved;
    //public event Action? OnCoreReady;
}