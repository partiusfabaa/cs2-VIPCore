using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using Modularity;
using VipCoreApi;

namespace VIP_Health;

public class VipHealth : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Health";
    public override string ModuleVersion => "v1.0.1";

    private IVipCoreApi _api = null!;
    private Health _health;
    
    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _health = new Health(_api);
        _api.RegisterFeature(_health);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_health);
    }
}

public class Health : VipFeatureBase
{
    public override string Feature => "Health";
    
    public Health(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        VirtualFunctions.CBasePlayerController_SetPawnFunc.Invoke(player, playerPawn, true, false);
        VirtualFunction.CreateVoid<CCSPlayerController>(player.Handle,
            GameData.GetOffset("CCSPlayerController_Respawn"))(player);
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not IVipCoreApi.FeatureState.Enabled) return;

        var playerPawn = player.PlayerPawn.Value;

        var healthValue = GetFeatureValue<int>(player);

        if (healthValue <= 0 || playerPawn == null) return;
        
        playerPawn.Health = healthValue;
        playerPawn.MaxHealth = healthValue;
        Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
    }
}