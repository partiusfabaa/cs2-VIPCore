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

    private const string Feature = "Vampirism";
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerHurt>((@event, _) =>
        {
            var attacker = @event.Attacker;

            if (!attacker.IsValid) return HookResult.Continue;

            if (attacker == @event.Userid) return HookResult.Continue;
            
            if (_api.IsClientVip(attacker) && _api.PlayerHasFeature(attacker, Feature) && attacker.PawnIsAlive)
            {
                if (_api.GetPlayerFeatureState(attacker, Feature) is not IVipCoreApi.FeatureState.Enabled)
                    return HookResult.Continue;

                var attackerPawn = attacker.PlayerPawn.Value;
                if (attackerPawn == null) return HookResult.Continue;

                var health = attackerPawn.Health + (int)float.Round(@event.DmgHealth * _api.GetFeatureValue<float>(attacker, Feature) / 100.0f);

                if (health > attackerPawn.MaxHealth)
                    health = attackerPawn.MaxHealth;

                attackerPawn.Health = health;
                Utilities.SetStateChanged(attackerPawn, "CBaseEntity", "m_iHealth");
            }

            return HookResult.Continue;
        });
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}