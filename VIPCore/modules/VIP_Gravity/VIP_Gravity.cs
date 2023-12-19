using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Gravity;

public class VipGravity : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Gravity";
    public override string ModuleVersion => "1.0.0";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "Gravity";

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, selectItem: OnSelectItem);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (!_api.PlayerHasFeature(controller, Feature) ||
            _api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess) return;

        var playerPawnValue = controller.PlayerPawn.Value;

        if (playerPawnValue == null) return;
        
        playerPawnValue.GravityScale = _api.GetFeatureValue<float>(controller, Feature);
    }

    private void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        var playerPawnValue = player.PlayerPawn.Value;
        
        if (state == IVipCoreApi.FeatureState.Disabled)
        {
            if (playerPawnValue != null)
                playerPawnValue.GravityScale = 1.0f;
            return;
        }

        if (playerPawnValue != null)
            playerPawnValue.GravityScale = _api.GetFeatureValue<float>(player, Feature);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}