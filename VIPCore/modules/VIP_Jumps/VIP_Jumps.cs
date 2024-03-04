using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;

namespace VIP_Jumps;

public class VipJumps : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Jumps";
    public override string ModuleVersion => "v1.0.1";

    private IVipCoreApi? _api;
    private Jumps _jumps;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _jumps = new Jumps(this, _api);
            _api.RegisterFeature(_jumps);
        };
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_jumps);
    }
}

public class Jumps : VipFeatureBase
{
    public override string Feature => "Jumps";
    private static readonly UserSettings?[] UserSettings = new UserSettings?[65];

    public Jumps(VipJumps vipJumps, IVipCoreApi api) : base(api)
    {
        vipJumps.RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
            UserSettings[slot + 1] = new UserSettings());
        vipJumps.RegisterListener<Listeners.OnClientDisconnectPost>(slot => UserSettings[slot + 1] = null);
        vipJumps.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (UserSettings[player.Index] == null ||
                    player.TeamNum is not ((int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist)) continue;

                if (IsClientVip(player) && PlayerHasFeature(player) &&
                    GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Enabled)
                    OnTick(player);
            }
        });
    }

    private void OnTick(CCSPlayerController player)
    {
        var client = player.Index;
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

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (UserSettings[player.Index] == null) return;
        if (!PlayerHasFeature(player)) return;

        UserSettings[player.Index]!.NumberOfJumps = GetFeatureValue<int>(player);
    }
}

public class UserSettings
{
    public PlayerButtons LastButtons { get; set; }
    public PlayerFlags LastFlags { get; set; }
    public int JumpsCount { get; set; }
    public int NumberOfJumps { get; set; }
}