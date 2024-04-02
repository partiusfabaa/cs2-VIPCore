using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Defuser;

public class VipDefuser : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Defuser";
    public override string ModuleVersion => "v1.0";
    
    private Defuser _defuser;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _defuser = new Defuser(_api);
            _api.RegisterFeature(_defuser);
        };
    }
    
    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_defuser);
    }
}

public class Defuser : VipFeatureBase
{
    public override string Feature => "Defuser";
    
    public Defuser(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;
        
        if(player.TeamNum == 3)
        {
            player.GiveNamedItem("item_defuser");
        }
    }
}
