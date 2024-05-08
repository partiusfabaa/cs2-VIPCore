using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;

namespace VIP_DamageChange;

public class VipDamageChange : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Damage Multiplier";
    public override string ModuleVersion => "v1.0";
    private IVipCoreApi? _api;
    private DamageMultiplier? _damageMultiplier;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _damageMultiplier = new DamageMultiplier(_api);
        _api.RegisterFeature(_damageMultiplier);
    }

    public override void Unload(bool hotReload)
    {
        if(_api != null && _damageMultiplier != null)
        {
            _api?.UnRegisterFeature(_damageMultiplier);
        }
    }
}

public class DamageMultiplier : VipFeatureBase
{
    public override string Feature => "DamageMultiplier";
    public DamageMultiplier(IVipCoreApi api) : base(api)
    {
    }

    public HookResult OnTakeDamage(DynamicHook hook)
    {   
        CTakeDamageInfo damageInfo = hook.GetParam<CTakeDamageInfo>(1);

         CCSPlayerController player = new CCSPlayerController(damageInfo.Attacker.Value.Handle);

        if (!PlayerHasFeature(player)) return HookResult.Continue;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return HookResult.Continue;

        if (damageInfo.Attacker.Value is null)
            return HookResult.Continue;

        if (player.IsValid && IsClientVip(player))
        {
            CCSWeaponBase? ccsWeaponBase = damageInfo.Ability.Value?.As<CCSWeaponBase>();

            if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
            {
                CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;

                if (weaponData == null)
                    return HookResult.Continue;

                if (weaponData.GearSlot != gear_slot_t.GEAR_SLOT_RIFLE && weaponData.GearSlot != gear_slot_t.GEAR_SLOT_PISTOL)
                    return HookResult.Continue;

                float damageModifierValue = GetFeatureValue<float>(player);
                damageInfo.Damage *= damageModifierValue;
            }
        }
        return HookResult.Continue;
    }
}