using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Attributes;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;
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
    public override string ModuleVersion => "v1.1";

    private IVipCoreApi? _api;
    private CustomDefaultAmmo? _customDefaultAmmoFeature;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    public Dictionary<string, Dictionary<string, WeaponSettings>> _ammoConfig = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _customDefaultAmmoFeature = new CustomDefaultAmmo(this, _api);
        _api.RegisterFeature(_customDefaultAmmoFeature);

        LoadConfig();
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _customDefaultAmmoFeature != null)
        {
            _api.UnRegisterFeature(_customDefaultAmmoFeature);
        }
    }

    private void LoadConfig()
    {
        if (_api == null)
        {
            throw new InvalidOperationException("API is not initialized.");
        }

        var configPath = Path.Combine(_api.ModulesConfigDirectory, "vip_custom_default_ammo.json");

        if (!File.Exists(configPath))
        {
            _ammoConfig = CreateConfig();
            SaveConfig(configPath);
        }
        else
        {
            var configJson = File.ReadAllText(configPath);
            _ammoConfig = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, WeaponSettings>>>(configJson) ?? CreateConfig();
        }
    }

    private Dictionary<string, Dictionary<string, WeaponSettings>> CreateConfig()
    {
        return new Dictionary<string, Dictionary<string, WeaponSettings>>
        {
            {
                "VIPGroup1", new Dictionary<string, WeaponSettings>
                {
                    { "weapon_awp", new WeaponSettings { DefaultClip = 50, DefaultReserve = 100 } },
                    { "weapon_ak47", new WeaponSettings { DefaultClip = 35, DefaultReserve = 120 } }
                }
            },
            {
                "VIPGroup2", new Dictionary<string, WeaponSettings>
                {
                    { "weapon_awp", new WeaponSettings { DefaultClip = 30, DefaultReserve = 90 } },
                    { "weapon_m4a1", new WeaponSettings { DefaultClip = 30, DefaultReserve = 90 } }
                }
            }
        };
    }

    private void SaveConfig(string configPath)
    {
        var configJson = JsonSerializer.Serialize(_ammoConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, configJson);
    }

}

public class CustomDefaultAmmo : VipFeatureBase
{
    public override string Feature => "CustomDefaultAmmo";
    private readonly VipCustomDefaultAmmo _vipCustomAmmo;
    private readonly bool[] _customEnabled = new bool[64];

    public CustomDefaultAmmo(VipCustomDefaultAmmo vipCustomAmmo, IVipCoreApi api) : base(api)
    {
        _vipCustomAmmo = vipCustomAmmo;
        vipCustomAmmo.RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);

        // Re-apply the VIP group's custom ammo when a VIP picks a weapon up off the ground.
        vipCustomAmmo.RegisterEventHandler<EventItemPickup>(OnItemPickup);
    }

    private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot ||
            player.Connected != PlayerConnectedState.Connected)
            return HookResult.Continue;

        if (!PlayerHasFeature(player) || !_customEnabled[player.Slot])
            return HookResult.Continue;

        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess)
            return HookResult.Continue;

        var groupName = GetFeatureValue<string>(player);
        if (string.IsNullOrEmpty(groupName) ||
            !_vipCustomAmmo._ammoConfig.TryGetValue(groupName, out var weaponSettings))
            return HookResult.Continue;

        // The picked-up weapon becomes the player's active weapon a frame later; apply then.
        Server.NextFrame(() =>
        {
            if (!player.IsValid) return;

            var pawn = player.PlayerPawn.Value;
            var activeWeaponHandle = pawn?.WeaponServices?.ActiveWeapon;
            var active = activeWeaponHandle is { IsValid: true } ? activeWeaponHandle.Value : null;
            if (active == null || !active.IsValid) return;

            ApplyAmmoToWeapon(active, weaponSettings);
        });

        return HookResult.Continue;
    }

    /// <summary>
    /// Applies the VIP group's clip/reserve settings to a single weapon instance, if the weapon
    /// matches an entry in the settings. Never touches the shared VData - only this instance.
    /// </summary>
    private void ApplyAmmoToWeapon(CBasePlayerWeapon weapon, Dictionary<string, WeaponSettings> weaponSettings)
    {
        if (weapon == null || !weapon.IsValid) return;

        var defIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

        foreach (var item in weaponSettings)
        {
            if (string.IsNullOrEmpty(item.Key) || item.Value == null) continue;
            if (!CheckIfWeapon(item.Key.Trim(), defIndex)) continue;

            CCSWeaponBase? _weapon = weapon.As<CCSWeaponBase>();
            if (_weapon == null) return;

            if (item.Value.DefaultClip != -1)
            {
                _weapon.Clip1 = item.Value.DefaultClip;
                Utilities.SetStateChanged(_weapon, "CBasePlayerWeapon", "m_iClip1");
            }

            if (item.Value.DefaultReserve != -1)
            {
                _weapon.ReserveAmmo[0] = item.Value.DefaultReserve;
                Utilities.SetStateChanged(_weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
            }

            break;
        }
    }

    public void OnEntityCreated(CEntityInstance entity)
    {
        if (entity == null || entity.Entity == null || !entity.IsValid || !entity.DesignerName.Contains("weapon_"))
            return;

        CBasePlayerWeapon? weapon = new(entity.Handle);
        if (weapon == null || !weapon.IsValid)
            return;

        // Resolve settings a frame later, once the engine has assigned the weapon's owner.
        Server.NextFrame(() =>
        {
            if (!weapon.IsValid) return;

            // KEY FIX: only apply custom ammo to a weapon that is actually OWNED by a player (i.e.
            // freshly given / picked up). A dropped weapon (on death, or thrown) has no owner, so we
            // leave it completely untouched - it keeps whatever ammo the player really had (empty
            // stays empty, low stays low). Previously every created weapon entity, including dropped
            // ones, was force-filled to the config amount, which is the bug being fixed.
            var owner = weapon.OwnerEntity.Value;
            if (owner == null || !owner.IsValid)
                return;

            var ownerController = owner.As<CCSPlayerPawn>()?.Controller.Value?.As<CCSPlayerController>();
            if (ownerController == null || !ownerController.IsValid)
                return;

            if (ownerController.IsBot || ownerController.Connected != PlayerConnectedState.Connected)
                return;

            if (!PlayerHasFeature(ownerController) || !_customEnabled[ownerController.Slot])
                return;

            if (GetPlayerFeatureState(ownerController) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess)
                return;

            var groupName = GetFeatureValue<string>(ownerController);
            if (string.IsNullOrEmpty(groupName) ||
                !_vipCustomAmmo._ammoConfig.TryGetValue(groupName, out var weaponSettings))
                return;

            // NOTE: ApplyAmmoToWeapon only sets this instance's live ammo, never the shared VData.
            ApplyAmmoToWeapon(weapon, weaponSettings);
        });
    }

    public override void OnPlayerLoaded(CCSPlayerController player, string group)
    {
        if (!PlayerHasFeature(player)) return;

        _customEnabled[player.Slot] = GetPlayerFeatureState(player) == FeatureState.Enabled;
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        _customEnabled[player.Slot] = state == FeatureState.Enabled;
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

public class WeaponSettings
{
    public int DefaultClip { get; set; }
    public int DefaultReserve { get; set; }
}
