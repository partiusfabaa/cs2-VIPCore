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

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
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
    private readonly Timer?[] _timers = new Timer?[64];

    public Flags(VipFlags vipFlags, IVipCoreApi api) : base("flags", api, FeatureType.Hide)
    {
        _vipFlags = vipFlags;
        vipFlags.RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (player is null || !player.IsValid) return HookResult.Continue;

            KillTimer(player.Slot);
            RemovePlayerPermissions(player);

            return HookResult.Continue;
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        KillTimer(player.Slot);
        RemovePlayerPermissions(player);

        if (!PlayerHasFeature(player)) return;

        var slot = player.Slot;

        _timers[slot] = _vipFlags.AddTimer(1f, () =>
        {
            if (!player.IsValid || player.Connected is PlayerConnectedState.Disconnecting
                    or PlayerConnectedState.Disconnected)
            {
                KillTimer(slot);
                return;
            }

            if (player.Connected != PlayerConnectedState.Connected)
                return;

            KillTimer(slot);

            var flagsOrGroups = GetValue(player);
            if (flagsOrGroups is null) return;

            if (!_flags.ContainsKey(player.SteamID))
                _flags.Add(player.SteamID, []);

            var steamId = new SteamID(player.SteamID);
            foreach (var flagOrGroup in flagsOrGroups)
            {
                if (flagOrGroup.StartsWith('@'))
                    AdminManager.AddPlayerPermissions(steamId, flagOrGroup);
                else
                    AdminManager.AddPlayerToGroup(steamId, flagOrGroup);

                _flags[player.SteamID].Add(flagOrGroup);
            }
        }, TimerFlags.REPEAT);
    }

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        KillTimer(player.Slot);
        RemovePlayerPermissions(player);
    }

    private void KillTimer(int slot)
    {
        _timers[slot]?.Kill();
        _timers[slot] = null;
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