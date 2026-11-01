using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text;
using System.Text.Json;
using VipCoreApi;
using VipCoreApi.Enums;
using CounterStrikeSharp.API.Core.Capabilities;

namespace VIP_ShowDamage;

public class VIP_ShowDamage : BasePlugin
{
    public override string ModuleAuthor => "GSM-RO";
    public override string ModuleName => "[VIP] ShowDamage";
    public override string ModuleVersion => "v2.0.1";

    public override string ModuleDescription =>
        "Shows damage dealt to enemies in the center text for VIP players + optional sound";

    private ShowDamageFeature? _feature;
    private SoundDamageFeature? _soundFeature;
    private IVipCoreApi? _api;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _feature = new ShowDamageFeature(this, _api);
        _soundFeature = new SoundDamageFeature(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _feature?.Dispose();
        _soundFeature?.Dispose();
    }
}

public static class FeatureValue
{
    // vip.json value of an on/off feature: 0 / false turns it off, any other value (1, true) turns it on
    public static bool IsOn(IVipCoreApi api, CCSPlayerController player, string feature)
    {
        var value = api.GetFeatureValue<JsonElement>(player, feature);
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble() != 0,
            JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
            _ => true
        };
    }
}

public class DamageDone
{
    public float Health { get; set; }
    public float Armor { get; set; }
}

public class ShowDamageFeature : VipFeature
{
    private readonly VIP_ShowDamage _plugin;
    private readonly Dictionary<int, DamageDone> _damageDone = new();
    private ConVar? _ffaEnabledConVar = null;
    public ShowDamageConfig _config;

    public bool FFAEnabled => _ffaEnabledConVar?.GetPrimitiveValue<bool>() ?? false;

    public ShowDamageFeature(VIP_ShowDamage plugin, IVipCoreApi api) : base("ShowDamage", api)
    {
        _plugin = plugin;
        _config = LoadConfig<ShowDamageConfig>("VIP_ShowDamage");
        _ffaEnabledConVar = ConVar.Find("mp_teammates_are_enemies");
        _plugin.RegisterEventHandler<EventPlayerHurt>(EventPlayerHurt);
    }

    private Action BuildCallback(int attackerUserId) =>
        () =>
        {
            if (!_damageDone.TryGetValue(attackerUserId, out var dmg)) return;

            var player = Utilities.GetPlayerFromUserid(attackerUserId);
            if (player is null || !player.IsValid)
            {
                _damageDone.Remove(attackerUserId);
                return;
            }

            // Safety check: player still in VIPCore
            if (!Api.IsPlayerVip(player))
            {
                _damageDone.Remove(attackerUserId);
                return;
            }

            try
            {
                // Main check: is ShowDamage actually enabled?
                if (Api.GetPlayerFeatureState(player, "ShowDamage") != FeatureState.Enabled ||
                    !FeatureValue.IsOn(Api, player, "ShowDamage"))
                {
                    _damageDone.Remove(attackerUserId);
                    return;
                }

                // Show damage text
                var builder = new StringBuilder();
                builder.Append($"-{dmg.Health} HP");
                if (_config.ShowArmorDmg)
                    builder.Append($"\n-{dmg.Armor} Armor");

                player.PrintToCenter(builder.ToString());

                // Play sound only if SoundDMG is enabled
                if (Api.PlayerHasFeature(player, "SoundDMG") &&
                    Api.GetPlayerFeatureState(player, "SoundDMG") == FeatureState.Enabled &&
                    FeatureValue.IsOn(Api, player, "SoundDMG"))
                {
                    player.ExecuteClientCommand($"play {_config.SoundPath}");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("player not found"))
            {
                // Player disconnected between damage and timer → silent ignore
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VIP_ShowDamage] Unexpected error in callback: {ex.Message}");
            }
            finally
            {
                _damageDone.Remove(attackerUserId);
            }
        };

    public HookResult EventPlayerHurt(EventPlayerHurt ev, GameEventInfo info)
    {
        if (ev.Attacker is null || ev.Userid is null || !ev.Attacker.IsValid)
            return HookResult.Continue;

        if (ev.Attacker.TeamNum == ev.Userid.TeamNum && !FFAEnabled)
            return HookResult.Continue;

        // Must have ShowDamage AND be enabled
        if (!IsFeatureEnabled(ev.Attacker) || !FeatureValue.IsOn(Api, ev.Attacker, Name))
            return HookResult.Continue;

        int attackerUserId = ev.Attacker.UserId!.Value;

        if (_config.HideDamage)
        {
            ev.Attacker.PrintToCenter("*");
            return HookResult.Continue;
        }

        if (_damageDone.TryGetValue(attackerUserId, out var existing))
        {
            existing.Health += ev.DmgHealth;
            existing.Armor += ev.DmgArmor;
        }
        else
        {
            _damageDone[attackerUserId] = new DamageDone
            {
                Health = ev.DmgHealth,
                Armor = ev.DmgArmor
            };

            _plugin.AddTimer(0.1f, BuildCallback(attackerUserId));
        }

        return HookResult.Continue;
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        player.PrintToChat(GetPlayerFeatureState(player) == FeatureState.Enabled
            ? "ShowDamage enabled"
            : "ShowDamage disabled");
    }
}

public class SoundDamageFeature : VipFeature
{
    public SoundDamageFeature(VIP_ShowDamage plugin, IVipCoreApi api) : base("SoundDMG", api)
    {
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        player.PrintToChat(GetPlayerFeatureState(player) == FeatureState.Enabled
            ? "SoundDMG enabled"
            : "SoundDMG disabled");
    }
}

public class ShowDamageConfig
{
    public bool ShowArmorDmg { get; set; } = true;
    public bool HideDamage { get; set; } = false;
    public string AdminGroup { get; set; } = string.Empty;
    public string SoundPath { get; set; } = "sounds/Training/timer_bell.vsnd_c";
}