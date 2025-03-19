using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using FabiusTimer.Configs;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;

namespace VIPCore.Services;

public class CommandsService(
    Plugin plugin,
    ILogger<Plugin> logger,
    VipCoreApi api,
    PlayersManager playersManager,
    Config<VipConfig> vipConfig,
    Config<GroupsConfig> groupsConfig)
{
    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_adduser")]
    public void OnCmdAddUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount is > 4 or < 4)
        {
            plugin.ReplyToCommand(controller,
                $"Usage: css_vip_adduser <steamid or accountid> <group> <time_{plugin.TimeUnitName}>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
            return;

        var vipGroup = command.GetArg(2);
        var endVipTime = Convert.ToInt32(command.GetArg(3));

        if (!groupsConfig.Value.ContainsKey(vipGroup))
        {
            plugin.ReplyToCommand(controller, "This VIP group was not found!");
            return;
        }

        var username = player == null ? "unknown" : player.PlayerName;

        playersManager.AddUser(player, plugin.CreateNewUser(accountId, username, vipGroup, endVipTime));
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_deleteuser")]
    public void OnCmdDeleteVipUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount is < 2 or > 2)
        {
            plugin.ReplyToCommand(controller, "Using: css_vip_deleteuser <steamid or accountid>");
            return;
        }

        var accountId = Utils.GetAccountIdFromCommand(command.GetArg(1), out var player);
        if (accountId == -1)
            return;

        playersManager.RemoveUser(player, accountId);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReload(CCSPlayerController? player, CommandInfo info)
    {
        vipConfig.Load();
        groupsConfig.Load();

        const string text = "Configs successfully reloaded";
        if (player is null)
            logger.LogInformation("[{0}] " + text, "VIP");
        else
            playersManager.PrintToChat(player, text);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_reload_modules")]
    public void OnCommandReloadModules(CCSPlayerController? player, CommandInfo info)
    {
        // api.InvokeOnCoreReady();
        //
        // const string text = "Modules successfully reloaded";
        // if (player is null)
        //     logger.LogInformation("[{0}] " + text, "VIP");
        // else
        //     playersManager.PrintToChat(player, text);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_vip_update_users")]
    public void OnCommandUpdateUsers(CCSPlayerController? player, CommandInfo info)
    {
        playersManager.UpdatePlayers();
    }
    
    [RequiresPermissions("@css/root")]
    [CommandHelper(1, "<steamid>")]
    [ConsoleCommand("css_vip_update_user")]
    public void OnCommandUpdateUser(CCSPlayerController? player, CommandInfo info)
    {
        var target = Utils.GetPlayerFromSteamId(info.GetArg(1));
        if (target is null) return;
        
        playersManager.UpdatePlayer(target);
    }
}