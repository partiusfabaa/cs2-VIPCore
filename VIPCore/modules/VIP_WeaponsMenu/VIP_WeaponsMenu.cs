using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_WeaponsMenu;

public class VipWeaponsMenu : BasePlugin
{
    public override string ModuleAuthor => "daffyy";
    public override string ModuleName => "[VIP] WeaponsMenu";
    public override string ModuleVersion => "v1.0.2";
    
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

    private readonly Dictionary<int, Dictionary<CCSPlayerController, WeaponSelection>> _playerSelection = new();

    private readonly Dictionary<string, int> _grenadeIndex = new()
    {
        ["flashbang"] = 14,
        ["smokegrenade"] = 15,
        ["decoy"] = 17,
        ["incgrenade"] = 16,
        ["molotov"] = 16,
        ["hegrenade"] = 13
    };

    public override string Feature => "WeaponsMenu";

    public WeaponsMenu(VipWeaponsMenu weaponsMenu, IVipCoreApi api) : base(api)
    {
        _weaponsMenu = weaponsMenu;
        _weaponsMenu.RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        _weaponsMenu.RegisterEventHandler<EventPlayerDisconnect>(EventPlayerDisconnect);
        _weaponsMenu.RegisterListener<Listeners.OnMapStart>(OnMapStart);
        _weaponsMenu.AddCommand("css_vwmenu", "Reset vip package selection", OnVwMenuCommand);
    }

