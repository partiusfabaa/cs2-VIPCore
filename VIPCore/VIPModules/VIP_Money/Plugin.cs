using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using CounterStrikeSharp.API.Modules.Cvars;
using VipCoreApi.Enums;

namespace VIP_Money;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] Money";
    public override string ModuleVersion => "v2.0.0";

    private Money? _money;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = PluginCapability.Get();
        if (api == null) return;

        _money = new Money(api);
    }

    public override void Unload(bool hotReload)
    {
        _money?.Dispose();
    }
}

public class Money(IVipCoreApi api) : VipFeature<string>("Money", api)
{
    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (IsPistolRound() || !IsPlayerValid(player)) return;

        var moneyServices = player.InGameMoneyServices;
        if (moneyServices == null) return;

        var moneyValue = GetFeatureValue<string>(player);

        if (string.IsNullOrWhiteSpace(moneyValue)) return;

        var maxMoney = ConVar.Find("mp_maxmoney")!.GetPrimitiveValue<int>();

        if (moneyValue.Contains("++"))
        {
            var money = int.Parse(moneyValue.Split("++")[1]);
            if (moneyServices.Account + money > maxMoney)
                moneyServices.Account = maxMoney;
            else
                moneyServices.Account += money;
        }
        else
        {
            var money = int.Parse(moneyValue);
            moneyServices.Account = money > maxMoney ? maxMoney : money;
        }

        Utilities.SetStateChanged(player, "CCSPlayerController_InGameMoneyServices", "m_iAccount");
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
        {
            args.Display = $"[{GetValue(args.Controller)}]";
        }
    }
}