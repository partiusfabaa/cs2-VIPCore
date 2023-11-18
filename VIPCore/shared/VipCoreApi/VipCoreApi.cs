using CounterStrikeSharp.API.Core;

namespace VipCoreApi;

public interface IVipCoreApi
{
    public void RegisterFeature(string feature, Action<CCSPlayerController> selectItem);
    public void UnRegisterFeature(string feature);
    public bool IsClientVip(CCSPlayerController player);
    public bool IsClientFeature(CCSPlayerController player, string feature);
    // public int GetFeatureIntValue(CCSPlayerController player, string feature);
    // public float GetFeatureFloatValue(CCSPlayerController player, string feature);
    // public string GetFeatureStringValue(CCSPlayerController player, string feature);
    // public bool GetFeatureBoolValue(CCSPlayerController player, string feature);
    public T GetFeatureValue<T>(CCSPlayerController player, string feature);
    public string GetClientVipGroup(CCSPlayerController player);
    public void GiveClientVip(CCSPlayerController player, string group, int time);
    public void RemoveClientVip(CCSPlayerController player);
    public void PrintToChat(CCSPlayerController player, string message);
    public bool VipCoreLoad();
}