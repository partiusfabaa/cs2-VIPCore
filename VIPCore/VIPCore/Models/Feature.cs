using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIPCore.Models;

public class Feature
{
    public IVipCoreApi.FeatureType FeatureType { get; set; }
    public Action<CCSPlayerController, IVipCoreApi.FeatureState>? OnSelectItem { get; set; }
}