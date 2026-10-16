using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Speed;

public class VipSpeed : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Speed";
    public override string ModuleVersion => "v1.2";
    private IVipCoreApi? _api;
    private SpeedModifier? _speedModifier;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _speedModifier = new SpeedModifier(this, _api);
        _api.RegisterFeature(_speedModifier);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _speedModifier != null)
        {
            _api?.UnRegisterFeature(_speedModifier);
        }
    }
}

public class SpeedModifier : VipFeatureBase
{
    public override string Feature => "Speed";

    public SpeedModifier(VipSpeed vipSpeed, IVipCoreApi api) : base(api)
    {
        // FIX: m_flVelocityModifier is recalculated by the engine every movement tick. Climbing a
        // ladder, walking on stairs, weapon speed penalties, etc. all reset it - so setting it only
        // on spawn / on player_hurt (the old approach) left the multiplier missing most of the time.
        // Reapply it every tick, exactly like VIP_Gravity / VIP_Bhop / VIP_Jumps do for their values.
        vipSpeed.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(u => u.IsValid &&
                                     IsClientVip(u) &&
                                     PlayerHasFeature(u) &&
                                     GetPlayerFeatureState(u) is FeatureState.Enabled))
            {
                ApplySpeed(player);
            }
        });
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!player.IsValid) return;
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        ApplySpeed(player);
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        if (!player.IsValid) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        // When the player turns the feature off, restore the engine default so they are not left
        // stuck at the VIP speed until respawn.
        if (state is FeatureState.Disabled or FeatureState.NoAccess)
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

        var speedModifierValue = GetFeatureValue<float>(player);

        // Only write when it actually differs, to avoid a needless network state change every tick.
        if (Math.Abs(playerPawn.VelocityModifier - speedModifierValue) < 0.0001f) return;

        playerPawn.VelocityModifier = speedModifierValue;
        Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
    }
}
