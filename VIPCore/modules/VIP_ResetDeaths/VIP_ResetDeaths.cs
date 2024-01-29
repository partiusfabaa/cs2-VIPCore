using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Modularity;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_ResetDeaths;

public class VipResetDeaths : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] ResetDeaths";
    public override string ModuleVersion => "v1.0.0";

    private ResetDeaths _resetDeaths;
    private IVipCoreApi _api = null!;
    private static readonly string Feature = "ResetDeaths";

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _resetDeaths = new ResetDeaths(this, _api);
        _api.RegisterFeature(_resetDeaths, FeatureType.Hide);
    }
    
    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_resetDeaths);
    }
}

public class ResetDeaths : VipFeatureBase
{
    public override string Feature => "ResetDeaths";

    public ResetDeaths(VipResetDeaths vipResetDeaths, IVipCoreApi api) : base(api)
    {
        vipResetDeaths.AddCommand("css_rd", "ResetDeaths", OnResetDeathsCommand);
        vipResetDeaths.AddCommand("css_resetdeaths", "ResetDeaths", OnResetDeathsCommand);
        vipResetDeaths.AddCommand("rd", "ResetDeaths", OnResetDeathsCommand);
    }

    private void OnResetDeathsCommand(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if (player == null) return;
        
        if (!IsClientVip(player) || !PlayerHasFeature(player))
        {
            PrintToChat(player, GetTranslatedText("vip.NoAccess"));
            return;
        }

        if (GetPlayerFeatureState(player) is FeatureState.NoAccess) return;

        if (player.ActionTrackingServices == null) return;
        
        var scoreDeaths = player.ActionTrackingServices!.MatchStats;
        var playerPawnValue = player.PlayerPawn.Value;
        var scoreValue = GetFeatureValue<bool>(player);

        if (!scoreValue) return;
        if (playerPawnValue == null) return;
        
        scoreDeaths.Deaths = 0;
        PrintToChat(player, GetTranslatedText("vip.ResetDeaths"));
    }
}