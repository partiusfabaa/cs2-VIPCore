using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Modularity;
using VipCoreApi;

namespace VIP_Jumps;

public class VipJumps : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Jumps";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "Jumps";
    private static readonly UserSettings?[] UserSettings = new UserSettings?[Server.MaxPlayers + 1];

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) => UserSettings[slot + 1] = new UserSettings());
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => UserSettings[slot + 1] = null);

        RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (UserSettings[player.Index] == null ||
                    player.TeamNum is not ((int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist)) continue;

                if (_api.IsClientVip(player) && _api.PlayerHasFeature(player, Feature))
                    OnTick(player);
            }
        });
    }

    private void OnTick(CCSPlayerController player)
    {
        var client = player.Index;

        if (_api.GetPlayerFeatureState(player, Feature) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn != null)
        {
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;

            if ((UserSettings[client]!.LastFlags & PlayerFlags.FL_ONGROUND) != 0 &&
                (flags & PlayerFlags.FL_ONGROUND) == 0 &&
                (UserSettings[client]!.LastButtons & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0)
            {
                //UserSettings[client]!.JumpsCount ++;
            }
            else if ((flags & PlayerFlags.FL_ONGROUND) != 0)
                UserSettings[client]!.JumpsCount = 0;
            else if ((UserSettings[client]!.LastButtons & PlayerButtons.Jump) == 0 &&
                     (buttons & PlayerButtons.Jump) != 0 &&
                     UserSettings[client]!.JumpsCount < UserSettings[client]!.NumberOfJumps)
            {
                UserSettings[client]!.JumpsCount ++;
                playerPawn.AbsVelocity.Z = 300;
            }

            UserSettings[client]!.LastFlags = flags;
            UserSettings[client]!.LastButtons = buttons;
        }
    }


    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(Feature);
    }

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
        _api.OnPlayerSpawn += OnPlayerSpawn;
    }

    private void OnPlayerSpawn(CCSPlayerController player)
    {
        if (UserSettings[player.Index] == null) return;
        if(!_api.PlayerHasFeature(player, Feature)) return;
        
        UserSettings[player.Index]!.NumberOfJumps = _api.GetFeatureValue<int>(player, Feature);
    }
}

public class UserSettings
{
    public PlayerButtons LastButtons { get; set; }
    public PlayerFlags LastFlags { get; set; }
    public int JumpsCount { get; set; }
    public int NumberOfJumps { get; set; }
}