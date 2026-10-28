using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;
using System.Globalization;

namespace VIP_TeammatesHeal;

public class VipTeammatesHeal : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Teammates Heal";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _api;
    private TeammatesHeal? _flags;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();

        if (_api == null) return;

        _flags = new TeammatesHeal(_api);
    }

    public override void Unload(bool hotReload)
    {
        _flags?.Dispose();
    }
}

public class Config
{
    public List<string> WeaponBlacklist { get; set; } = ["weapon_hegrenade", "weapon_molotov"];
    public int MaxHealth { get; set; } = 100;
    public int HealPerShot { get; set; } = 25;
}

public class TeammatesHeal : VipFeature<float>
{

    private readonly Config _config;

    public TeammatesHeal(IVipCoreApi api) : base("TeammatesHeal", api)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
        _config = LoadConfig<Config>("vip_teammates_heal");
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
            IsFeatureEnabled(attacker))
        {
            var weaponName = GetWeaponName(damageInfo.Inflictor.Value, attacker);
            if (weaponName is null)
                return HookResult.Continue;

            if (_config.WeaponBlacklist.Contains(weaponName) ||
                (weaponName == "weapon_molotov" && _config.WeaponBlacklist.Contains("weapon_incgrenade")))
                return HookResult.Continue;

            var health = playerPawn.Health;
            
            var maxHealth = _config.MaxHealth;
            if (maxHealth is 0)
            {
                maxHealth = playerPawn.MaxHealth;
            }
            
            if (health >= maxHealth) return HookResult.Continue;
            
            var healPercentage = GetValue(attacker);
            
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

    private static string? GetWeaponName(CEntityInstance? inflictor, CCSPlayerController attacker)
    {
        var name = inflictor?.DesignerName;
        if (string.IsNullOrEmpty(name) || name == "player")
            return attacker.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value?.DesignerName;

        return name switch
        {
            "inferno" or "molotov_projectile" => "weapon_molotov",
            _ when name.EndsWith("_projectile") => "weapon_" + name[..^"_projectile".Length],
            _ => name
        };
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

    public override void Dispose()
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        base.Dispose();
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
            args.Display = $"[{GetValue(args.Controller).ToString("0.#", CultureInfo.InvariantCulture)}%]";
    }
}