using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;
using System.Globalization;

namespace VIP_Vampirism;

public class VipVampirism : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Vampirism";
    public override string ModuleVersion => "v2.0.0";

    private Vampirism? _vampirism;
    private IVipCoreApi? _api;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _vampirism = new Vampirism(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _vampirism?.Dispose();
    }
}

public class Vampirism : VipFeature<float>
{
    public Vampirism(VipVampirism vipVampirism, IVipCoreApi api) : base("Vampirism", api)
    {
        vipVampirism.RegisterEventHandler<EventPlayerHurt>((@event, _) =>
        {
            var attacker = @event.Attacker;

            if (attacker is null || !attacker.IsValid) return HookResult.Continue;

            if (attacker == @event.Userid) return HookResult.Continue;

            if (@event.Userid is { IsValid: true } victim && victim.Team == attacker.Team) return HookResult.Continue;

            if (IsFeatureEnabled(attacker) && attacker.PawnIsAlive)
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

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
            args.Display = $"[{GetValue(args.Controller).ToString("0.#", CultureInfo.InvariantCulture)}%]";
    }
}