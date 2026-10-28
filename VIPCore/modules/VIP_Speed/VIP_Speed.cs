using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Speed;

public class VipSpeed : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Speed";
    public override string ModuleVersion => "v2.0.0";
    private IVipCoreApi? _api;
    private SpeedModifier? _speedModifier;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _speedModifier = new SpeedModifier(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _speedModifier?.Dispose();
    }
}

public class SpeedModifier : VipFeature<float>
{
    private readonly CCSPlayerController?[] _players = new CCSPlayerController?[65];

    public SpeedModifier(VipSpeed vipSpeed, IVipCoreApi api) : base("Speed", api)
    {
        vipSpeed.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _players[slot] = null);

        // FIX: m_flVelocityModifier is recalculated by the engine every movement tick. Climbing a
        // ladder, walking on stairs, weapon speed penalties, etc. all reset it - so setting it only
        // on spawn / on player_hurt (the old approach) left the multiplier missing most of the time.
        // Reapply it every tick, exactly like VIP_Gravity / VIP_Bhop / VIP_Jumps do for their values.
        vipSpeed.RegisterListener<Listeners.OnTick>(() =>
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

                ApplySpeed(player);
            }
        });

        Server.NextWorldUpdate(() =>
        {
            foreach (var player in Utilities.GetPlayers())
                UpdatePlayer(player);
        });
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group) => UpdatePlayer(player);

    public override void OnPlayerRemoved(CCSPlayerController player, string group) => _players[player.Slot] = null;

    public override void OnPlayerDisconnect(CCSPlayerController player, bool vip) => _players[player.Slot] = null;

    private void UpdatePlayer(CCSPlayerController player)
    {
        if (player is not { IsValid: true, IsBot: false }) return;

        _players[player.Slot] = IsPlayerValid(player) ? player : null;
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        UpdatePlayer(player);

        if (!vip) return;
        if (!player.IsValid) return;
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        ApplySpeed(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        if (!player.IsValid) return;

        UpdatePlayer(player);
        
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        // When the player turns the feature off, restore the engine default so they are not left
        // stuck at the VIP speed until respawn.
        if (GetPlayerFeatureState(player) is FeatureState.Disabled or FeatureState.NoAccess)
        {
            playerPawn.VelocityModifier = 1.0f;
            Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_flVelocityModifier");
            return;
        }

        ApplySpeed(player);
    }

    private void ApplySpeed(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is null || !playerPawn.IsValid) return;

        var speedModifierValue = GetValue(player);

        // Only write when it actually differs, to avoid a needless network state change every tick.
        if (Math.Abs(playerPawn.VelocityModifier - speedModifierValue) < 0.0001f) return;

        playerPawn.VelocityModifier = speedModifierValue;
        Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_flVelocityModifier");
    }
}
