using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_NoFallDamage;

public class VipNoFallDamage : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] No Fall Damage";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private NoFallDamage? _noFallDamage;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _noFallDamage = new NoFallDamage(_api);
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
        var entityInstance = hook.GetParam<CEntityInstance>(0);
        
        var baseEntity = new CBaseEntity(entityInstance.Handle);
        
        var player = GetPlayer(baseEntity);
        if (player is null) return HookResult.Continue;
        
        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

        if (damageInfo.BitsDamageType.HasFlag(DamageTypes_t.DMG_FALL) 
            && IsFeatureEnabled(player)
            && GetValue(player))
        {
            return HookResult.Handled;
        }
        
        return HookResult.Continue;
    }
    
    public static CCSPlayerController? GetPlayer(CBaseEntity? ent)
    {
        if (ent != null && ent.DesignerName == "player")
        {
            var pawn = new CCSPlayerPawn(ent.Handle);

            if (!pawn.IsValid)
                return null;

            if (!pawn.OriginalController.IsValid)
                return null;

            return pawn.OriginalController.Value;
        }

        return null;
    }

    public override void Dispose()
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        base.Dispose();
    }
}