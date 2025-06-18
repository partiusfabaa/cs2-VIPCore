using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Interface;
using VIPCore.Configs;
using VIPCore.Player;
using System.Collections.Concurrent;

namespace VIPCore.Services;

public class MenuManager : IFeature
{
    private readonly Plugin _plugin;
    private readonly VipCoreApi _api;
    private readonly Config<VipConfig> _coreConfig;
    private readonly Config<GroupsConfig> _groupsConfig;
    private readonly PlayersManager _playersManager;
    private readonly ConcurrentDictionary<ulong, (CCSPlayerController target, string group)> _pendingCustomTime = new();

    public MenuManager(
        Plugin plugin,
        VipCoreApi api,
        Config<VipConfig> coreConfig,
        Config<GroupsConfig> groupsConfig,
        PlayersManager playersManager)
    {
        _plugin = plugin;
        _api = api;
        _coreConfig = coreConfig;
        _groupsConfig = groupsConfig;
        _playersManager = playersManager;

        plugin.AddCommand("css_vip_admin", "", CreateAdminMenu);
        plugin.AddCommand("css_admin_vip", "", CreateAdminMenu);
        plugin.AddCommandListener("say", OnSayCommand);
        plugin.AddCommandListener("say_team", OnSayCommand);
    }

    private void CreateAdminMenu(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player is null || !AdminManager.PlayerHasPermissions(player, _coreConfig.Value.AdminMenuPermission))
            return;

        var localizer = _plugin.Localizer;
        var menu = _api.CreateMenu(localizer.ForPlayer(player, "admin.menu.title"));

        menu.AddItem(localizer.ForPlayer(player, "admin.menu.players_manage"), (p, i) =>
            OnPlayersManage(p, i, menu));
        menu.AddItem(localizer.ForPlayer(player, "admin.menu.reload_configs"), OnConfigsReload);
        menu.AddItem(localizer.ForPlayer(player, "admin.menu.reload_players"), OnPlayersReload);

        menu.Display(player, 0);
    }

    private void OnPlayersManage(CCSPlayerController player, ItemOption option, IMenu prevMenu)
    {
        var menu = _api.CreateMenu(option.Text);
        menu.PrevMenu = prevMenu;

        var localizer = _plugin.Localizer;
        menu.AddItem(localizer.ForPlayer(player, "admin.menu.players_manage.add"), (p, i) =>
            OnPlayersMenu(player, menu, false, target => ShowAddVipMenu(player, target)));
        menu.AddItem(localizer.ForPlayer(player, "admin.menu.players_manage.remove"), (p, i) =>
            OnPlayersMenu(player, menu, true, target => RemoveVipFromPlayer(player, target)));

        //TODO: Implement VIP player update
        // menu.AddItem(localizer.ForPlayer(player, "admin.menu.players_manage.set"));

        menu.Display(player, 0);
    }

    private void OnPlayersMenu(
        CCSPlayerController player,
        IMenu prevMenu,
        bool? isVip,
        Action<CCSPlayerController> handler)
    {
        var menu = _api.CreateMenu(_plugin.Localizer.ForPlayer(player, "admin.menu.select_player"));
        menu.PrevMenu = prevMenu;

        foreach (var target in Utilities.GetPlayers())
        {
            if (!_playersManager.TryGetPlayer(target, out var vipPlayer) ||
                isVip != null && ((isVip.Value && !vipPlayer.IsVip) || (!isVip.Value && vipPlayer.IsVip))) continue;

            menu.AddItem(target.PlayerName, (_, _) => handler(target));
        }

        menu.Display(player, 0);
    }

    private void ShowAddVipMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        var menu = _api.CreateMenu(_plugin.Localizer.ForPlayer(admin, "admin.menu.select_group"));
        menu.PrevMenu = null;

        foreach (var group in _api.GetVipGroups())
        {
            menu.AddItem(group, (p, i) => ShowAddVipTimeMenu(admin, target, group));
        }

        menu.Display(admin, 0);
    }

    private void ShowAddVipTimeMenu(CCSPlayerController admin, CCSPlayerController target, string group)
    {
        var menu = _api.CreateMenu(_plugin.Localizer.ForPlayer(admin, "admin.menu.select_time"));
        menu.PrevMenu = null;

        var times = new[]
        {
            (label: _plugin.Localizer.ForPlayer(admin, "admin.menu.time.own"), seconds: -1),
            (label: _plugin.Localizer.ForPlayer(admin, "admin.menu.time.forever"), seconds: 0),
            (label: _plugin.Localizer.ForPlayer(admin, "admin.menu.time.1d"), seconds: 86400),
            (label: _plugin.Localizer.ForPlayer(admin, "admin.menu.time.7d"), seconds: 604800),
            (label: _plugin.Localizer.ForPlayer(admin, "admin.menu.time.30d"), seconds: 2592000),
            (label: _plugin.Localizer.ForPlayer(admin, "admin.menu.time.1y"), seconds: 31536000)
        };
        foreach (var (label, seconds) in times)
        {
            menu.AddItem(label, (p, i) =>
            {
                if (seconds == -1)
                {
                    _pendingCustomTime[admin.SteamID] = (target, group);
                    _playersManager.PrintToChat(admin,
                        _plugin.Localizer.ForPlayer(admin, "admin.menu.enter_time_chat"));
                }
                else
                {
                    _api.GivePlayerVip(target, group, seconds);
                    _playersManager.PrintToChat(admin,
                        _plugin.Localizer.ForPlayer(admin, "admin.menu.vip_given", target.PlayerName, group, label));
                }
            });
        }

        menu.Display(admin, 0);
    }

    private void RemoveVipFromPlayer(CCSPlayerController admin, CCSPlayerController target)
    {
        _api.RemovePlayerVip(target);
        _playersManager.PrintToChat(admin,
            _plugin.Localizer.ForPlayer(admin, "admin.menu.vip_removed", target.PlayerName));
    }

    private void OnConfigsReload(CCSPlayerController player, ItemOption option)
    {
        _coreConfig.Load();
        _groupsConfig.Load();

        _playersManager.PrintToChat(player, _plugin.Localizer.ForPlayer(player, "admin.configs_reloaded_successfully"));
    }

    private void OnPlayersReload(CCSPlayerController player, ItemOption option)
    {
        _playersManager.UpdatePlayers();
        _playersManager.PrintToChat(player, _plugin.Localizer.ForPlayer(player, "admin.players_reloaded_successfully"));
    }

    private HookResult OnSayCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || 
            !_pendingCustomTime.TryRemove(player.SteamID, out var pending) ||
            !AdminManager.PlayerHasPermissions(player, _coreConfig.Value.AdminMenuPermission))
            return HookResult.Continue;

        var text = command.GetArg(1);
        if (!int.TryParse(text, out var seconds) || seconds < 0)
        {
            _playersManager.PrintToChat(player, _plugin.Localizer.ForPlayer(player, "admin.menu.invalid_time"));
            return HookResult.Continue;
        }

        _api.GivePlayerVip(pending.target, pending.group, seconds);
        _playersManager.PrintToChat(player,
            _plugin.Localizer.ForPlayer(player, "admin.menu.vip_given_seconds", pending.target.PlayerName,
                pending.group, seconds));

        return HookResult.Stop;
    }
}