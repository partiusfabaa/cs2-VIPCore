using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Jumps;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Jumps";
    public override string ModuleVersion => "v2.0.0";

    private Jumps? _jumps;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _jumps = new Jumps(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _jumps?.Dispose();
    }
}

public class Jumps : VipFeature<int>
{
    private static readonly UserSettings?[] UserSettings = new UserSettings?[65];

    public Jumps(Plugin plugin, IVipCoreApi api) : base("Jumps", api)
    {
        plugin.RegisterListener<Listeners.OnClientAuthorized>((slot, id) => UserSettings[slot] = new UserSettings());
        plugin.RegisterListener<Listeners.OnClientDisconnectPost>(slot => UserSettings[slot] = null);

        plugin.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (UserSettings[player.Slot] == null ||
                    player.TeamNum is not ((int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist)) continue;

                if (IsPlayerValid(player))
                    OnTick(player);
            }
        });
    }
    
    private void OnTick(CCSPlayerController player)
    {
        var client = player.Slot;
        var playerPawn = player.PlayerPawn.Value;

        if (playerPawn != null)
        {
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;
            var user = UserSettings[client]!;

            if ((flags & PlayerFlags.FL_ONGROUND) != 0)
                user.JumpsCount = 0;
            else if ((user.LastButtons & PlayerButtons.Jump) == 0 &&
                     (buttons & PlayerButtons.Jump) != 0 &&
                     user.JumpsCount < user.NumberOfJumps)
            {
                user.JumpsCount++;
                playerPawn.AbsVelocity.Z = 300;
            }

            user.LastFlags = flags;
            user.LastButtons = buttons;
        }
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!IsPlayerValid(player)) return;

        UserSettings[player.Slot]!.NumberOfJumps = GetFeatureValue<int>(player);
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
        {
            args.Display = $"[{GetValue(args.Controller)}]";
        }
    }
}

public class UserSettings
{
    public PlayerButtons LastButtons { get; set; }
    public PlayerFlags LastFlags { get; set; }
    public int JumpsCount { get; set; }
    public int NumberOfJumps { get; set; }
}