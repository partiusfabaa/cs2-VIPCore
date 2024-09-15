using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text.Json;
using VipCoreApi;

namespace VIP_GunsPack;

public class VIP_GunsPack : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[VIP] Guns Pack";
    public override string ModuleVersion => "1.0";

    private readonly Dictionary<CCSPlayerController, int> _commandUsageCount = new();
    private static readonly Dictionary<string, string> WeaponList = new()
    {
        {"weapon_deagle", "Desert Eagle"},
        {"weapon_elite", "Dual Berettas"},
        {"weapon_fiveseven", "Five-SeveN"},
        {"weapon_glock", "Glock-18"},
        {"weapon_ak47", "AK-47"},
        {"weapon_aug", "AUG"},
        {"weapon_awp", "AWP"},
        {"weapon_famas", "FAMAS"},
        {"weapon_g3sg1", "G3SG1"},
        {"weapon_galilar", "Galil AR"},
        {"weapon_m249", "M249"},
        {"weapon_m4a1", "M4A1"},
        {"weapon_mac10", "MAC-10"},
        {"weapon_p90", "P90"},
        {"weapon_mp5sd", "MP5-SD"},
        {"weapon_ump45", "UMP-45"},
        {"weapon_xm1014", "XM1014"},
        {"weapon_bizon", "PP-Bizon"},
        {"weapon_mag7", "MAG-7"},
        {"weapon_negev", "Negev"},
        {"weapon_sawedoff", "Sawed-Off"},
        {"weapon_tec9", "Tec-9"},
        {"weapon_taser", "Zeus x27"},
        {"weapon_hkp2000", "P2000"},
        {"weapon_mp7", "MP7"},
        {"weapon_mp9", "MP9"},
        {"weapon_nova", "Nova"},
        {"weapon_p250", "P250"},
        {"weapon_scar20", "SCAR-20"},
        {"weapon_sg556", "SG 553"},
        {"weapon_ssg08", "SSG 08"},
        {"weapon_m4a1_silencer", "M4A1-S"},
        {"weapon_usp_silencer", "USP-S"},
        {"weapon_cz75a", "CZ75-Auto"},
        {"weapon_revolver", "R8 Revolver"},
        {"weapon_healthshot", "Healtshot" },
        {"weapon_hegrenade", "Grenade" },
        {"weapon_smokegrenade", "Smoke" },
        {"weapon_decoy", "Decoy" },
        {"weapon_molotov", "Molotov" },
        {"weapon_flashbang", "Flashbang" },
    };

    private Config _config = null!;
    private IVipCoreApi? _api;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;
        _config = LoadConfig();

        AddCommand(_config.CommandName, "opens guns pack", Command_Pack);
    }
    private Config LoadConfig()
    {
        var configPath = Path.Combine(_api.ModulesConfigDirectory, "vip_gunspack.json");

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }
    public void Command_Pack(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || _api == null) return;

        if (!_api.IsClientVip(player))
        {
            _api.PrintToChat(player, Localizer["not.Vip"]);
            return;
        }

        if (_config.Packs.Count == 0)
        {
            _api.PrintToChat(player, Localizer["no.Pack"]);
            return;
        }

        if (_commandUsageCount.ContainsKey(player) && _commandUsageCount[player] >= _config.CommandUsageLimit)
        {
            _api.PrintToChat(player, Localizer["usage.LimitReached"]);
            return;
        }

        var packMenu = new ChatMenu(Localizer["pack.menu_title"]);

        foreach (var pack in _config.Packs)
        {
            var packName = pack.Name;
            var weaponsList = string.Join(", ", pack.Weapons.Select(w => WeaponList.ContainsKey(w) ? WeaponList[w] : w));
            var packDescription = string.Format(Localizer["pack_description"], packName, weaponsList);

            packMenu.AddMenuOption(packDescription, (player, option) =>
            {
                var selectedPack = _config.Packs.FirstOrDefault(p => p.Name == packName);
                if (selectedPack == null) return;

                bool hasPermission = selectedPack.Permissions.Count == 0 ||
                                     selectedPack.Permissions.Any(permission => AdminManager.PlayerHasPermissions(player, permission));

                if (!hasPermission)
                {
                    _api.PrintToChat(player, Localizer["no.Permission"]);
                    return;
                }

                foreach (var weapon in selectedPack.Weapons)
                {
                    player.GiveNamedItem(weapon);
                }

                if (_commandUsageCount.ContainsKey(player))
                    _commandUsageCount[player]++;
                else
                    _commandUsageCount[player] = 1;

                _api.PrintToChat(player, $"{string.Format(Localizer["received.Pack"], selectedPack.Name)}");
            });
        }

        MenuManager.OpenChatMenu(player, packMenu);
    }


    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            Packs = new List<GunPack>
            {
                new GunPack
                {
                    Name = "Default Pack",
                    Weapons = new List<string> { "weapon_ak47", "weapon_deagle" },
                    Permissions = new List<string> { "@css/vips" }
                },
                new GunPack
                {
                    Name = "Sniper Pack",
                    Weapons = new List<string> { "weapon_awp", "weapon_usp_silencer" },
                    Permissions = new List<string> { "@css/viphero" }
                }
            },
            CommandName = "css_pack",
            CommandUsageLimit = 1,

        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return config;
    }

    public class Config
    {
        public List<GunPack> Packs { get; set; } = new List<GunPack>();
        public string CommandName { get; set; } = "css_pack";
        public int CommandUsageLimit { get; set; } = 1;
    }

    public class GunPack
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Weapons { get; set; } = new List<string>();
        public List<string> Permissions { get; set; } = new List<string>();
    }
}
