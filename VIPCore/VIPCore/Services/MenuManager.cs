using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;

namespace VIPCore.Services;

public class MenuManager
{
    private static readonly string SortMenuPath = Path.Combine(ConfigSystem.CoreConfigDirectory, "sort_menu.txt");

    private readonly Plugin _plugin;
    private readonly VipCoreApi _api;
    private readonly PlayersManager _players;
    private readonly FeatureManager _featureManager;
    private readonly CookieService _cookies;
    private readonly Config<VipConfig> _coreConfig;

    private readonly ConcurrentDictionary<ulong, (CCSPlayerController Target, string Group)> _pendingCustomTime = new();
    private string[] _sortedItems = [];

    public MenuManager(
        Plugin plugin,
        VipCoreApi api,
        PlayersManager players,
        FeatureManager featureManager,
        CookieService cookies,
        Config<VipConfig> coreConfig)
    {
        _plugin = plugin;
        _api = api;
        _players = players;
        _featureManager = featureManager;
        _cookies = cookies;
        _coreConfig = coreConfig;

        LoadSortOrder();

        plugin.AddCommand("css_vip", "command that opens the VIP MENU", (player, _) => OpenVipMenu(player));
        plugin.AddCommand("css_vip_admin", "opens the VIP admin menu", OnAdminMenuCommand);
        plugin.AddCommand("css_admin_vip", "opens the VIP admin menu", OnAdminMenuCommand);
        plugin.AddCommandListener("say", OnSayCommand);
        plugin.AddCommandListener("say_team", OnSayCommand);
    }

    public void LoadSortOrder()
    {
        if (!File.Exists(SortMenuPath))
            File.WriteAllLines(SortMenuPath, ["Health", "Armor"]);

        _sortedItems = File.ReadAllLines(SortMenuPath);
    }

    private void OpenVipMenu(CCSPlayerController? player)
    {
        if (player is null) return;

        if (!_api.IsPlayerVip(player) || !_players.TryGetPlayer(player, out var vipPlayer))
        {
            _plugin.PrintToChat(player, _plugin.Translate(player, "vip.NoAccess"));
            return;
        }

        var menu = _api.CreateMenu(_plugin.Translate(player, "menu.Title", vipPlayer.Group));

        var features = _featureManager.GetFeatures()
            .Where(f => f.Type is not FeatureType.Hide && vipPlayer.HasFeature(f.Name))
            .OrderBy(f => Array.IndexOf(_sortedItems, f.Name))
            .ThenBy(f => f.Name);

        foreach (var feature in features)
        {
            var state = vipPlayer.FeatureStates.GetValueOrDefault(feature.Name, FeatureState.NoAccess);

            var args = new FeatureDisplayArgs
            {
                Controller = player,
                Feature = feature,
                State = state,
                Display = feature.Type is FeatureType.Toggle ? $"[{StateText(player, state)}]" : string.Empty
            };

            try
            {
                feature.OnFeatureDisplay(args);
                feature.OnFeatureDisplayEvent?.Invoke(args);
            }
            catch (Exception e)
            {
                _plugin.Logger.LogError("Exception in OnFeatureDisplay of '{feature}': {e}", feature.Name, e);
            }

            var name = _plugin.Translate(player, feature.Name);
            menu.AddMenuOption(string.IsNullOrEmpty(args.Display) ? name : $"{name} {args.Display}",
                (controller, _) => OnFeatureSelected(controller, vipPlayer, feature),
                state is FeatureState.NoAccess);
        }

        menu.Open(player);
    }

    private void OnFeatureSelected(CCSPlayerController player, VipPlayer vipPlayer, VipFeature feature)
    {
        if (vipPlayer.Disconnected || !_api.IsPlayerVip(player)) return;

        var state = vipPlayer.FeatureStates.GetValueOrDefault(feature.Name, FeatureState.NoAccess);
        if (state is FeatureState.NoAccess) return;

        var args = new PlayerUseFeatureEventArgs
        {
            Controller = player,
            Feature = feature,
            State = state
        };

        _api.InvokeOnPlayerUseFeature(args);

        if (!args.Allow)
        {
            OpenVipMenu(player);
            return;
        }

        if (feature.Type is not FeatureType.Selectable)
        {
            var newState = state is FeatureState.Enabled ? FeatureState.Disabled : FeatureState.Enabled;
            vipPlayer.FeatureStates[feature.Name] = newState;

            if (feature.Type is FeatureType.Toggle)
                _cookies.Set(player.SteamID, feature.Name, (int)newState);

            _plugin.PrintToChat(player,
                $"{_plugin.Translate(player, feature.Name)}: {StateText(player, newState)}");
        }

        try
        {
            feature.OnSelectItem(player, feature);
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError("Exception in OnSelectItem of '{feature}': {e}", feature.Name, e);
        }

        if (_coreConfig.Value.ReOpenMenuAfterItemClick && feature.Type is not FeatureType.Selectable)
            OpenVipMenu(player);
    }

    private string StateText(CCSPlayerController player, FeatureState state) => state switch
    {
        FeatureState.Enabled => _plugin.Translate(player, "chat.Enabled"),
        FeatureState.Disabled => _plugin.Translate(player, "chat.Disabled"),
        _ => _plugin.Translate(player, "chat.NoAccess")
    };

