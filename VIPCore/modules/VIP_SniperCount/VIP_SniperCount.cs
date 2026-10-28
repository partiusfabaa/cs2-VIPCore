using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using VipCoreApi;

namespace VIP_SniperCount;

/// <summary>
/// VIP_SniperCount — a VIPCore companion plugin.
///
/// At the end of every freeze time it counts how many sniper rifles (AWP, SSG 08, SCAR-20, G3SG1)
/// each team currently holds, and prints a two-line summary (CT line + T line) in chat to every
/// VIP player. Non-VIP players see nothing. There is no toggle and no VIP menu entry: the summary
/// is sent automatically to anyone VIPCore reports as a VIP.
/// </summary>
public class VIP_SniperCount : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Sniper Count";
    public override string ModuleVersion => "v2.0.0";
    public override string ModuleDescription => "Shows per-team sniper counts to VIPs at round start.";

    private IVipCoreApi? _api;
    private readonly PluginCapability<IVipCoreApi> _vipCapability = new("vipcore:core");

    // The four sniper rifles, by their entity designer names, mapped to the label shown in chat.
    private static readonly (string Designer, string Label)[] Snipers =
    [
        ("weapon_awp",    "AWP"),
        ("weapon_ssg08",  "SSG"),
        ("weapon_scar20", "SCAR"),
        ("weapon_g3sg1",  "G3SG1"),
    ];

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _vipCapability.Get();
        if (_api == null)
        {
            Logger.LogWarning("[VIP_SniperCount] VIPCore (vipcore:core) not found - the plugin will do nothing. Install VIPCore + the shared VipCoreApi.dll.");
            return;
        }

        _api.OnCoreReady += () =>
        {
            // Freeze time just ended: players have had the buy phase, so snipers are in hand now.
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        };
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
    {
        if (_api == null)
            return HookResult.Continue;

        var players = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false })
            .ToList();

        // Count snipers per team. Index by sniper for each team.
        var ctCounts = new int[Snipers.Length];
        var tCounts = new int[Snipers.Length];

        foreach (var player in players)
        {
            if (player.Team != CsTeam.CounterTerrorist && player.Team != CsTeam.Terrorist)
                continue;

            var target = player.Team == CsTeam.CounterTerrorist ? ctCounts : tCounts;
            CountSnipers(player, target);
        }

        string ctLine = BuildLine("CT", ctCounts);
        string tLine = BuildLine("T", tCounts);

        // Send only to VIPs.
        foreach (var player in players)
        {
            if (!_api.IsPlayerVip(player))
                continue;

            player.PrintToChat(ctLine);
            player.PrintToChat(tLine);
        }

        return HookResult.Continue;
    }

    /// <summary>Adds this player's sniper rifles into the per-sniper count array.</summary>
    private static void CountSnipers(CCSPlayerController player, int[] counts)
    {
        var pawn = player.PlayerPawn.Value;
        var weaponServices = pawn?.WeaponServices;
        if (weaponServices == null)
            return;

        foreach (var weaponHandle in weaponServices.MyWeapons)
        {
            var weapon = weaponHandle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            string designer = weapon.DesignerName;
            for (int i = 0; i < Snipers.Length; i++)
            {
                if (designer == Snipers[i].Designer)
                {
                    counts[i]++;
                    break;
                }
            }
        }
    }

    /// <summary>Builds a line like: "{teamcolor}CT: {default}AWP x1 | SSG x0 | SCAR x1 | G3SG1 x0".</summary>
    private static string BuildLine(string team, int[] counts)
    {
        var parts = new string[Snipers.Length];
        for (int i = 0; i < Snipers.Length; i++)
            parts[i] = $"{Snipers[i].Label} x{counts[i]}";

        char teamColor = team == "CT" ? ChatColors.Blue : ChatColors.Gold;
        return $" {teamColor}{team}: {ChatColors.Default}{string.Join(" | ", parts)}";
    }
}
