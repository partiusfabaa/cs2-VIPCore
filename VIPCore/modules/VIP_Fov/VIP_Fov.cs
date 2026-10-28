using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Fov;

public class VipFov : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fov";
    public override string ModuleVersion => "v2.0.0";
    
    private IVipCoreApi? _api;
    private Fov? _fov;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _fov = new Fov(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _fov?.Dispose();
    }
}

public class Fov : VipFeature<List<int>>
{
    private readonly int[] _fovSettings = new int[67];

    public Fov(BasePlugin vipFov, IVipCoreApi api) : base("Fov", api, FeatureType.Selectable)
    {
        vipFov.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (player != null && !IsPlayerVip(player))
            {
                _fovSettings[player.Slot] = 90;
                ChangeFov(player);
            }
            return HookResult.Continue;
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        var cookie = GetPlayerCookie<int>(player, "player_fov");

        _fovSettings[player.Slot] = cookie == 0 ? 90 : cookie;
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        var userFov = GetValue(player);

        var menu = CreateMenu(GetTranslatedText(Name));
        menu.AddMenuOption(GetTranslatedText("fov.Disable"), (controller, option) =>
        {
            _fovSettings[player.Slot] = 90;

            PrintToChat(player, GetTranslatedText("fov.Off"));
            ChangeFov(controller);
        }, _fovSettings[player.Slot] == 90);
        
        foreach (var i in userFov)
        {
            menu.AddMenuOption(i.ToString(), (controller, option) =>
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
        
        if(IsPlayerVip(player))
            SetPlayerCookie(player, "player_fov", fov);
        
        player.DesiredFOV = fov;
        Utilities.SetStateChanged(player, "CBasePlayerController", "m_iDesiredFOV");
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip) return;

        if (!PlayerHasFeature(player))
            _fovSettings[player.Slot] = 90;

        ChangeFov(player);
    }
}