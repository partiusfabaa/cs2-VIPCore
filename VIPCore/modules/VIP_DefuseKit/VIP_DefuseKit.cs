using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;

namespace VIPCore;

public class VIP_DefuseKit : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[VIP] DefuseKit";
    public override string ModuleVersion => "1.0";
    private IVipCoreApi? VIP_API;
    private DefuseKit? defuseKit;
    private PluginCapability<IVipCoreApi> pluginCapabilty { get; } = new("vipcore:core");
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        VIP_API = pluginCapabilty.Get() ?? throw new Exception("Vip api not found");

        VIP_API.OnCoreReady += () =>
        {
            defuseKit = new DefuseKit(this, VIP_API);
            VIP_API.RegisterFeature(defuseKit);
        };
    }
    public override void Unload(bool hotReload)
    {
        VIP_API?.UnRegisterFeature(defuseKit);
    }
    public class DefuseKit : VipFeatureBase
    {
        public override string Feature => "DefuseKit";

        public DefuseKit(VIP_DefuseKit defuseKit, IVipCoreApi api) : base(api)
        {

        }
        public override void OnPlayerSpawn(CCSPlayerController player)
        {
            if (!IsClientVip(player) ||
                !PlayerHasFeature(player) ||
                GetPlayerFeatureState(player) is not IVipCoreApi.FeatureState.Enabled) return;

            if (player.Team == CsTeam.Terrorist)
                return;

            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            if (gameRules == null || gameRules.WarmupPeriod)
                return;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null)
                return;

            var itemServices = new CCSPlayer_ItemServices(pawn.ItemServices!.Handle);
            itemServices.HasDefuser = true;
        }
    }
}