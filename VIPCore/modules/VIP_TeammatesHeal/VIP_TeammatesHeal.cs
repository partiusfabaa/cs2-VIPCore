using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;

namespace VIP_TeammatesHeal;

public class VipTeammatesHeal : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Teammates Heal";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi? _api;
    private TeammatesHeal? _flags;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();

        if (_api == null) return;

        _flags = new TeammatesHeal(_api);
        _api.RegisterFeature(_flags);
    }

    public override void Unload(bool hotReload)
    {
        if (_flags is null) return;

        _flags.Dispose();
        _api?.UnRegisterFeature(_flags);
    }
}

public class Config
{
    public List<string> WeaponBlacklist { get; set; } = ["weapon_hegrenade", "weapon_molotov"];
    public int MaxHealth { get; set; } = 100;
    public int HealPerShot { get; set; } = 25;
}

public class TeammatesHeal : VipFeatureBase, IDisposable
{
    public override string Feature => "TeammatesHeal";

    private readonly Config _config;
    private readonly float[] _healPercentages = new float[70];

    public TeammatesHeal(IVipCoreApi api) : base(api)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
        _config = LoadConfig<Config>("vip_teammates_heal");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;

        _healPercentages[player.Slot] = GetFeatureValue<float>(player);
    }

    private HookResult OnTakeDamage(DynamicHook hook)
    {
        var baseEntity = hook.GetParam<CBaseEntity>(0);

        var player = GetPlayer(baseEntity);
        if (player is null || player.IsBot)
            return HookResult.Continue;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is null)
            return HookResult.Continue;

        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

        var attacker = GetPlayer(damageInfo.Attacker.Value);
        if (attacker is null || attacker.IsBot)
            return HookResult.Continue;

        if (attacker != player &&
            attacker.Team == player.Team &&
            IsClientVip(attacker) &&
            PlayerHasFeature(attacker))
        {
            var weapon = damageInfo.Inflictor.Value?.As<CBasePlayerWeapon>();
            if (weapon is null)
            {
                weapon = attacker.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
                if (weapon is null)
                {
                    return HookResult.Continue;
                }
            }

            if (_config.WeaponBlacklist.Contains(weapon.DesignerName))
                return HookResult.Continue;

            var health = playerPawn.Health;
            
            var maxHealth = _config.MaxHealth;
            if (maxHealth is 0)
            {
                maxHealth = playerPawn.MaxHealth;
            }
            
            if (health >= maxHealth) return HookResult.Continue;
            
            var healPercentage = _healPercentages[attacker.Slot];
            
            var calculatedGain = (int)MathF.Ceiling(damageInfo.Damage / 100.0f * healPercentage);
            
            var healthGain = calculatedGain;
            
            var healPerShot = _config.HealPerShot;
            if (healPerShot is not 0)
                healthGain = Math.Min(calculatedGain, healPerShot);
            
            playerPawn.Health = Math.Min(health + healthGain, maxHealth);
            Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
            
        }

        return HookResult.Continue;
    }

    private static CCSPlayerController? GetPlayer(CBaseEntity? ent)
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

    public void Dispose()
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
    }
}