using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Modularity;
using VipCoreApi;

namespace VIP_Fov;

public class VipFov : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Fov";
    public override string ModuleVersion => "v1.0.0";

    private readonly UserSettings?[] _userSettings = new UserSettings?[65];
    private static readonly string Feature = "Fov";
    private IVipCoreApi _api = null!;

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            var cookie = _api.GetPlayerCookie<int>(Utilities.GetPlayerFromSlot(slot).SteamID, "player_fov");

            _userSettings[slot + 1] = new UserSettings()
            {
                Fov = cookie == 0 ? 90 : cookie
            };
        });
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => _userSettings[slot + 1] = null);
    }

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, IVipCoreApi.FeatureType.Selectable, OnSelectItem);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_userSettings[player.Index] == null) return;
        if (!_api.PlayerHasFeature(player, Feature)) return;

        ChangeFov(player);
    }

    private void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        if (_userSettings[player.Index] == null) return;

        var userFov = _api.GetFeatureValue<List<int>>(player, Feature);

        _userSettings[player.Index]!.Menu.MenuOptions.Clear();
        _userSettings[player.Index]!.Menu.AddMenuOption(Localizer["fov.Disable"], (controller, option) =>
        {
            _userSettings[player.Index]!.Fov = 90;
            ChangeFov(controller);
        }, _userSettings[player.Index]!.Fov == -1);
        foreach (var i in userFov)
        {
            _userSettings[player.Index]!.Menu.AddMenuOption(i.ToString(), (controller, option) =>
            {
                _userSettings[player.Index]!.Fov = i;
                ChangeFov(controller);
            }, _userSettings[player.Index]!.Fov == i);
        }

        ChatMenus.OpenMenu(player, _userSettings[player.Index]!.Menu);
    }

    private void ChangeFov(CCSPlayerController player)
    {
        if (_userSettings[player.Index] == null) return;

        var fov = (uint)_userSettings[player.Index]!.Fov;
        _api.SetPlayerCookie(player.SteamID, "player_fov", fov);
        player.DesiredFOV = fov;
        Utilities.SetStateChanged(player, "CBasePlayerController", "m_iDesiredFOV");
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}

public class UserSettings
{
    public int Fov { get; set; } = -1;
    public ChatMenu Menu { get; set; } = new("Fov");
}