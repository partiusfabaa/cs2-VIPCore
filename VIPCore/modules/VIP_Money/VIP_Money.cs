using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;
using CounterStrikeSharp.API.Modules.Cvars;

namespace VIP_Money;

public class VipMoney : BasePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] Money";
    public override string ModuleVersion => "v1.0.2";

    private IVipCoreApi? _api;
    private Money _money;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _money = new Money(_api);
            _api.RegisterFeature(_money);
        };
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_money);
    }
}

public class Money : VipFeatureBase
{
    public override string Feature => "Money";
    
    public Money(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (IsPistolRound()) return;
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var moneyServices = player.InGameMoneyServices;
        if (moneyServices == null) return;

        var moneyValue = GetFeatureValue<string>(player);

        if (string.IsNullOrWhiteSpace(moneyValue)) return;

        int maxmoney = ConVar.Find("mp_maxmoney").GetPrimitiveValue<int>();
        var money = int.Parse(moneyValue);

        if (moneyValue.Contains("++"))
        {
            if (moneyServices.Account + money.Split("++")[1] > maxmoney)
                moneyServices.Account = maxmoney;
            else
                moneyServices.Account += money.Split("++")[1];
        }
        else
        {
            if (money >
                maxmoney)
                moneyServices.Account = maxmoney;
            else
                moneyServices.Account = money;
        }
    }
}