using System.Text;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIP_Flags;

public class VipFlags : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Flags";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private Flags? _flags;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();

        if (_api == null) return;

        _flags = new Flags(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _flags?.Dispose();
    }
}

public class Flags : VipFeature<List<string>>
{
    private readonly VipFlags _vipFlags;
    private readonly Dictionary<ulong, List<string>> _flags = new();

    public Flags(VipFlags vipFlags, IVipCoreApi api) : base("flags", api, FeatureType.Hide)
    {
        _vipFlags = vipFlags;
        vipFlags.RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (player is null || !player.IsValid) return HookResult.Continue;

            if (IsPlayerVip(player) && PlayerHasFeature(player))
            {
                RemovePlayerPermissions(player);
            }

            return HookResult.Continue;
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        if (!PlayerHasFeature(player)) return;

        Timer timer = null!;

        timer = _vipFlags.AddTimer(1f, () =>
        {
            if (!player.IsValid || player.Connected != PlayerConnectedState.Connected)
                return;

            if (!_flags.ContainsKey(player.SteamID))
                _flags.Add(player.SteamID, []);

            var flagsOrGroups = GetValue(player);

            var steamId = new SteamID(player.SteamID);
            foreach (var flagOrGroup in flagsOrGroups)
            {
                if (flagOrGroup.StartsWith('@'))
                    AdminManager.AddPlayerPermissions(steamId, flagOrGroup);
                else
                    AdminManager.AddPlayerToGroup(steamId, flagOrGroup);

                _flags[player.SteamID].Add(flagOrGroup);
            }

            timer.Kill();
        }, TimerFlags.REPEAT);
    }

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
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