    private bool HasAdminAccess(CCSPlayerController? player) =>
        player is not null && AdminManager.PlayerHasPermissions(player, _coreConfig.Value.AdminMenuPermission);

    private void OnAdminMenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player is null) return;

        if (!HasAdminAccess(player))
        {
            _plugin.PrintToChat(player, _plugin.Translate(player, "vip.NoAccess"));
            return;
        }

        var menu = _api.CreateMenu(_plugin.Translate(player, "admin.menu.title"));

        menu.AddMenuOption(_plugin.Translate(player, "admin.menu.players_manage.add"),
            (admin, _) => OpenPlayersMenu(admin, false, target => OpenGroupsMenu(admin, target)));
        menu.AddMenuOption(_plugin.Translate(player, "admin.menu.players_manage.remove"),
            (admin, _) => OpenPlayersMenu(admin, true, target => RemoveVip(admin, target)));
        menu.AddMenuOption(_plugin.Translate(player, "admin.menu.reload_configs"), (admin, _) =>
        {
            _plugin.ReloadConfigs();
            _plugin.PrintToChat(admin, _plugin.Translate(admin, "admin.configs_reloaded_successfully"));
        });
        menu.AddMenuOption(_plugin.Translate(player, "admin.menu.reload_players"), (admin, _) =>
        {
            _players.ReloadAll();
            _plugin.PrintToChat(admin, _plugin.Translate(admin, "admin.players_reloaded_successfully"));
        });

        menu.Open(player);
    }

    private void OpenPlayersMenu(CCSPlayerController admin, bool vips, Action<CCSPlayerController> onSelect)
    {
        var menu = _api.CreateMenu(_plugin.Translate(admin, "admin.menu.select_player"));

        foreach (var target in Utilities.GetPlayers().Where(p => p is { IsBot: false, IsValid: true }))
        {
            if (!_players.TryGetPlayer(target, out var vipPlayer) || (vipPlayer.Data != null) != vips) continue;

            menu.AddMenuOption(target.PlayerName, (_, _) =>
            {
                if (target.IsValid) onSelect(target);
            });
        }

        menu.Open(admin);
    }

    private void OpenGroupsMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        var menu = _api.CreateMenu(_plugin.Translate(admin, "admin.menu.select_group"));

        foreach (var group in _api.GetVipGroups())
            menu.AddMenuOption(group, (_, _) => OpenTimeMenu(admin, target, group));

        menu.Open(admin);
    }

    private void OpenTimeMenu(CCSPlayerController admin, CCSPlayerController target, string group)
    {
        var menu = _api.CreateMenu(_plugin.Translate(admin, "admin.menu.select_time"));

        var times = new (string Label, int Seconds)[]
        {
            (_plugin.Translate(admin, "admin.menu.time.own"), -1),
            (_plugin.Translate(admin, "admin.menu.time.forever"), 0),
            (_plugin.Translate(admin, "admin.menu.time.1d"), 86400),
            (_plugin.Translate(admin, "admin.menu.time.7d"), 604800),
            (_plugin.Translate(admin, "admin.menu.time.30d"), 2592000),
            (_plugin.Translate(admin, "admin.menu.time.1y"), 31536000)
        };

        foreach (var (label, seconds) in times)
        {
            menu.AddMenuOption(label, (_, _) =>
            {
                if (seconds == -1)
                {
                    _pendingCustomTime[admin.SteamID] = (target, group);
                    _plugin.PrintToChat(admin, _plugin.Translate(admin, "admin.menu.enter_time_chat"));
                    return;
                }

                if (GiveVip(target, group, seconds))
                    _plugin.PrintToChat(admin,
                        _plugin.Translate(admin, "admin.menu.vip_given", target.PlayerName, group, label));
            });
        }

        menu.Open(admin);
    }

    private bool GiveVip(CCSPlayerController target, string group, int seconds)
    {
        if (!target.IsValid || target.AuthorizedSteamID is not { } steamId) return false;

        var expires = seconds == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds;
        _players.GiveVip(target, _players.CreateVipData(steamId.AccountId, target.PlayerName, group, expires),
            notify: true);

        return true;
    }

    private void RemoveVip(CCSPlayerController admin, CCSPlayerController target)
    {
        if (target.AuthorizedSteamID is not { } steamId) return;

        _players.RemoveVip(steamId.AccountId, target, notify: true);
        _plugin.PrintToChat(admin, _plugin.Translate(admin, "admin.menu.vip_removed", target.PlayerName));
    }

    private HookResult OnSayCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player is null || !_pendingCustomTime.TryRemove(player.SteamID, out var pending) || !HasAdminAccess(player))
            return HookResult.Continue;

        if (!int.TryParse(command.GetArg(1), out var seconds) || seconds < 0)
        {
            _plugin.PrintToChat(player, _plugin.Translate(player, "admin.menu.invalid_time"));
            return HookResult.Handled;
        }

        if (GiveVip(pending.Target, pending.Group, seconds))
            _plugin.PrintToChat(player, _plugin.Translate(player, "admin.menu.vip_given_seconds",
                pending.Target.PlayerName, pending.Group, seconds));

        return HookResult.Handled;
    }
}
