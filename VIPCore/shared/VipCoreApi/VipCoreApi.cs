using CounterStrikeSharp.API.Core;

namespace VipCoreApi;

public interface IVipCoreApi
{
    public string CoreConfigDirectory { get; }
    public string ModulesConfigDirectory { get; }
    public void RegisterFeature(string feature, Action<CCSPlayerController> selectItem);
    public void UnRegisterFeature(string feature);
    public bool IsClientVip(CCSPlayerController player);
    public bool IsClientFeature(CCSPlayerController player, string feature);
    public T GetFeatureValue<T>(CCSPlayerController player, string feature);
    public string GetClientVipGroup(CCSPlayerController player);
    public void GiveClientVip(CCSPlayerController player, string group, int time);
    public void RemoveClientVip(CCSPlayerController player);
    public void SetPlayerCookie<T>(ulong steamId64, string featureName, T value);
    public T GetPlayerCookie<T>(ulong steamId64, string featureName);
    public void PrintToChat(CCSPlayerController player, string message);
    public string GetTranslatedText(string feature);
    public event Action<CCSPlayerController>? OnPlayerSpawn;
    //public event Action? OnCoreReady;
}
