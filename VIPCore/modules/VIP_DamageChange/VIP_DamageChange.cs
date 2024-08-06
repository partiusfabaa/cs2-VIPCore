using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_DamageChange;

public class VipDamageChange : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Damage Multiplier";
    public override string ModuleVersion => "v1.1";
    private IVipCoreApi? _api;
    private DamageMultiplier? _damageMultiplier;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _damageMultiplier = new DamageMultiplier(_api);
        _api.RegisterFeature(_damageMultiplier);

        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(_damageMultiplier.OnTakeDamage, HookMode.Pre);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _damageMultiplier != null)
        {
            _api?.UnRegisterFeature(_damageMultiplier);
            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(_damageMultiplier.OnTakeDamage, HookMode.Pre);
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
        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

        var attacker = damageInfo.Attacker.Value;
        if (attacker is null)
            return HookResult.Continue;

        var pawnController = new CCSPlayerPawn(attacker.Handle).Controller.Value;
        if (pawnController is null)
            return HookResult.Continue;

        var player = new CCSPlayerController(pawnController.Handle);

        if (player.IsValid)
        {
            if (!PlayerHasFeature(player) || GetPlayerFeatureState(player) is not FeatureState.Enabled)
                return HookResult.Continue;

            var weaponBase = damageInfo.Ability.Value?.As<CCSWeaponBase>();

            if (weaponBase != null && weaponBase.IsValid)
            {
                var weaponData = weaponBase.VData;

                if (weaponData == null)
                    return HookResult.Continue;

                if (weaponData.GearSlot != gear_slot_t.GEAR_SLOT_RIFLE &&
                    weaponData.GearSlot != gear_slot_t.GEAR_SLOT_PISTOL)
                    return HookResult.Continue;

                var damageModifierValue = GetFeatureValue<float>(player);

                if (damageModifierValue < 1.0f)
                    return HookResult.Continue;

                damageInfo.Damage *= damageModifierValue;
                return HookResult.Changed;
            }
        }
        
        return HookResult.Continue;
    }
}