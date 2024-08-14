using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_BuyTeamWeapon;

public class VipBuyTeamWeapon : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] BuyTeamWeapon";
    public override string ModuleVersion => "v1.0.0";
    
    private BuyTeamWeapon _buyTeamWeapon;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _buyTeamWeapon = new BuyTeamWeapon(this, _api);
        _api.RegisterFeature(_buyTeamWeapon, FeatureType.Hide);
    }
    
    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_buyTeamWeapon);
    }
}

public class BuyTeamWeapon : VipFeatureBase
{
    public override string Feature => "BuyTeamWeapon";
    public BuyTeamWeapon(BasePlugin basePlugin, IVipCoreApi api) : base(api)
    {
        basePlugin.RegisterEventHandler<EventRoundStart>(EventRoundStart);
        
        basePlugin.AddCommand("css_ak47", "buy ak47", (player, _) => BuyWeapon(player, "weapon_ak47", 2700));
        basePlugin.AddCommand("css_m4a1", "buy m4a1", (player, _) => BuyWeapon(player, "weapon_m4a1_silencer", 2900));
        basePlugin.AddCommand("css_m4a4", "buy m4a4", (player, _) => BuyWeapon(player, "weapon_m4a1", 3000));
        basePlugin.AddCommand("css_glock", "buy glock", (player, _) => BuyWeapon(player, "weapon_glock", 200));
        basePlugin.AddCommand("css_usp", "buy usp", (player, _) => BuyWeapon(player, "weapon_usp_silencer", 200));
    }
    
    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        foreach (var player in Utilities.GetPlayers().Where(u => IsClientVip(u) && PlayerHasFeature(u) && GetFeatureValue<bool>(u) && u.PawnIsAlive))
        {
            PrintToChat(player, 
                GetTranslatedText("buyteamweapon.round_start", 
                    player.Team is CsTeam.Terrorist 
                ? "!m4a1, !m4a4, !glock"
                : "!ak47, !usp"));
        }

        return HookResult.Continue;
    }

    private void BuyWeapon(CCSPlayerController? player, string weaponName, int price)
    {
        if (player is null) return;
        
        if (!IsClientVip(player) || !PlayerHasFeature(player) || !GetFeatureValue<bool>(player) || !player.PawnIsAlive)
        {
            PrintToChat(player, GetTranslatedText("vip.NoAccess"));
            return;
        }

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is not null && !playerPawn.InBuyZone)
        {
            PrintToChat(player, GetTranslatedText("buyteamweapon.in_buy_zone"));
            return;
        }
        
        var moneySerivce = player.InGameMoneyServices;
        if (moneySerivce is null) return;

        if (moneySerivce.Account < price)
        {
            PrintToChat(player, GetTranslatedText("buyteamweapon.no_money"));
            return;
        }

        moneySerivce.Account -= price;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
        
        player.GiveNamedItem(weaponName);
    }
}