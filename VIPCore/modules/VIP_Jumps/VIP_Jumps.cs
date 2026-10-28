using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Jumps;

public class VipJumps : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius,GSM-RO";
    public override string ModuleName => "[VIP] Jumps";
    public override string ModuleVersion => "v2.0.0";

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
        };
    }

    public override void Unload(bool hotReload)
    {
        _jumps?.Dispose();
    }
}

public class Jumps : VipFeature
{
    private static readonly UserSettings?[] UserSettings = new UserSettings?[65];
    private readonly CCSPlayerController?[] _players = new CCSPlayerController?[65];

    public Jumps(VipJumps vipJumps, IVipCoreApi api) : base("Jumps", api)
    {
        vipJumps.RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
            UserSettings[slot + 1] = new UserSettings());
        vipJumps.RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            UserSettings[slot + 1] = null;
            _players[slot] = null;
        });
        vipJumps.RegisterListener<Listeners.OnTick>(() =>
        {
            for (var slot = 0; slot < _players.Length; slot++)
            {
                var player = _players[slot];
                if (player is null) continue;

                if (!player.IsValid)
                {
                    _players[slot] = null;
                    continue;
                }

                if (!IsFeatureEnabled(player))
                {
                    _players[slot] = null;
                    continue;
                }

                if (!player.PawnIsAlive || UserSettings[player.Index] == null ||
                    player.TeamNum is not ((int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist)) continue;

                OnTick(player);
            }
        });

        Server.NextWorldUpdate(() =>
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
            {
                UserSettings[player.Index] ??= new UserSettings();
                UpdatePlayer(player);
            }
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group) => _players[player.Slot] = null;

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip) => _players[player.Slot] = null;

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (player is not { IsValid: true, IsBot: false }) return;

        _players[player.Slot] = IsFeatureEnabled(player) ? player : null;
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

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        UpdatePlayer(player);

        if (!vip) return;
        if (UserSettings[player.Index] == null) return;
        if (!PlayerHasFeature(player)) return;

        var settings = UserSettings[player.Index]!;

        var feature = ReadJumpsFeature(player);

        settings.NumberOfJumps = feature.Jumps;
        settings.JumpLimitPerRound = feature.LimitPerRound;

        settings.JumpsUsedThisRound = 0;
    }

    /// <summary>
    /// Reads the "Jumps" feature value tolerantly so both config formats work:
    ///   new object form:  "Jumps": { "Jumps": 2, "LimitPerRound": 5 }
    ///   legacy number:    "Jumps": 2   (interpreted as number of jumps, default limit)
    /// This avoids the JsonException that crashed OnPlayerSpawn when the config used a plain number.
    /// </summary>
    private JumpsFeature ReadJumpsFeature(CCSPlayerController player)
    {
        var result = new JumpsFeature { Jumps = 2, LimitPerRound = 0 };

        try
        {
            var element = GetFeatureValue<JsonElement>(player);

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty("Jumps", out var jumpsProp) &&
                        jumpsProp.ValueKind == JsonValueKind.Number)
                        result.Jumps = jumpsProp.GetInt32();

                    if (element.TryGetProperty("LimitPerRound", out var limitProp) &&
                        limitProp.ValueKind == JsonValueKind.Number)
                        result.LimitPerRound = limitProp.GetInt32();
                    break;

                case JsonValueKind.Number:
                    // Legacy config: a bare number means "number of extra jumps".
                    result.Jumps = element.GetInt32();
                    break;
            }
        }
        catch
        {
            // Fall back to the defaults above rather than crashing the spawn handler.
        }

        return result;
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