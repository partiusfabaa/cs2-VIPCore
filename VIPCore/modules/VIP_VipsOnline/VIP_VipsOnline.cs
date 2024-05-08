using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;

namespace VIP_VipsOnline;

public class VIPVipsOnline : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Vips Online";
    public override string ModuleVersion => "v1.0";
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        AddCommand("css_vips", "List online VIP players", ListVipOnlinePlayers);
        AddCommand("css_vipsonline", "List online VIP players", ListVipOnlinePlayers);
    }

    private void ListVipOnlinePlayers(CCSPlayerController? player, CommandInfo info)
    {
        if (_api == null) return;

        var onlineVips = Utilities.GetPlayers().Where(p => p.IsValid && _api.IsClientVip(p)).Select(p => $"{p.PlayerName}").ToList();

        string message;
        var vipList = string.Join(", ", onlineVips);

        if (onlineVips.Count != 0)
        {
            message = ReplaceColorPlaceholders(_api.GetTranslatedText("vip.OnlineVips", vipList));
        }
        else
        {
            message = ReplaceColorPlaceholders(_api.GetTranslatedText("vip.NoVipsOnline"));
        }

        if (player != null)
            _api.PrintToChat(player, message);
        else
        {
            if(onlineVips.Count != 0)
                Console.WriteLine($"VIP players online: {vipList}.");
            else
                Console.WriteLine($"No VIP players online.");
        }
    }

    private readonly Dictionary<string, char> _colorMap = new()
    {
        { "[default]", ChatColors.Default },
        { "[white]", ChatColors.White },
        { "[darkred]", ChatColors.DarkRed },
        { "[green]", ChatColors.Green },
        { "[lightyellow]", ChatColors.LightYellow },
        { "[lightblue]", ChatColors.LightBlue },
        { "[olive]", ChatColors.Olive },
        { "[lime]", ChatColors.Lime },
        { "[red]", ChatColors.Red },
        { "[lightpurple]", ChatColors.LightPurple },
        { "[purple]", ChatColors.Purple },
        { "[grey]", ChatColors.Grey },
        { "[yellow]", ChatColors.Yellow },
        { "[gold]", ChatColors.Gold },
        { "[silver]", ChatColors.Silver },
        { "[blue]", ChatColors.Blue },
        { "[darkblue]", ChatColors.DarkBlue },
        { "[bluegrey]", ChatColors.BlueGrey },
        { "[magenta]", ChatColors.Magenta },
        { "[lightred]", ChatColors.LightRed },
        { "[orange]", ChatColors.Orange }
    };

    private string ReplaceColorPlaceholders(string message)
    {
        foreach (var colorPlaceholder in _colorMap)
        {
            message = message.Replace(colorPlaceholder.Key, colorPlaceholder.Value.ToString());
        }
        return message;
    }
}