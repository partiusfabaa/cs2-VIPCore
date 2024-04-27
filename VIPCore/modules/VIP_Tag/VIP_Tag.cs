using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Tag;

public class VIPTag : BasePlugin
{
    public override string ModuleAuthor => "Toil";
    public override string ModuleName => "[VIP] Tag";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi? _api;
    private Tag? _tag;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _tag = new Tag(this, _api);
        _api.RegisterFeature(_tag, FeatureType.Selectable);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _tag != null)
        {
            _api?.UnRegisterFeature(_tag);
        }
    }
}

public class UserSettings
{
    public string Tag { get; set; } = "";
    public ChatMenu Menu { get; set; } = new("Tag");
}

public class Tag : VipFeatureBase
{
    public override string Feature => "Tag";

    private readonly UserSettings?[] _userSettings = new UserSettings?[65];

    public Tag(VIPTag vipTag, IVipCoreApi api) : base(api)
    {
        vipTag.RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            var cookie = GetPlayerCookie<string>(Utilities.GetPlayerFromSlot(slot).SteamID, "player_tag");

            _userSettings[slot + 1] = new UserSettings { Tag = cookie };
        });
        vipTag.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (!IsClientVip(player))
            {
                _userSettings[player.Index]!.Tag = "";
                ChangeTag(player);
            }
            
            _userSettings[player.Index] = null;
            return HookResult.Continue;
        });
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        if (_userSettings[player.Index] == null) return;

        var userTag = GetFeatureValue<List<string>>(player);
        
        _userSettings[player.Index]!.Menu.MenuOptions.Clear();
        _userSettings[player.Index]!.Menu.AddMenuOption(GetTranslatedText("tag.Disable"), (controller, option) =>
        {
            _userSettings[player.Index]!.Tag = "";

            PrintToChat(player, GetTranslatedText("tag.Off"));
            ChangeTag(controller);
        }, _userSettings[player.Index]!.Tag == "");
        foreach (var tag in userTag)
        {
            _userSettings[player.Index]!.Menu.AddMenuOption(tag, (controller, option) =>
            {
                _userSettings[player.Index]!.Tag = tag;
                PrintToChat(player, GetTranslatedText("tag.On", tag));
                ChangeTag(controller);
            }, _userSettings[player.Index]!.Tag == tag);
        }

        MenuManager.OpenChatMenu(player, _userSettings[player.Index]!.Menu);
    }

    private void ChangeTag(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.IsBot || player.TeamNum == (int)CsTeam.Spectator) return;
        if (_userSettings[player.Index] == null) return;

        var tag = _userSettings[player.Index]!.Tag;
        SetPlayerCookie(player.SteamID, "player_tag", tag);
        player.Clan = tag;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_userSettings[player.Index] == null) return;
        if (!PlayerHasFeature(player))
            _userSettings[player.Index]!.Tag = "";

        ChangeTag(player);
    }
}