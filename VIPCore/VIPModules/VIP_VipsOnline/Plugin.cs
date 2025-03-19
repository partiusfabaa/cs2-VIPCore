using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using VipCoreApi;

namespace VIP_VipsOnline;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Vips Online";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        AddCommand("css_vips", "List online VIP players", ListVipOnlinePlayers);
        AddCommand("css_vipsonline", "List online VIP players", ListVipOnlinePlayers);
    }

    private void ListVipOnlinePlayers(CCSPlayerController? player, CommandInfo info)
    {
        if (_api == null) return;

        var onlineVips = Utilities.GetPlayers().Where(p => p.IsValid && _api.IsPlayerVip(p))
            .Select(p => $"{p.PlayerName}").ToList();

        var vipList = string.Join(", ", onlineVips);

        var message = onlineVips.Count != 0
            ? string.Format(Localizer.ForPlayer(player, "vip.OnlineVips"), vipList).ReplaceColorTags()
            : string.Format(Localizer.ForPlayer(player, "vip.NoVipsOnline")).ReplaceColorTags();

        if (player != null)
            _api.PrintToChat(player, message);
        else
            Console.WriteLine(onlineVips.Count != 0
                ? $"VIP players online: {vipList}."
                : $"No VIP players online.");
    }
}