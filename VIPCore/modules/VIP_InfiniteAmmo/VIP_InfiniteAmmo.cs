using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;
using Microsoft.Extensions.Logging;

namespace VIP_InfiniteAmmo;

public class VipInfiniteAmmo : BasePlugin
{
	public override string ModuleAuthor => "panda";
	public override string ModuleName => "[VIP] Infinite Ammo";
	public override string ModuleVersion => "v2.0.0";

	private IVipCoreApi? _api;
	private InfiniteAmmo? _infiniteAmmoFeature;
	
	public override void OnAllPluginsLoaded(bool hotReload)
	{
		_api = IVipCoreApi.Capability.Get();
		if (_api == null) return;

		_infiniteAmmoFeature = new InfiniteAmmo(this, _api);
	}

	public override void Unload(bool hotReload)
    {
        _infiniteAmmoFeature?.Dispose();
    }
}

public class InfiniteAmmo : VipFeature<Config>
{
	private Config _config;

	public InfiniteAmmo(VipInfiniteAmmo vipAmmo, IVipCoreApi api) : base("InfiniteAmmo", api)
	{
		vipAmmo.RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
		vipAmmo.RegisterEventHandler<EventWeaponReload>(OnWeaponReload);
		
		_config = new Config();
	}

	private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Userid;
		if (player == null) return HookResult.Continue;

		ApplyInfiniteAmmo(player);
		return HookResult.Continue;
	}

	private HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Userid;
		if (player == null) return HookResult.Continue;

		ApplyInfiniteAmmo(player);
		return HookResult.Continue;
	}
	
	private void ApplyInfiniteAmmo(CCSPlayerController? player)
	{
		if (player == null) return;
		
		if (!IsFeatureEnabled(player)) return;
		
		var config = GetValue(player);
		if (config == null) return;
		_config = config;

		var activeWeapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon?.Value;
		if (activeWeapon == null) return;
		
		string weaponName = activeWeapon?.DesignerName ?? string.Empty;
		if (_config.DisabledGuns.Contains(weaponName)) 
			return;

		switch (_config.Type)
		{
			case 1:
				ApplyInfiniteClip(player);
				break;
			case 2:
				ApplyInfiniteReserve(player);
				break;
			default:
				Console.WriteLine("[InfiniteAmmo] Invalid type. Only value 1 or 2 are accepted.");
				break;
		}
	}

	private void ApplyInfiniteClip(CCSPlayerController? player)
    {
		if (player == null) return;
		
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
        if (activeWeaponHandle?.Value != null)
        {
            activeWeaponHandle.Value.Clip1 = 100;
        }
    }

    private void ApplyInfiniteReserve(CCSPlayerController? player)
    {
		if (player == null) return;

        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
        if (activeWeaponHandle?.Value != null)
        {
            activeWeaponHandle.Value.ReserveAmmo[0] = 100;
        }
    }
}

public class Config
{
    public int Type { get; set; } = 1;
	public List<string> DisabledGuns { get; set; } = new List<string>();
}