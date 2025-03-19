using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_Vampirism;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Vampirism";
    public override string ModuleVersion => "v2.0.0";

    private Vampirism? _vampirism;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _vampirism = new Vampirism(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _vampirism?.Dispose();
    }
}

public class Vampirism : VipFeature<float>
{
    public Vampirism(Plugin plugin, IVipCoreApi api) : base("Vampirism", api)
    {
        plugin.RegisterEventHandler<EventPlayerHurt>((@event, _) =>
        {
            var attacker = @event.Attacker;

            if (attacker is null || !attacker.IsValid) return HookResult.Continue;

            if (attacker == @event.Userid) return HookResult.Continue;

            if (IsPlayerValid(attacker) && attacker.PawnIsAlive)
            {
                var attackerPawn = attacker.PlayerPawn.Value;
                if (attackerPawn == null) return HookResult.Continue;

                var health = attackerPawn.Health +
                             (int)float.Round(@event.DmgHealth * GetValue(attacker) / 100.0f);

                if (health > attackerPawn.MaxHealth)
                    health = attackerPawn.MaxHealth;

                attackerPawn.Health = health;
                Utilities.SetStateChanged(attackerPawn, "CBaseEntity", "m_iHealth");
            }

            return HookResult.Continue;
        });
    }
}