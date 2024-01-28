using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Modularity;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Gravity;

public class VipGravity : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Gravity";
    public override string ModuleVersion => "1.0.1";

    private IVipCoreApi _api = null!;
    private Gravity _gravity;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _gravity = new Gravity(this, _api);
        _api.RegisterFeature(_gravity, selectItem: _gravity.OnSelectItem);
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_gravity);
    }
}

public class Gravity : VipFeatureBase
{
    public override string Feature => "Gravity";
    
    public Gravity(VipGravity vipGravity, IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is not FeatureState.Enabled) return;

        var playerPawnValue = player.PlayerPawn.Value;

        if (playerPawnValue == null) return;
        
        playerPawnValue.GravityScale = GetFeatureValue<float>(player);
    }
    
    public void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        var playerPawnValue = player.PlayerPawn.Value;
        
        if (state == FeatureState.Disabled)
        {
            if (playerPawnValue != null)
                playerPawnValue.GravityScale = 1.0f;
            return;
        }

        if (playerPawnValue != null)
            playerPawnValue.GravityScale = GetFeatureValue<float>(player);
    }
}