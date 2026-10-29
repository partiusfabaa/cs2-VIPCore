using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;
using System.Globalization;

namespace VIP_DamageChange;

public class VipDamageChange : BasePlugin
{
    public override string ModuleAuthor => "panda.";
    public override string ModuleName => "[VIP] Damage Multiplier";
    public override string ModuleVersion => "v2.0.0";
    private IVipCoreApi? _api;
    private DamageMultiplier? _damageMultiplier;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _damageMultiplier = new DamageMultiplier(_api);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _damageMultiplier != null)
        {
            _damageMultiplier.Dispose();
        }
    }
}

public class DamageMultiplier : VipFeature<float>
{
    public DamageMultiplier(IVipCoreApi api) : base("DamageMultiplier", api)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
    }

    public HookResult OnTakeDamage(DynamicHook hook)
    {
        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

        var attacker = damageInfo.Attacker.Value;
        if (attacker is null || attacker.DesignerName != "player")
            return HookResult.Continue;

        var pawnController = new CCSPlayerPawn(attacker.Handle).Controller.Value;
        if (pawnController is null)
            return HookResult.Continue;

        var player = new CCSPlayerController(pawnController.Handle);

        if (player.IsValid)
        {
            if (!IsFeatureEnabled(player))
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

                var damageModifierValue = GetValue(player);
                
                if (damageModifierValue < 1.0f)
                    return HookResult.Continue;
                
                damageInfo.Damage *= damageModifierValue;
                return HookResult.Changed;
            }
        }
        
        return HookResult.Continue;
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
            args.Display = $"[x{GetValue(args.Controller).ToString("0.##", CultureInfo.InvariantCulture)}]";
    }

    public override void Dispose()
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        base.Dispose();
    }
}