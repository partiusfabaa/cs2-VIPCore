using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;

namespace VIP_Vampirism;

public class VipVampirism : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Vampirism";
    public override string ModuleVersion => "v1.0.0";

    private Vampirism _vampirism;
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _vampirism = new Vampirism(this, _api);
        _api.RegisterFeature(_vampirism);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_vampirism);
    }
}

public class Vampirism : VipFeatureBase
{
    public override string Feature => "Vampirism";

    public Vampirism(VipVampirism vipVampirism, IVipCoreApi api) : base(api)
    {
        vipVampirism.RegisterEventHandler<EventPlayerHurt>((@event, _) =>
        {
            var attacker = @event.Attacker;

            if (!attacker.IsValid) return HookResult.Continue;

            if (attacker == @event.Userid) return HookResult.Continue;

            if (IsClientVip(attacker) && PlayerHasFeature(attacker) && attacker.PawnIsAlive)
            {
                if (GetPlayerFeatureState(attacker) is not IVipCoreApi.FeatureState.Enabled)
                    return HookResult.Continue;

                var attackerPawn = attacker.PlayerPawn.Value;
                if (attackerPawn == null) return HookResult.Continue;

                var health = attackerPawn.Health +
                             (int)float.Round(@event.DmgHealth * GetFeatureValue<float>(attacker) / 100.0f);

                if (health > attackerPawn.MaxHealth)
                    health = attackerPawn.MaxHealth;

                attackerPawn.Health = health;
                Utilities.SetStateChanged(attackerPawn, "CBaseEntity", "m_iHealth");
            }

            return HookResult.Continue;
        });
    }
}