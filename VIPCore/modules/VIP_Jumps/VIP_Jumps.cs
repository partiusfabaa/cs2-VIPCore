using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;

namespace VIP_Jumps;

public class VipJumps : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius,GSM-RO";
    public override string ModuleName => "[VIP] Jumps";
    public override string ModuleVersion => "v1.0.2";

    private IVipCoreApi? _api;
    private Jumps? _jumps;

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
        if(_jumps != null)
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
        var settings = UserSettings[client]!;

        if (playerPawn != null)
        {
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;

            if ((settings.LastFlags & PlayerFlags.FL_ONGROUND) != 0 &&
                (flags & PlayerFlags.FL_ONGROUND) == 0 &&
                (settings.LastButtons & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0)
            {
                // cod comentat original
            }
            else if ((flags & PlayerFlags.FL_ONGROUND) != 0)
            {
                settings.JumpsCount = 0;
            }
            else if ((settings.LastButtons & PlayerButtons.Jump) == 0 &&
                (buttons & PlayerButtons.Jump) != 0 &&
                settings.JumpsCount < settings.NumberOfJumps &&
                (settings.JumpLimitPerRound == 0 || settings.JumpsUsedThisRound < settings.JumpLimitPerRound))
            {
                settings.JumpsCount++;
                settings.JumpsUsedThisRound++;
                playerPawn.AbsVelocity.Z = 300;
            }


            settings.LastFlags = flags;
            settings.LastButtons = buttons;
        }
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (UserSettings[player.Index] == null) return;
        if (!PlayerHasFeature(player)) return;

        var settings = UserSettings[player.Index]!;

		var feature = GetFeatureValue<JumpsFeature>(player);
		
        settings.NumberOfJumps = feature.Jumps;
        settings.JumpLimitPerRound = feature.LimitPerRound;
		
        settings.JumpsUsedThisRound = 0;
    }
}

public class JumpsFeature
{
	public int Jumps { get; set; }
	public int LimitPerRound { get; set; }
}

public class UserSettings
{
    public PlayerButtons LastButtons { get; set; }
    public PlayerFlags LastFlags { get; set; }
    public int JumpsCount { get; set; }
    public int NumberOfJumps { get; set; }

    public int JumpsUsedThisRound { get; set; } = 0;
    public int JumpLimitPerRound { get; set; } = 5; // default
}