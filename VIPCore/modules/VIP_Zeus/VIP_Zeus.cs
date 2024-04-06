using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;

namespace VIP_Zeus;

public class VipZeus : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Zeus";
    public override string ModuleVersion => "v1.0";
    
    private Zeus? _zeus;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _zeus = new Zeus(_api);
        _api.RegisterFeature(_zeus);
    }
    
    public override void Unload(bool hotReload)
    {   
        if(_api != null && _zeus != null)
        {
            _api?.UnRegisterFeature(_zeus);
        }
    }
}

public class Zeus : VipFeatureBase
{
    public override string Feature => "Zeus";
    
    public Zeus(IVipCoreApi api) : base(api)
    {
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!PlayerHasFeature(player)) return;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return;
        
        if(player.TeamNum == 3 || player.TeamNum == 2)
        {
            player.GiveNamedItem("weapon_taser");
        }
    }
}
