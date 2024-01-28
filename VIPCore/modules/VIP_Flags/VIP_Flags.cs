using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using Modularity;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Flags;

public class VipFlags : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Flags";
    public override string ModuleVersion => "v1.0.0";
    
    private IVipCoreApi _api = null!;
    private Flags _flags;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _flags = new Flags(this, _api);
        _api.RegisterFeature(_flags, FeatureType.Hide);
    }
    
    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_flags);
    }
}

public class Flags : VipFeatureBase
{
    public override string Feature => "flags";
    private readonly Dictionary<ulong, List<string>> _flags = new();
    
    public Flags(VipFlags vipFlags, IVipCoreApi api) : base(api)
    {
        vipFlags.RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (IsClientVip(player) && PlayerHasFeature(player))
                RemovePlayerPermissions(player);
            
            return HookResult.Continue;
        });
    }

    public override void OnPlayerLoaded(CCSPlayerController player, string group)
    {
        if (!PlayerHasFeature(player)) return;
        
        if (!_flags.ContainsKey(player.SteamID))
            _flags.Add(player.SteamID, new List<string>());
        
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