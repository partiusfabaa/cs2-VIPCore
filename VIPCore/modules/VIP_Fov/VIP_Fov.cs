using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Fov;

public class VipFov : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fov";
    public override string ModuleVersion => "v1.0.1";
    
    private IVipCoreApi? _api;
    private Fov _fov;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _fov = new Fov(this, _api);
            _api.RegisterFeature(_fov, FeatureType.Selectable);
        };
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_fov);
    }
}

public class UserSettings
{
    public int Fov { get; set; } = -1;
    public ChatMenu Menu { get; set; } = new("Fov");
}

public class Fov : VipFeatureBase
{
    public override string Feature => "Fov";
    private readonly UserSettings?[] _userSettings = new UserSettings?[65];

    public Fov(VipFov vipFov, IVipCoreApi api) : base(api)
    {
        vipFov.RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            var cookie = GetPlayerCookie<int>(Utilities.GetPlayerFromSlot(slot).SteamID, "player_fov");

            _userSettings[slot + 1] = new UserSettings { Fov = cookie == 0 ? 90 : cookie };
        });
        vipFov.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (!IsClientVip(player))
            {
                _userSettings[player.Index]!.Fov = 90;
                ChangeFov(player);
            }

            _userSettings[player.Index] = null;
            return HookResult.Continue;
        });
    }
    
    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        if (_userSettings[player.Index] == null) return;

        var userFov = GetFeatureValue<List<int>>(player);

        _userSettings[player.Index]!.Menu.MenuOptions.Clear();
        _userSettings[player.Index]!.Menu.AddMenuOption(GetTranslatedText("fov.Disable"), (controller, option) =>
        {
            _userSettings[player.Index]!.Fov = 90;

            PrintToChat(player, GetTranslatedText("fov.Off"));
            ChangeFov(controller);
        }, _userSettings[player.Index]!.Fov == 90);
        foreach (var i in userFov)
        {
            _userSettings[player.Index]!.Menu.AddMenuOption(i.ToString(), (controller, option) =>
            {
                _userSettings[player.Index]!.Fov = i;
                PrintToChat(player, GetTranslatedText("fov.On", i));
                ChangeFov(controller);
            }, _userSettings[player.Index]!.Fov == i);
        }

        MenuManager.OpenChatMenu(player, _userSettings[player.Index]!.Menu);
    }

    private void ChangeFov(CCSPlayerController player)
    {
        if (_userSettings[player.Index] == null) return;

        var fov = (uint)_userSettings[player.Index]!.Fov;
        SetPlayerCookie(player.SteamID, "player_fov", fov);
        player.DesiredFOV = fov;
        Utilities.SetStateChanged(player, "CBasePlayerController", "m_iDesiredFOV");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_userSettings[player.Index] == null) return;
        if (!PlayerHasFeature(player))
            _userSettings[player.Index]!.Fov = 90;

        ChangeFov(player);
    }
}