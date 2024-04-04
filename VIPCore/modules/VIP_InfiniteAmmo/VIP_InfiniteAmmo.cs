using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_InfiniteAmmo;

public class VipInfiniteAmmo : BasePlugin
{
	public override string ModuleAuthor => "panda";
	public override string ModuleName => "[VIP] Infinite Ammo";
	public override string ModuleVersion => "v1.0.0";

	private IVipCoreApi? _api;
	private InfiniteAmmo? _infiniteAmmoFeature;
	private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
	public override void OnAllPluginsLoaded(bool hotReload)
	{

		_api = PluginCapability.Get();
		if (_api == null) return;

		_infiniteAmmoFeature = new InfiniteAmmo(this, _api);
		_api.RegisterFeature(_infiniteAmmoFeature);
	}

	public override void Unload(bool hotReload)
    {
        if (_api != null && _infiniteAmmoFeature != null)
        {
            _api?.UnRegisterFeature(_infiniteAmmoFeature);
        }
    }
}

public class InfiniteAmmo : VipFeatureBase
{
	public override string Feature => "InfiniteAmmo";

	public InfiniteAmmo(VipInfiniteAmmo vipAmmo, IVipCoreApi api) : base(api)
	{
		vipAmmo.RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
		vipAmmo.RegisterEventHandler<EventWeaponReload>(OnWeaponReload);
	}

	private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
	{
		CCSPlayerController player = @event.Userid;
		ApplyInfiniteAmmo(player);
		return HookResult.Continue;
	}

	private HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
	{
		CCSPlayerController player = @event.Userid;
		ApplyInfiniteAmmo(player);
		return HookResult.Continue;
	}

	private void ApplyInfiniteAmmo(CCSPlayerController player)
	{
		if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;

		int featureValue = Api.GetFeatureValue<int>(player, Feature);

		switch (featureValue)
		{
			case 1:
				ApplyInfiniteClip(player);
				break;
			case 2:
				ApplyInfiniteReserve(player);
				break;
		}
	}

	private void ApplyInfiniteClip(CCSPlayerController player)
    {
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
        if (activeWeaponHandle?.Value != null)
        {
            activeWeaponHandle.Value.Clip1 = 100;
        }
    }

    private void ApplyInfiniteReserve(CCSPlayerController player)
    {
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
        if (activeWeaponHandle?.Value != null)
        {
            activeWeaponHandle.Value.ReserveAmmo[0] = 100;
        }
    }
}