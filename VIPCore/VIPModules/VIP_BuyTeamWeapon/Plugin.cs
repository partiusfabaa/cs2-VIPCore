using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_BuyTeamWeapon;

public class VipBuyTeamWeapon : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] BuyTeamWeapon";
    public override string ModuleVersion => "v2.0.0";
    
    private BuyTeamWeapon? _buyTeamWeapon;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _buyTeamWeapon = new BuyTeamWeapon(this, api);
    }
    
    public override void Unload(bool hotReload)
    {
        _buyTeamWeapon?.Dispose();
    }
}

public class BuyTeamWeapon : VipFeature<bool>
{
    public BuyTeamWeapon(BasePlugin basePlugin, IVipCoreApi api) : base("BuyTeamWeapon", api, FeatureType.Hide)
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
        foreach (var player in Utilities.GetPlayers().Where(p => IsPlayerValid(p) &&  GetValue(p) && p.PawnIsAlive))
        {
            PrintToChat(player, 
                GetTranslatedText(player, "buyteamweapon.round_start", 
                    player.Team is CsTeam.Terrorist 
                ? "!m4a1, !m4a4, !glock"
                : "!ak47, !usp"));
        }

        return HookResult.Continue;
    }

    private void BuyWeapon(CCSPlayerController? player, string weaponName, int price)
    {
        if (player is null) return;
        
        if (!IsPlayerValid(player) || !GetValue(player) || !player.PawnIsAlive)
        {
            PrintToChat(player, GetTranslatedText(player, "vip.NoAccess"));
            return;
        }

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is not null && !playerPawn.InBuyZone)
        {
            PrintToChat(player, GetTranslatedText(player, "buyteamweapon.in_buy_zone"));
            return;
        }
        
        var moneySerivce = player.InGameMoneyServices;
        if (moneySerivce is null) return;

        if (moneySerivce.Account < price)
        {
            PrintToChat(player, GetTranslatedText(player, "buyteamweapon.no_money"));
            return;
        }

        moneySerivce.Account -= price;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
        
        player.GiveNamedItem(weaponName);
    }
}