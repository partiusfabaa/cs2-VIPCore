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
    public override string ModuleVersion => "1.0.1";
    public override string ModuleDescription => "Shows damage dealt to enemies in the center text for VIP players";

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
            if (_feature != null)
            {
                _api.UnRegisterFeature(_feature);
            }
            if (_soundFeature != null)
            {
                _api.UnRegisterFeature(_soundFeature);
            }
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
    private Dictionary<int, DamageDone> damageDone = new();
    private ConVar? ffaEnabledConVar = null;
    public ShowDamageConfig _config;

    public bool FFAEnabled
    {
        get
        {
            if (ffaEnabledConVar is null)
                return false;
            return ffaEnabledConVar.GetPrimitiveValue<bool>();
        }
    }

    public ShowDamageFeature(VIP_ShowDamage plugin, IVipCoreApi api) : base(api)
    {
        _plugin = plugin;
        _config = LoadConfig<ShowDamageConfig>("VIP_ShowDamage");  // Încarcă config-ul specific modulului
        ffaEnabledConVar = ConVar.Find("mp_teammates_are_enemies");
        _plugin.RegisterEventHandler<EventPlayerHurt>(EventPlayerHurt);
    }

    private Action BuildCallback(int attackerUserId) =>
        () =>
        {
            if (damageDone.ContainsKey(attackerUserId))
            {
                var player = Utilities.GetPlayerFromUserid(attackerUserId);
                if (player is not null && player.IsValid)
                {
                    // Debug: Print ShowDamage state
                    var showState = Api.GetPlayerFeatureState(player, "ShowDamage");
                    //player.PrintToChat($"Debug ShowDamage State: {showState}");

                    if (showState != IVipCoreApi.FeatureState.Enabled)
                    {
                        damageDone.Remove(attackerUserId);
                        return;
                    }

                    var dmg = damageDone[attackerUserId];
                    if (dmg is not null)
                    {
                        StringBuilder builder = new();
                        builder.Append($"-{dmg.Health} HP");
                        if (_config.ShowArmorDmg)
                        {
                            builder.Append($"\n-{dmg.Armor} Armor");
                        }
                        player.PrintToCenter(builder.ToString());

                        // Debug: Print SoundDMG state
                        bool hasSound = Api.PlayerHasFeature(player, "SoundDMG");
                        var soundState = Api.GetPlayerFeatureState(player, "SoundDMG");
                        //player.PrintToChat($"Debug SoundDMG: Has {hasSound}, State {soundState}");

                        // Play sound if has feature and enabled
                        if (hasSound && soundState == IVipCoreApi.FeatureState.Enabled)
                        {
                            player.ExecuteClientCommand($"play {_config.SoundPath}");
                        }
                    }
                }
                damageDone.Remove(attackerUserId);
            }
        };

    public HookResult EventPlayerHurt(EventPlayerHurt ev, GameEventInfo info)
    {
        if (ev.Attacker is null ||
            ev.Userid is null ||
            !ev.Attacker.IsValid ||
            (ev.Attacker.TeamNum == ev.Userid.TeamNum && !FFAEnabled))
            return HookResult.Continue;

        // Check if attacker has ShowDamage feature AND it is enabled
        if (!PlayerHasFeature(ev.Attacker) || Api.GetPlayerFeatureState(ev.Attacker, "ShowDamage") != IVipCoreApi.FeatureState.Enabled)
            return HookResult.Continue;

        int attackerUserId = ev.Attacker.UserId!.Value;

        if (_config.HideDamage)
        {
            ev.Attacker.PrintToCenter("*");
            return HookResult.Continue;
        }

        if (damageDone.ContainsKey(attackerUserId))
        {
            DamageDone? dmg = damageDone[attackerUserId];
            if (dmg is not null)
            {
                dmg.Health += ev.DmgHealth;
                dmg.Armor += ev.DmgArmor;
            }
        }
        else
        {
            damageDone.Add(attackerUserId, new DamageDone { Armor = ev.DmgArmor, Health = ev.DmgHealth });
            _plugin.AddTimer(0.1F, BuildCallback(attackerUserId), 0);
        }

        return HookResult.Continue;
    }

    public override void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        if (state == IVipCoreApi.FeatureState.Enabled)
        {
            player.PrintToChat("ShowDamage enabled");
        }
        else
        {
            player.PrintToChat("ShowDamage disabled");
        }
        // Debug after toggle
       // player.PrintToChat($"Debug ShowDamage after toggle: {state}");
    }
}

public class SoundDamageFeature : VipFeatureBase
{
    public override string Feature => "SoundDMG";

    public SoundDamageFeature(VIP_ShowDamage plugin, IVipCoreApi api) : base(api)
    {
    }

    public override void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        if (state == IVipCoreApi.FeatureState.Enabled)
        {
            player.PrintToChat("SoundDMG enabled");
        }
        else
        {
            player.PrintToChat("SoundDMG disabled");
        }
        // Debug after toggle
       // player.PrintToChat($"Debug SoundDMG after toggle: {state}");
    }
}

public class ShowDamageConfig
{
    public bool ShowArmorDmg { get; set; } = true;
    public bool HideDamage { get; set; } = false;
    public string AdminGroup { get; set; } = string.Empty;  // Kept for compatibility, but not used in VIP mode
    public string SoundPath { get; set; } = "sounds/Training/timer_bell.vsnd_c";
}