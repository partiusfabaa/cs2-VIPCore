﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Attributes;
using VipCoreApi;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace VIP_CustomDefaultAmmo;
    
public class VipCustomDefaultAmmo : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Custom Default Ammo";
    public override string ModuleVersion => "v1.0";
    private IVipCoreApi? _api;
    private CustomDefaultAmmo? _CustomDefaultAmmoFeature;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    public CustomDefaultAmmoConfig _config = null!;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _CustomDefaultAmmoFeature = new CustomDefaultAmmo(this, _api);
        _api.RegisterFeature(_CustomDefaultAmmoFeature);

        RegisterListener<Listeners.OnEntityCreated>(_CustomDefaultAmmoFeature.OnEntityCreated);

        _config = LoadConfig();
    }
    
    public override void Unload(bool hotReload)
    {
        if (_api != null && _CustomDefaultAmmoFeature != null)
        {
            _api?.UnRegisterFeature(_CustomDefaultAmmoFeature);
        }
    }
    
    private CustomDefaultAmmoConfig LoadConfig()
    {
        if (_api == null) throw new InvalidOperationException("API is not initialized.");

        var configPath = Path.Combine(_api.ModulesConfigDirectory, "vip_custom_default_ammo.json");

        if (!File.Exists(configPath))
        {
            return CreateConfig(configPath);
        }

        var configJson = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<CustomDefaultAmmoConfig>(configJson) ?? CreateConfig(configPath);
    }
    
    private CustomDefaultAmmoConfig CreateConfig(string configPath)
    {
        var defaultConfig = new CustomDefaultAmmoConfig
        {
            WeaponSettings = new Dictionary<string, WeaponSettings>
            {
                { "weapon_awp", new WeaponSettings { DefaultClip = 50, DefaultReserve = 100 } },
                { "weapon_ak47", new WeaponSettings { DefaultClip = 30, DefaultReserve = 120 } },
                { "weapon_m4a1", new WeaponSettings { DefaultClip = 20, DefaultReserve = 90 } }
            }
        };

        File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
        return defaultConfig;
    }
}

public class CustomDefaultAmmo : VipFeatureBase
{
    public override string Feature => "CustomDefaultAmmo";
    private readonly VipCustomDefaultAmmo _vipCustomAmmo;

    public CustomDefaultAmmo(VipCustomDefaultAmmo vipCustomAmmo, IVipCoreApi api) : base(api)
    {
        _vipCustomAmmo = vipCustomAmmo;
    }

    public void OnEntityCreated(CEntityInstance? entity)
    {
        if (entity == null || !IsValidWeaponEntity(entity)) return;
        
        var weapon = CreateWeapon(entity);
        if (weapon == null) return;
        
        foreach (var player in GetEligiblePlayers())
        {
            if (!IsValidPlayer(player)) continue;
            
            ApplyCustomAmmoSettings(weapon, player);
        }
    }

    private bool IsValidWeaponEntity(CEntityInstance entity)
    {
        return entity != null && entity.IsValid && entity.Entity != null && entity.Entity.Handle != IntPtr.Zero && entity.DesignerName.Contains("weapon_");
    }
    
    private CBasePlayerWeapon? CreateWeapon(CEntityInstance entity)
    {
        var weapon = new CBasePlayerWeapon(entity.Handle);
        return weapon.IsValid ? weapon : null;
    }

    private IEnumerable<CCSPlayerController> GetEligiblePlayers()
    {
        return Utilities.GetPlayers().Where(x => x is { IsBot: false, Connected: PlayerConnectedState.PlayerConnected });
    }
    
    private bool IsValidPlayer(CCSPlayerController? player)
    {
        return player != null && PlayerHasFeature(player) && GetPlayerFeatureState(player) != IVipCoreApi.FeatureState.Disabled && GetPlayerFeatureState(player) != IVipCoreApi.FeatureState.NoAccess;
    }

    private void ApplyCustomAmmoSettings(CBasePlayerWeapon weapon, CCSPlayerController? player)
    {
        var config = _vipCustomAmmo._config;

        foreach (var item in config.WeaponSettings)
        {
            if (string.IsNullOrEmpty(item.Key) || item.Value == null) continue;

            Server.NextFrame(() =>
            {
                if (!weapon.IsValid) return;

                if (!CheckIfWeapon(item.Key.Trim(), weapon.AttributeManager.Item.ItemDefinitionIndex)) return;

                ApplyAmmoSettingsToWeapon(weapon, item.Value);
            });
        }
    }

    private void ApplyAmmoSettingsToWeapon(CBasePlayerWeapon weapon, WeaponSettings settings)
    {
        var weaponBase = weapon.As<CCSWeaponBase>();
        if (weaponBase == null) return;
        
        if (settings.DefaultClip != -1)
        {
            SetWeaponClip(weaponBase, settings.DefaultClip);
        }
        
        if (settings.DefaultReserve != -1)
        {
            SetWeaponReserveAmmo(weaponBase, settings.DefaultReserve);
        }
    }

    private void SetWeaponClip(CCSWeaponBase weapon, int clip)
    {
        if (weapon.VData != null)
        {
            weapon.VData.MaxClip1 = clip;
            weapon.VData.DefaultClip1 = clip;
        }

        weapon.Clip1 = clip;

        Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_iClip1");
    }

    private void SetWeaponReserveAmmo(CCSWeaponBase weapon, int reserve)
    {
        if (weapon.VData != null)
        {
            weapon.VData.PrimaryReserveAmmoMax = reserve;
        }
        
        weapon.ReserveAmmo[0] = reserve;
        
        Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
    }

    
    
    public bool CheckIfWeapon(string weaponName, int weaponDefIndex)
    {
        Dictionary<int, string> WeaponDefindex = new()
        {
            { 1, "weapon_deagle" },
            { 2, "weapon_elite" },
            { 3, "weapon_fiveseven" },
            { 4, "weapon_glock" },
            { 7, "weapon_ak47" },
            { 8, "weapon_aug" },
            { 9, "weapon_awp" },
            { 10, "weapon_famas" },
            { 11, "weapon_g3sg1" },
            { 13, "weapon_galilar" },
            { 14, "weapon_m249" },
            { 16, "weapon_m4a1" },
            { 17, "weapon_mac10" },
            { 19, "weapon_p90" },
            { 23, "weapon_mp5sd" },
            { 24, "weapon_ump45" },
            { 25, "weapon_xm1014" },
            { 26, "weapon_bizon" },
            { 27, "weapon_mag7" },
            { 28, "weapon_negev" },
            { 29, "weapon_sawedoff" },
            { 30, "weapon_tec9" },
            { 32, "weapon_hkp2000" },
            { 33, "weapon_mp7" },
            { 34, "weapon_mp9" },
            { 35, "weapon_nova" },
            { 36, "weapon_p250" },
            { 38, "weapon_scar20" },
            { 39, "weapon_sg556" },
            { 40, "weapon_ssg08" },
            { 60, "weapon_m4a1_silencer" },
            { 61, "weapon_usp_silencer" },
            { 63, "weapon_cz75a" },
            { 64, "weapon_revolver" },
        };

        return WeaponDefindex.TryGetValue(weaponDefIndex, out string? value) && value == weaponName;
    }
}

public class CustomDefaultAmmoConfig
{
    public Dictionary<string, WeaponSettings> WeaponSettings { get; set; } = new();
}

public class WeaponSettings
{
    public int DefaultClip { get; init; }
    public int DefaultReserve { get; init; }
}