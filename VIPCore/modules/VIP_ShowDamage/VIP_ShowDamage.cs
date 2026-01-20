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
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "Shows damage dealt to enemies in the center text for VIP players";

    private ShowDamageFeature? _feature;
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
        };
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null && _feature != null)
        {
            _api.UnRegisterFeature(_feature);
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

    private ShowDamageConfig _config;

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

        // Check if the attacker has the feature active (VIP and enabled)
        if (!PlayerHasFeature(ev.Attacker))
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

    // Optional: Dacă vrei toggle în meniul VIP
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
    }
}

public class ShowDamageConfig
{
    public bool ShowArmorDmg { get; set; } = true;
    public bool HideDamage { get; set; } = false;
    public string AdminGroup { get; set; } = string.Empty;  // Kept for compatibility, but not used in VIP mode
}