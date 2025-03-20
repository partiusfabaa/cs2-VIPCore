using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;

namespace VIP_NoFallDamage;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] No Fall Damage";
    public override string ModuleVersion => "v2.0.0";

    private NoFallDamage? _noFallDamage;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _noFallDamage = new NoFallDamage(api);
    }

    public override void Unload(bool hotReload)
    {
        _noFallDamage?.Dispose();
    }
}

public class NoFallDamage : VipFeature<bool>
{
    public NoFallDamage(IVipCoreApi api) : base("NoFallDamage", api)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
    }

    private HookResult OnTakeDamage(DynamicHook hook)
    {
        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);
        if (damageInfo.BitsDamageType != DamageTypes_t.DMG_FALL) return HookResult.Continue;

        var entity = hook.GetParam<CBaseEntity>(0);
        if (!entity.DesignerName.Contains("player")) return HookResult.Continue;

        var player = entity.As<CCSPlayerPawn>().OriginalController.Value;
        if (player is null || !IsPlayerValid(player)) return HookResult.Continue;

        return HookResult.Handled;
    }

    public override void Dispose()
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        base.Dispose();
    }
}