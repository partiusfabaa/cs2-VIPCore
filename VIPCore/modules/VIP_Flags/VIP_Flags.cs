using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using Modularity;
using VipCoreApi;

namespace VIP_Flags;

public class VipFlags : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Flags";
    public override string ModuleVersion => "v1.0.0";

    private static readonly string Feature = "flags";
    private IVipCoreApi _api = null!;
    private readonly Dictionary<ulong, List<string>> _flags = new();

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, IVipCoreApi.FeatureType.Hide);
        _api.PlayerLoaded += PlayerLoaded;
        _api.PlayerRemoved += PlayerRemoved;
    }

    private void PlayerLoaded(CCSPlayerController player, string group)
    {
        if (!_api.PlayerHasFeature(player, Feature)) return;
        
        if (!_flags.ContainsKey(player.SteamID))
            _flags[player.SteamID] = new List<string>();
        
        var flagsOrGroups = _api.GetFeatureValue<List<string>>(player, Feature);

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

    private void PlayerRemoved(CCSPlayerController player, string group)
    {
        RemovePlayerPermissions(player);
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;

            if (_api.IsClientVip(player) && _api.PlayerHasFeature(player, Feature))
                RemovePlayerPermissions(player);
            
            return HookResult.Continue;
        });
    }

    private void RemovePlayerPermissions(CCSPlayerController player)
    {
        if (!_flags.ContainsKey(player.SteamID)) return;
        
        var playerFlagsOrGroups = new List<string>(_flags[player.SteamID]);

        var steamId = new SteamID(player.SteamID);
        foreach (var flagOrGroups in playerFlagsOrGroups)
        {
            if (flagOrGroups.StartsWith('@'))
                AdminManager.RemovePlayerPermissions(steamId, flagOrGroups);
            else
                AdminManager.RemovePlayerFromGroup(steamId, groups: flagOrGroups);

            _flags[player.SteamID].Remove(flagOrGroups);
        }
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}