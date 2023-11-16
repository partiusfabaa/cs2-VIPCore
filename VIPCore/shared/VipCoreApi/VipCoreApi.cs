using CounterStrikeSharp.API.Core;

namespace VipCoreApi;

public interface IVipCoreApi
{
    public void RegisterFeature(string feature, string defaultValue);
    public void UnRegisterFeature(string feature);
    public bool IsClientVip(CCSPlayerController player);
    public bool IsClientFeature(CCSPlayerController player, string feature);
    public int GetFeatureIntValue(CCSPlayerController player, string feature);
    public float GetFeatureFloatValue(CCSPlayerController player, string feature);
    public string GetFeatureStringValue(CCSPlayerController player, string feature);
}