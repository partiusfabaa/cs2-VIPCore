using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_FastReload;

public class VIPFastReload : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[VIP] FastReload";
    public override string ModuleVersion => "1.0.0";

    private IVipCoreApi? _api;
    private FastReload? _fastReloadFeature;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _fastReloadFeature = new FastReload(this, _api);
        _api.RegisterFeature(_fastReloadFeature);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _fastReloadFeature != null)
        {
            _api?.UnRegisterFeature(_fastReloadFeature);
        }
    }

    public class FastReload : VipFeatureBase
    {
        public override string Feature => "FastReload";

        public FastReload(VIPFastReload vipFastReload, IVipCoreApi api) : base(api)
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
            if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled or IVipCoreApi.FeatureState.NoAccess) return;

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
