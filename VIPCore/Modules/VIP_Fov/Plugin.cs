using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Fov;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fov";
    public override string ModuleVersion => "v2.0.0";
    
    private Fov? _fov;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _fov = new Fov(api);
    }

    public override void Unload(bool hotReload)
    {
        _fov?.Dispose();
    }
}

public class Fov(IVipCoreApi api) : VipFeature<List<int>>("Fov", api, FeatureType.Selectable)
{
    private readonly int[] _fovSettings = new int[67];

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip)
    {
        if (vip)
        {
            _fovSettings[player.Slot] = 90;
            ChangeFov(player);
        }
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        var cookie = GetPlayerCookie<int>(player, "player_fov");

        _fovSettings[player.Slot] = cookie == 0 ? 90 : cookie;
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        var userFov = GetValue(player);
        if (userFov is null) return;

        var menu = CreateMenu(GetTranslatedText(player, "Fov"));
        menu.AddMenuOption(GetTranslatedText("fov.Disable"), (controller, _) =>
        {
            _fovSettings[player.Slot] = 90;

            PrintToChat(player, GetTranslatedText("fov.Off"));
            ChangeFov(controller);
        }, _fovSettings[player.Slot] == 90);
        
        foreach (var i in userFov)
        {
            menu.AddMenuOption(i.ToString(), (controller, _) =>
            {
                _fovSettings[player.Slot] = i;
                PrintToChat(player, GetTranslatedText("fov.On", i));
                ChangeFov(controller);
            }, _fovSettings[player.Slot] == i);
        }

        menu.Open(player);
    }

    private void ChangeFov(CCSPlayerController player)
    {
        var fov = (uint)_fovSettings[player.Slot];

        if (IsPlayerVip(player))
        {
            SetPlayerCookie(player, "player_fov", fov);
        }
        
        player.DesiredFOV = fov;
        Utilities.SetStateChanged(player, "CBasePlayerController", "m_iDesiredFOV");
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !PlayerHasFeature(player))
            _fovSettings[player.Slot] = 90;

        ChangeFov(player);
    }
}