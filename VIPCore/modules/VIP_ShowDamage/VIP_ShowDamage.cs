using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text;
using VipCoreApi;
using CounterStrikeSharp.API.Core.Capabilities;

namespace VIP_ShowDamage;

public class VIP_ShowDamage : BasePlugin
{
    public override string ModuleAuthor => "GSM-RO";
    public override string ModuleName => "[VIP] ShowDamage";
    public override string ModuleVersion => "1.0.2";
    public override string ModuleDescription => "Shows damage dealt to enemies in the center text for VIP players + optional sound";

    private ShowDamageFeature? _feature;
    private SoundDamageFeature? _soundFeature;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _feature = new ShowDamageFeature(this, _api);
            _api.RegisterFeature(_feature);

            _soundFeature = new SoundDamageFeature(this, _api);
            _api.RegisterFeature(_soundFeature);
        };
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null)
        {
            if (_feature != null) _api.UnRegisterFeature(_feature);
            if (_soundFeature != null) _api.UnRegisterFeature(_soundFeature);
        }
    }
}

public class DamageDone
{
    public float Health { get; set; }
    public float Armor { get; set; }
}

public class ShowDamageFeature : VipFeatureBase
{
    public override string Feature => "ShowDamage";

    private readonly VIP_ShowDamage _plugin;
    private readonly Dictionary<int, DamageDone> _damageDone = new();
    private ConVar? _ffaEnabledConVar = null;
    public ShowDamageConfig _config;

    public bool FFAEnabled => _ffaEnabledConVar?.GetPrimitiveValue<bool>() ?? false;

    public ShowDamageFeature(VIP_ShowDamage plugin, IVipCoreApi api) : base(api)
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
            if (!Api.IsClientVip(player))
            {
                _damageDone.Remove(attackerUserId);
                return;
            }

            try
            {
                // Main check: is ShowDamage actually enabled?
                if (Api.GetPlayerFeatureState(player, "ShowDamage") != IVipCoreApi.FeatureState.Enabled)
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
                    Api.GetPlayerFeatureState(player, "SoundDMG") == IVipCoreApi.FeatureState.Enabled)
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
        if (!PlayerHasFeature(ev.Attacker) ||
            Api.GetPlayerFeatureState(ev.Attacker, "ShowDamage") != IVipCoreApi.FeatureState.Enabled)
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

    public override void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        player.PrintToChat(state == IVipCoreApi.FeatureState.Enabled
            ? "ShowDamage enabled"
            : "ShowDamage disabled");
    }
}

public class SoundDamageFeature : VipFeatureBase
{
    public override string Feature => "SoundDMG";

    public SoundDamageFeature(VIP_ShowDamage plugin, IVipCoreApi api) : base(api) { }

    public override void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        player.PrintToChat(state == IVipCoreApi.FeatureState.Enabled
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