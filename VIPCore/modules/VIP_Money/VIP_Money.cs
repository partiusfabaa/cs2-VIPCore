using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Money;

public class VipMoney : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] Money";
    public override string ModuleVersion => "v1.0.1";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "Money";

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    public void OnPlayerSpawn(CCSPlayerController controller)
    {
        if (!_api.PlayerHasFeature(controller, Feature)) return;
        if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var moneyServices = controller.InGameMoneyServices;

        if (!_api.IsPistolRound())
        {
            if (moneyServices != null)
            {
                var moneyValue = _api.GetFeatureValue<string>(controller, Feature);

                if (string.IsNullOrWhiteSpace(moneyValue)) return;

                if (moneyValue.Contains("++"))
                    moneyServices.Account += int.Parse(moneyValue.Split("++")[1]);
                else
                    moneyServices.Account = int.Parse(moneyValue);
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}