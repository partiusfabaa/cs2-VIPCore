﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Attributes;
using VipCoreApi;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VIP_CustomDefaultAmmo;

public class VipCustomDefaultAmmo : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Custom Default Ammo";
    public override string ModuleVersion => "v1.0";

    private IVipCoreApi? _api;
    private CustomDefaultAmmo? _CustomDefaultAmmoFeature;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _CustomDefaultAmmoFeature = new CustomDefaultAmmo(this, _api);
        _api.RegisterFeature(_CustomDefaultAmmoFeature);
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _CustomDefaultAmmoFeature != null)
        {
            _api?.UnRegisterFeature(_CustomDefaultAmmoFeature);
        }
    }
}

public class CustomDefaultAmmo : VipFeatureBase
{
    public override string Feature => "CustomDefaultAmmo";
    private CustomDefaultAmmoConfig _config;

    public CustomDefaultAmmo(VipCustomDefaultAmmo vipCustomAmmo, IVipCoreApi api) : base(api)
    {
        vipCustomAmmo.RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
        _config = new CustomDefaultAmmoConfig();
    }
    
    public void OnEntityCreated(CEntityInstance entity)
    {
        CBasePlayerWeapon? weapon = new(entity.Entity.Handle);
        if (weapon == null || !weapon.IsValid) return;

        CCSPlayerController? player =  Utilities.GetPlayerFromSteamId((ulong)weapon.OriginalOwnerXuidLow);
        if (player == null) return;
        
        _config = GetFeatureValue<CustomDefaultAmmoConfig>(player);
        
        if (entity == null || entity.Entity == null || !entity.IsValid || !entity.DesignerName.Contains("weapon_")) return;
        
        foreach (var item in _config.WeaponSettings)
        {
            if (string.IsNullOrEmpty(item.Key) || item.Value == null) continue;
            
            Server.NextFrame(() =>
            {
                if (!weapon.IsValid) return;
                
                string weaponName = item.Key.Trim();
                
                if (!CheckIfWeapon(weaponName, weapon.AttributeManager.Item.ItemDefinitionIndex)) return;
                
                CCSWeaponBase _weapon = weapon.As<CCSWeaponBase>();
                if (_weapon == null) return;

                if (item.Value.DefaultClip != -1)
                {
                    if (_weapon.VData != null)
                    {
                        _weapon.VData.MaxClip1 = item.Value.DefaultClip;
                        _weapon.VData.DefaultClip1 = item.Value.DefaultClip;
                    }

                    _weapon.Clip1 = item.Value.DefaultClip;

                    Utilities.SetStateChanged(weapon.As<CCSWeaponBase>(), "CBasePlayerWeapon", "m_iClip1");
                }

                if (item.Value.DefaultReserve != -1)
                {
                    if (_weapon.VData != null)
                    {
                        _weapon.VData.PrimaryReserveAmmoMax = item.Value.DefaultReserve;
                    }
                    _weapon.ReserveAmmo[0] = item.Value.DefaultReserve;

                    Utilities.SetStateChanged(weapon.As<CCSWeaponBase>(), "CBasePlayerWeapon", "m_pReserveAmmo");
                }
            });
        }
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
    public Dictionary<string, WeaponSetting> WeaponSettings { get; set; } = new();

    public class WeaponSetting
    {
        public int DefaultClip { get; set; } = -1;
        public int DefaultReserve { get; set; } = -1;
    }
}