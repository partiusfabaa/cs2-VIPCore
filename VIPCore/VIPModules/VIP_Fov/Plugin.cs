using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Enum;
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

        var fovDisableKey = GetTranslatedText(player, "fov.Disable");
        if (_fovSettings[player.Slot] == 90)
        {
            menu.AddItem(fovDisableKey, DisableOption.DisableShowNumber);
        }
        else
        {
            menu.AddItem(fovDisableKey, (controller, _) =>
            {
                _fovSettings[player.Slot] = 90;

                PrintToChat(player, GetTranslatedText("fov.Off"));
                ChangeFov(controller);
            });
        }

        foreach (var i in userFov)
        {
            if (_fovSettings[player.Slot] == i)
            {
                menu.AddItem(i.ToString(), DisableOption.DisableShowNumber);
            }
            else
            {
                menu.AddItem(i.ToString(), (controller, _) =>
                {
                    _fovSettings[player.Slot] = i;
                    PrintToChat(player, GetTranslatedText("fov.On", i));
                    ChangeFov(controller);
                });
            }
        }

        menu.Display(player, 0);
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