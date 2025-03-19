using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_CustomDefaultAmmo;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Custom Default Ammo";
    public override string ModuleVersion => "v2.0.0";

    private CustomDefaultAmmo? _customDefaultAmmo;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _customDefaultAmmo = new CustomDefaultAmmo(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _customDefaultAmmo?.Dispose();
    }
}

public class CustomDefaultAmmo : VipFeature<string>
{
    private readonly bool[] _customEnabled = new bool[64];

    public Dictionary<string, Dictionary<string, WeaponSettings>> _ammoConfig = new();

    public CustomDefaultAmmo(Plugin plugin, IVipCoreApi api) : base("CustomDefaultAmmo", api)
    {
        _ammoConfig = LoadConfig<Dictionary<string, Dictionary<string, WeaponSettings>>>("vip_custom_default_ammo");
        plugin.RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
    }

    public void OnEntityCreated(CEntityInstance entity)
    {
        if (entity == null || entity.Entity == null || !entity.IsValid || !entity.DesignerName.Contains("weapon_"))
            return;

        CBasePlayerWeapon? weapon = new(entity.Handle);
        if (weapon == null || !weapon.IsValid)
            return;

        var players = Utilities.GetPlayers().Where(x =>
            x is { IsBot: false, Connected: PlayerConnectedState.PlayerConnected } && PlayerHasFeature(x) &&
            _customEnabled[x.Slot]);

        foreach (var player in players)
        {
            if (player == null || !IsPlayerValid(player)) return;

            var groupName = GetValue(player);
            if (string.IsNullOrEmpty(groupName) || !_ammoConfig.TryGetValue(groupName, out var weaponSettings))
                continue;

            foreach (var item in weaponSettings)
            {
                if (string.IsNullOrEmpty(item.Key) || item.Value == null) continue;

                Server.NextFrame(() =>
                {
                    if (!weapon.IsValid) return;

                    string weaponName = item.Key.Trim();

                    if (!CheckIfWeapon(weaponName, weapon.AttributeManager.Item.ItemDefinitionIndex)) return;

                    CCSWeaponBase? _weapon = weapon.As<CCSWeaponBase>();
                    if (_weapon == null) return;

                    if (item.Value.DefaultClip != -1)
                    {
                        if (_weapon.VData != null)
                        {
                            _weapon.VData.MaxClip1 = item.Value.DefaultClip;
                            _weapon.VData.DefaultClip1 = item.Value.DefaultClip;
                        }

                        _weapon.Clip1 = item.Value.DefaultClip;

                        Utilities.SetStateChanged(_weapon, "CBasePlayerWeapon", "m_iClip1");
                    }

                    if (item.Value.DefaultReserve != -1)
                    {
                        if (_weapon.VData != null)
                        {
                            _weapon.VData.PrimaryReserveAmmoMax = item.Value.DefaultReserve;
                        }

                        _weapon.ReserveAmmo[0] = item.Value.DefaultReserve;

                        Utilities.SetStateChanged(_weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
                    }
                });
            }
        }
    }

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        _customEnabled[player.Slot] = IsPlayerValid(player);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        _customEnabled[player.Slot] = feature.State == FeatureState.Enabled;
    }

    public bool CheckIfWeapon(string weaponName, int weaponDefIndex)
    {
        Dictionary<int, string> weaponDefindex = new()
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

        return weaponDefindex.TryGetValue(weaponDefIndex, out string? value) && value == weaponName;
    }
}

public class WeaponSettings
{
    public int DefaultClip { get; set; }
    public int DefaultReserve { get; set; }
}