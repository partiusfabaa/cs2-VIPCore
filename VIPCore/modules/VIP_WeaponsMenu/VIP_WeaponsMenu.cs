using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Options;
using System.Text.Json;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_WeaponsMenu;

public class VipWeaponsMenu : BasePlugin
{
    public override string ModuleAuthor => "daffyy";
    public override string ModuleName => "[VIP] WeaponsMenu";
    public override string ModuleVersion => "v1.0.0";
    
    private WeaponsMenu _weaponsMenu;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _weaponsMenu = new WeaponsMenu(this, _api);
        _api.RegisterFeature(_weaponsMenu, FeatureType.Hide);
    }
    
    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_weaponsMenu);
    }
}

public class WeaponsMenu : VipFeatureBase
{
    private VipWeaponsMenu _weaponsMenu;
    private static CCSGameRules? _gameRules;

    public override string Feature => "WeaponsMenu";

    public WeaponsMenu(VipWeaponsMenu weaponsMenu, IVipCoreApi api) : base(api)
    {
        _weaponsMenu = weaponsMenu;
        _weaponsMenu.RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        _weaponsMenu.RegisterListener<Listeners.OnMapStart>(OnMapStart);
    }

    private void OnMapStart(string mapName)
    {
        _weaponsMenu.AddTimer(1.0f, () =>
        {
            _gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        });
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (_gameRules != null && _gameRules.WarmupPeriod)
            return HookResult.Continue;

        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot || player.TeamNum < 2)
            return HookResult.Continue;

        CreateMenu(player);

        return HookResult.Continue;
    }

    private void CreateMenu(CCSPlayerController player)
    {
        if (!player.PawnIsAlive || player.Connected != PlayerConnectedState.PlayerConnected)
            return;
        if (!IsClientVip(player) || !PlayerHasFeature(player) || GetPlayerFeatureState(player) is not FeatureState.Enabled)
            return;

        var json = GetFeatureValue<JsonElement>(player).GetRawText();
        var WeaponsSettings = JsonSerializer.Deserialize<WeaponsMenuSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (WeaponsSettings == null) return;

        var menu = new ChatMenu(GetTranslatedText("weaponsmenu.title"));
        int actualRound = ActualRound();

        foreach (var package in player.Team == CsTeam.Terrorist ? WeaponsSettings.T : WeaponsSettings.CT)
        {
            menu.AddMenuOption($"{GetTranslatedText("weaponsmenu.fromround", package.Value.Round)} {package.Key}", (controller, _) =>
            {
                RemoveWeapons(controller);

                package.Value.Weapons.ForEach(w =>
                {
                    controller.GiveNamedItem(w);
                });

            }, actualRound < package.Value.Round);
        }

        menu.Open(player);
    }

    private static int ActualRound()
    {
        return _gameRules?.RoundsPlayedThisPhase ?? 0;
    }

    private void RemoveWeapons(CCSPlayerController player)
    {
        if (player.PlayerPawn.Value?.WeaponServices == null || player.PlayerPawn.Value.ItemServices == null)
            return;

        var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;

        if (weapons.Count == 0)
            return;
        if (player.Team is CsTeam.None or CsTeam.Spectator)
            return;

        foreach (var weapon in weapons)
        {
            if (!weapon.IsValid || weapon.Value == null ||
                !weapon.Value.IsValid || !weapon.Value.DesignerName.Contains("weapon_"))
                continue;


            if (weapon.Value.Entity == null) continue;

            CCSWeaponBaseVData? weaponData = weapon.Value.As<CCSWeaponBase>().VData;

            if (weaponData == null) continue;

            if (weaponData.GearSlot == gear_slot_t.GEAR_SLOT_RIFLE || weaponData.GearSlot == gear_slot_t.GEAR_SLOT_PISTOL || weaponData.GearSlot == gear_slot_t.GEAR_SLOT_GRENADES)
                weapon.Value.Remove();
        }
    }
}

public class WeaponsMenuSettings
{
    public required Dictionary<string, WeaponSelection> CT { get; set; }
    public required Dictionary<string, WeaponSelection> T { get; set; }
}

public class WeaponSelection
{
    public List<string> Weapons { get; set; }
    public int Round { get; set; }
}