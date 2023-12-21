using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Modularity;
using VipCoreApi;

namespace VIP_ResetDeaths;

public class VipArmor : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "WodiX";
    public override string ModuleName => "[VIP] ResetDeaths";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "ResetDeaths";

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, IVipCoreApi.FeatureType.Hide);
    }

    [ConsoleCommand("rd", "ResetDeaths")]
    public void OnResetDeathsCommand(CCSPlayerController player, CommandInfo command)
    {
        if (!_api.IsClientVip(player) || !_api.PlayerHasFeature(player, Feature))
        {
            _api.PrintToChat(player, _api.GetTranslatedText("vip.NoAccess"));
            return;
        }
        if (_api.GetPlayerFeatureState(player, Feature) is IVipCoreApi.FeatureState.NoAccess) return;

        if (player.ActionTrackingServices == null) return;

        var scoreDeaths = player.ActionTrackingServices!.MatchStats;

        var playerPawnValue = player.PlayerPawn.Value;

        

        var scoreValue = _api.GetFeatureValue<bool>(player, Feature);

        if (scoreValue)
        {
            if (playerPawnValue != null)
            {
                scoreDeaths.Deaths = 0;
                _api.PrintToChat(player, _api.GetTranslatedText("vip.ResetDeaths"));
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}