using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using VIPCore.Configs;
using VIPCore.Player;

namespace VIPCore.Services;

public class CommandsService(Plugin plugin, PlayersManager players, Config<GroupsConfig> groupsConfig)
{
    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_adduser")]
    public void OnCmdAddUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount != 4 || !int.TryParse(command.GetArg(3), out var time) || time < 0)
        {
            plugin.ReplyToCommand(controller,
                $"Usage: css_vip_adduser <steamid or accountid> <group> <time_{plugin.TimeUnitName}>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
        {
            plugin.ReplyToCommand(controller, $"Invalid SteamID/AccountID: {command.GetArg(1)}");
            return;
        }

        var group = command.GetArg(2);
        if (!groupsConfig.Value.ContainsKey(group))
        {
            plugin.ReplyToCommand(controller, $"VIP group '{group}' was not found!");
            return;
        }

        var username = player == null ? "unknown" : player.PlayerName;

        players.GiveVip(player, players.CreateVipData(accountId, username, group, plugin.ExpiresIn(time)),
            notify: true);

        var timeText = time == 0 ? "permanent" : $"{time} {plugin.TimeUnitName}";
        plugin.ReplyToCommand(controller,
            $"VIP added: {username} (AccountID {accountId}) -> group '{group}', time: {timeText}.");
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_updateuser")]
    public void OnCmdUpdateUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount != 4)
        {
            plugin.ReplyToCommand(controller,
                "Usage: css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>");
            plugin.ReplyToCommand(controller,
                "Use '-' or '-s' to keep a field unchanged. Example: css_vip_updateuser \"STEAM_0:0:123456\" -s 3600");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
        {
            plugin.ReplyToCommand(controller, $"Invalid SteamID/AccountID: {command.GetArg(1)}");
            return;
        }

        string? group = command.GetArg(2);
        if (group is "-" or "-s")
            group = null;
        else if (!groupsConfig.Value.ContainsKey(group))
        {
            plugin.ReplyToCommand(controller, $"VIP group '{group}' was not found!");
            return;
        }

        var time = int.TryParse(command.GetArg(3), out var arg) && arg >= 0 ? arg : -1;

        players.UpdateVip(accountId, player, null, group, time == -1 ? null : plugin.ExpiresIn(time), notify: true);

        var groupText = group == null ? "unchanged" : $"'{group}'";
        var timeText = time == -1 ? "unchanged" : time == 0 ? "permanent" : $"{time} {plugin.TimeUnitName}";
        plugin.ReplyToCommand(controller, $"VIP updated: AccountID {accountId} -> group: {groupText}, time: {timeText}.");
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_deleteuser")]
    public void OnCmdDeleteUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount != 2)
        {
            plugin.ReplyToCommand(controller, "Usage: css_vip_deleteuser <steamid or accountid>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
        {
            plugin.ReplyToCommand(controller, $"Invalid SteamID/AccountID: {command.GetArg(1)}");
            return;
        }

        players.RemoveVip(accountId, player, notify: true);

        plugin.ReplyToCommand(controller,
            $"VIP removed: AccountID {accountId}{(player != null ? $" ({player.PlayerName})" : "")}.");
    }

    [RequiresPermissions("@css/root")]
    [CommandHelper(1, "<steamid>")]
    [ConsoleCommand("css_reload_vip_player")]
    public void OnCmdReloadPlayer(CCSPlayerController? controller, CommandInfo command)
    {
        var target = Utils.GetPlayerFromSteamId(command.GetArg(1));
        if (target == null || !players.TryGetPlayer(target, out var vipPlayer))
        {
            plugin.ReplyToCommand(controller, $"Player {command.GetArg(1)} was not found");
            return;
        }

        players.Reload(vipPlayer);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCmdReload(CCSPlayerController? controller, CommandInfo command)
    {
        plugin.ReloadConfigs();
        plugin.ReplyToCommand(controller, "Configuration reloaded successfully!");
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_help", "Lists all VIPCore admin commands.")]
    public void OnCmdHelp(CCSPlayerController? controller, CommandInfo command)
    {
        var t = plugin.TimeUnitName;
        string[] lines =
        [
            "===== VIPCore commands =====",
            $"css_vip_adduser <steamid or accountid> <group> <time_{t}>  - add a VIP (time 0 = permanent)",
            "css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>  - update a VIP (-s keeps a field)",
            "css_vip_deleteuser <steamid or accountid>  - remove a VIP",
            "css_reload_vip_player <steamid>  - reload a player's VIP data from the database",
            "css_vip_reload  - reload the VIP configuration",
            "css_vip_admin  - open the VIP admin menu",
            "css_vip_help  - show this list",
            "============================="
        ];

        foreach (var line in lines)
            plugin.ReplyToCommand(controller, line);
    }
}