    private void OnVwMenuCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid) return;
        if (!IsClientVip(caller) || !PlayerHasFeature(caller) || GetPlayerFeatureState(caller) is not FeatureState.Enabled)
            return;

        RemovePlayerSelection(caller);

        Api.PrintToChat(caller, GetTranslatedText("weaponsmenu.resetinfo.reset"));
    }

    private void OnMapStart(string mapName)
    {
        _weaponsMenu.AddTimer(1.0f, () =>
        {
            _gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        });
    }

    private HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        RemovePlayerSelection(player);

        return HookResult.Continue;
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (_gameRules is { WarmupPeriod: true })
            return HookResult.Continue;

        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot || player.TeamNum < 2)
            return HookResult.Continue;

        if (!IsClientVip(player) || !PlayerHasFeature(player) || GetPlayerFeatureState(player) is not FeatureState.Enabled)
            return HookResult.Continue;

        MenuManager.CloseActiveMenu(player);
        Server.NextFrame(() => _weaponsMenu.AddTimer(1.0f, () => CreateMenu(player)));

        return HookResult.Continue;
    }

    private void CreateMenu(CCSPlayerController player)
    {
        if (!player.PawnIsAlive || player.PlayerPawn.Value == null || player.Connected != PlayerConnectedState.PlayerConnected)
            return;

        if (GetPlayerSelection(player, out var selection) && selection != null)
        {
            if (GetActualRound() < selection.Round)
                return;

            if (_gameRules is { BuyTimeEnded: true } || !player.PlayerPawn.Value.InBuyZone)
                return;

            RemoveWeapons(player);

            selection.Weapons.ForEach(w =>
            {
                if (_grenadeIndex.TryGetValue(w, out var ammoIndex))
                {
                    if (player.PlayerPawn.Value?.WeaponServices?.Ammo[ammoIndex] == 0)
                        player.GiveNamedItem(w);
                    else
                    {
                        if (player.PlayerPawn.Value?.WeaponServices?.MyWeapons
                                .ToList()
                                .Find(m => m.Value != null && m.Value.DesignerName == w) == null)
                            player.GiveNamedItem(w);
                    }
                }
                else
                {
                    player.GiveNamedItem(w);
                }
            });

            return;
        }

        var json = GetFeatureValue<JsonElement>(player).GetRawText();
        var weaponsSettings = JsonSerializer.Deserialize<WeaponsMenuSettings>(json);

        if (weaponsSettings == null) return;

        var menu = new ChatMenu(GetTranslatedText("weaponsmenu.title"))
        {
            ExitButton = true,
            //PostSelectAction = PostSelectAction.Close
        };

        foreach (var package in player.Team == CsTeam.Terrorist ? weaponsSettings.T : weaponsSettings.CT)
        {
            menu.AddMenuOption($"{GetTranslatedText("weaponsmenu.fromround", package.Value.Round)} {package.Key}", (_, _) =>
            {
                if (GetActualRound() < package.Value.Round)
                    return;
                if (_gameRules is { BuyTimeEnded: true } || !player.PlayerPawn.Value.InBuyZone)
                    return;

                RemoveWeapons(player);

                package.Value.Weapons.ForEach(w =>
                {
                    if (_grenadeIndex.TryGetValue(w, out var ammoIndex))
                    {
                        if (player.PlayerPawn.Value?.WeaponServices?.Ammo[ammoIndex] == 0)
                            player.GiveNamedItem(w);
                        else
                        {
                            if (player.PlayerPawn.Value?.WeaponServices?.MyWeapons.ToList()
                                    .Find(m => m.Value != null && m.Value.DesignerName == w) == null)
                                player.GiveNamedItem(w);
                        }
                    } 
                    else
                    {
                        player.GiveNamedItem(w);
                    }
                });

                CreateSubMenu(player, package.Value);

            }, GetActualRound() < package.Value.Round);
        }

        menu.Open(player);
    }

    private void CreateSubMenu(CCSPlayerController player, WeaponSelection selection)
    {
        var menu = new ChatMenu(GetTranslatedText("weaponsmenu.wanttosave"))
        {
            PostSelectAction = PostSelectAction.Close
        };

        menu.AddMenuOption(GetTranslatedText("weaponsmenu.wanttosave.yes"), (_, _) =>
        {
            AddPlayerSelection(player, selection);
            Api.PrintToChat(player, GetTranslatedText("weaponsmenu.saved"));
            Api.PrintToChat(player, GetTranslatedText("weaponsmenu.resetinfo.info"));
        });
        menu.AddMenuOption(GetTranslatedText("weaponsmenu.wanttosave.no"), (_, _) =>
        {
            Api.PrintToChat(player, GetTranslatedText("weaponsmenu.notsaved"));
        });

        menu.Open(player);
    }

    private static int GetActualRound()
    {
        return _gameRules?.RoundsPlayedThisPhase + 1 ?? 1;
    }
    
    private bool GetPlayerSelection(CCSPlayerController player, out WeaponSelection? selection)
    {
        if (!_playerSelection.TryGetValue(player.TeamNum, out var playerDict))
        {
            selection = null;
            return false;
        }

        if (playerDict.TryGetValue(player, out var weaponSelection))
        {
            selection = weaponSelection;
            return true;
        }

        selection = null;
        return false;
    }
    
    private void AddPlayerSelection(CCSPlayerController player, WeaponSelection selection)
    {
        if (!_playerSelection.TryGetValue(player.TeamNum, out var value))
        {
            value = [];
            _playerSelection[player.TeamNum] = value;
        }

        value[player] = selection;
    }
    
    private void RemovePlayerSelection(CCSPlayerController player)
    {
        // Collect team numbers that need to be removed after the player is removed
        var teamsToRemove = new List<int>();

        foreach (var teamEntry in _playerSelection)
        {
            var teamNum = teamEntry.Key;
            var playerDict = teamEntry.Value;

            playerDict.Remove(player);

            if (playerDict.Count == 0)
                teamsToRemove.Add(teamNum);
        }

        foreach (var teamNum in teamsToRemove)
            _playerSelection.Remove(teamNum);
    }

    private static void RemoveWeapons(CCSPlayerController player)
    {
        if (player.PlayerPawn.Value?.WeaponServices == null || player.PlayerPawn.Value?.ItemServices == null)
            return;

        var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons.ToList();

        if (weapons.Count == 0)
            return;

        foreach (var weapon in weapons)
        {
            if (!weapon.IsValid || weapon.Value == null ||
                !weapon.Value.IsValid)
                continue;

            if (weapon.Value.Entity == null) continue;
            
            var weaponData = weapon.Value.As<CCSWeaponBase>().VData;
            if (weaponData == null)
                continue;

            if (weaponData.GearSlot is gear_slot_t.GEAR_SLOT_RIFLE or gear_slot_t.GEAR_SLOT_PISTOL)
            {
                weapon.Value?.AddEntityIOEvent("Kill", weapon.Value, null, "", 0.12f);
            }
        }
    }
}


public class WeaponsMenuSettings
{
    public required Dictionary<string, WeaponSelection> CT { get; init; }
    public required Dictionary<string, WeaponSelection> T { get; init; }
}

public class WeaponSelection(List<string> weapons, int round)
{
    public required List<string> Weapons { get; init; } = weapons;
    public required int Round { get; init; } = round;
}