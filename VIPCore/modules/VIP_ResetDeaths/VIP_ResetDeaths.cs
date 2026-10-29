using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using VipCoreApi;

namespace VIP_ResetDeaths;

public class VipResetDeaths : BasePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] ResetDeaths";
    public override string ModuleVersion => "v2.0.0";

    private ResetDeaths? _resetDeaths;
    private IVipCoreApi? _api;
    
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _resetDeaths = new ResetDeaths(this, _api);
        };
    }
    
    public override void Unload(bool hotReload)
    {
        _resetDeaths?.Dispose();
    }
}

public class ResetDeaths : VipFeature<bool>
{
    public ResetDeaths(VipResetDeaths vipResetDeaths, IVipCoreApi api) : base("ResetDeaths", api)
    {
        vipResetDeaths.AddCommand("css_rd", "ResetDeaths", OnResetDeathsCommand);
        vipResetDeaths.AddCommand("css_resetdeaths", "ResetDeaths", OnResetDeathsCommand);
        vipResetDeaths.AddCommand("rd", "ResetDeaths", OnResetDeathsCommand);
    }

    private void OnResetDeathsCommand(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if (player == null) return;
        
        if (!IsPlayerVip(player) || !PlayerHasFeature(player))
        {
            PrintToChat(player, GetTranslatedText("vip.NoAccess"));
            return;
        }

        if (!IsFeatureEnabled(player)) return;

        if (player.ActionTrackingServices == null) return;
        
        var scoreDeaths = player.ActionTrackingServices!.MatchStats;
        var playerPawnValue = player.PlayerPawn.Value;
        var scoreValue = GetValue(player);

        if (!scoreValue) return;
        if (playerPawnValue == null) return;
        
        scoreDeaths.Deaths = 0;
        PrintToChat(player, GetTranslatedText("vip.ResetDeaths"));
    }
}