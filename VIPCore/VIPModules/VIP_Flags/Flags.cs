using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Flags;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Flags";
    public override string ModuleVersion => "v2.0.0";

    private Flags? _flags;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _flags = new Flags(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _flags?.Dispose();
    }
}

public class Flags : VipFeature<List<string>>
{
    private readonly Dictionary<ulong, List<string>> _flags = new();

    public Flags(Plugin plugin, IVipCoreApi api) : base("Flags", api, FeatureType.Hide)
    {
        plugin.AddCommand("css_testflags", "", (player, info) =>
        {
            Server.PrintToChatAll($"@vip/test = {AdminManager.PlayerHasPermissions(player, "@vip/test")}");
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        if (!PlayerHasFeature(player)) return;

        if (!_flags.ContainsKey(player.SteamID))
            _flags.Add(player.SteamID, []);

        var flagsOrGroups = GetValue(player);
        if (flagsOrGroups is null || flagsOrGroups.Count == 0) return;

        var steamId = new SteamID(player.SteamID);
        foreach (var flagOrGroup in flagsOrGroups)
        {
            if (flagOrGroup.StartsWith('@'))
                AdminManager.AddPlayerPermissions(steamId, flagOrGroup);
            else
                AdminManager.AddPlayerToGroup(steamId, flagOrGroup);

            _flags[player.SteamID].Add(flagOrGroup);
        }
    }

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip)
    {
        RemovePlayerPermissions(player);
    }

    private void RemovePlayerPermissions(CCSPlayerController player)
    {
        if (!_flags.TryGetValue(player.SteamID, out var value)) return;

        var steamId = new SteamID(player.SteamID);
        foreach (var flagOrGroups in value.ToList())
        {
            if (flagOrGroups.StartsWith('@'))
                AdminManager.RemovePlayerPermissions(steamId, flagOrGroups);
            else
                AdminManager.RemovePlayerFromGroup(steamId, groups: flagOrGroups);
            value.Remove(flagOrGroups);
        }
    }
}