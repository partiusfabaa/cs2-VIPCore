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
    public SpeedModifier(VipSpeed vipSpeed, IVipCoreApi api) : base("Speed", api)
    {
        // FIX: m_flVelocityModifier is recalculated by the engine every movement tick. Climbing a
        // ladder, walking on stairs, weapon speed penalties, etc. all reset it - so setting it only
        // on spawn / on player_hurt (the old approach) left the multiplier missing most of the time.
        // Reapply it every tick, exactly like VIP_Gravity / VIP_Bhop / VIP_Jumps do for their values.
        vipSpeed.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(u => u.IsValid &&
                                     IsPlayerVip(u) &&
                                     PlayerHasFeature(u) &&
                                     GetPlayerFeatureState(u) is FeatureState.Enabled))
            {
                ApplySpeed(player);
            }
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip) return;
        if (!player.IsValid) return;
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        ApplySpeed(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        if (!player.IsValid) return;
        
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        // When the player turns the feature off, restore the engine default so they are not left
        // stuck at the VIP speed until respawn.
        if (GetPlayerFeatureState(player) is FeatureState.Disabled or FeatureState.NoAccess)
        {
            playerPawn.VelocityModifier = 1.0f;
            Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
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
        Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
    }
}
