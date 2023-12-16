using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Health;

public class VipHealth : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Health";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "Health";
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, selectItem: OnSelectItem);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (!_api.PlayerHasFeature(controller, Feature)) return;
        if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var playerPawnValue = controller.PlayerPawn.Value;

        var healthValue = _api.GetFeatureValue<int>(controller, Feature);

        if (healthValue <= 0 || playerPawnValue == null) return;
        
        playerPawnValue.Health = healthValue;
        playerPawnValue.MaxHealth = healthValue;
    }
    
    private void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        if (state == IVipCoreApi.FeatureState.Disabled)
        {
            _api.PrintToChat(player, $"{_api.GetTranslatedText(Feature)}:\x02 Off");
            return;
        }

        _api.PrintToChat(player, $"{_api.GetTranslatedText(Feature)}:\x06 On");
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}