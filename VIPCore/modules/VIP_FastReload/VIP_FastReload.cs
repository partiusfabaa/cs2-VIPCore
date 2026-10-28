using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_FastReload;

public class VIPFastReload : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[VIP] FastReload";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private FastReload? _fastReloadFeature;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _fastReloadFeature = new FastReload(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _fastReloadFeature?.Dispose();
    }

    public class FastReload : VipFeature
    {
        public FastReload(VIPFastReload vipFastReload, IVipCoreApi api) : base("FastReload", api)
        {
            vipFastReload.RegisterEventHandler<EventWeaponReload>(OnWeaponReload);
        }

        private HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            CBasePlayerWeapon? activeWeapon = player?.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
            if (player == null) return HookResult.Continue;

            ApplyFastReload(player);
            return HookResult.Continue;
        }

        private void ApplyFastReload(CCSPlayerController player)
        {
            if (!PlayerHasFeature(player)) return;
            if (GetPlayerFeatureState(player) is FeatureState.Disabled or FeatureState.NoAccess) return;

            CBasePlayerWeapon? activeWeapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
            if (activeWeapon == null) return;

            CCSWeaponBaseVData? weaponData = activeWeapon.As<CCSWeaponBase>()?.VData;
            if (weaponData == null) return;

            if (activeWeapon.Clip1 < weaponData.MaxClip1)
            {
                activeWeapon.Clip1 = weaponData.MaxClip1;
                Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_iClip1");
            }
        }
    }
}
