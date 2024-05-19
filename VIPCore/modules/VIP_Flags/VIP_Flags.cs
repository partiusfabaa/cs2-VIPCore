using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIP_Flags;

public class VipFlags : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Flags";
    public override string ModuleVersion => "v1.0.0";
    
    private IVipCoreApi? _api;
    private Flags _flags;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();

        if (_api == null) return;

        _flags = new Flags(this, _api);
        _api.RegisterFeature(_flags, FeatureType.Hide);
    }
    
    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_flags);
    }
}

public class Flags : VipFeatureBase
{
    private readonly VipFlags _vipFlags;
    public override string Feature => "flags";
    private readonly Dictionary<ulong, List<string>> _flags = new();
    private bool[] _clientDisconnected = new bool[70];
    
    public Flags(VipFlags vipFlags, IVipCoreApi api) : base(api)
    {
        _vipFlags = vipFlags;
        vipFlags.RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (player is null || !player.IsValid) return HookResult.Continue;
            
            if (IsClientVip(player) && PlayerHasFeature(player))
            {
                _clientDisconnected[player.Slot] = true;
                RemovePlayerPermissions(player);
            }
            
            return HookResult.Continue;
        });
        
        vipFlags.AddCommand("css_testflag", "", (player, info) =>
        {
            if (player is null) return;
            
            player.PrintToChat($"{AdminManager.PlayerHasPermissions(player, "@css/viptestflag")}");
        });
    }

    public override void OnPlayerLoaded(CCSPlayerController player, string group)
    {
        if (!PlayerHasFeature(player)) return;
        
        Timer timer = null!;

        timer = _vipFlags.AddTimer(1f, () =>
        {
            if (_clientDisconnected[player.Slot])
            {
                timer.Kill();
                return;
            }

            if (player.Connected != PlayerConnectedState.PlayerConnected)
                return;

            if (!_flags.ContainsKey(player.SteamID))
                _flags.Add(player.SteamID, []);
        
            var flagsOrGroups = GetFeatureValue<List<string>>(player);

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
        
        var playerFlagsOrGroups = new List<string>(value);

        var steamId = new SteamID(player.SteamID);
        foreach (var flagOrGroups in playerFlagsOrGroups)
        {
            if (flagOrGroups.StartsWith('@'))
                AdminManager.RemovePlayerPermissions(steamId, flagOrGroups);
            else
                AdminManager.RemovePlayerFromGroup(steamId, groups: flagOrGroups);
            value.Remove(flagOrGroups);
        }
    }
}

