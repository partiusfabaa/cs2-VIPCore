using CounterStrikeSharp.API.Core;
using VipCoreApi.Enums;

namespace VipCoreApi;

public interface IVipFeatureStateApi
{
    // The stored selection before temporary restrictions such as pistol rounds.
    FeatureState GetPlayerFeatureSelection(CCSPlayerController player, string feature);

    event Action<CCSPlayerController>? PlayerFeatureStatesChanged;
}
