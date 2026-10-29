using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIPCore;

public class VIP_DefuseKit : BasePlugin
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[VIP] DefuseKit";
    public override string ModuleVersion => "v2.0.0";
    private IVipCoreApi? _api;
    private DefuseKit? _defuseKit;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get() ?? throw new Exception("Vip api not found");

        _defuseKit = new DefuseKit(this, _api);
    }

    public override void Unload(bool hotReload)
    {
        _defuseKit?.Dispose();
    }

    public class DefuseKit : VipFeature
    {
        public DefuseKit(VIP_DefuseKit defuseKit, IVipCoreApi api) : base("DefuseKit", api)
        {
        }

        public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
        {
            if (!IsFeatureEnabled(player)) return;

            if (player.Team == CsTeam.Terrorist)
                return;

            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()
                ?.GameRules;
            if (gameRules == null || gameRules.WarmupPeriod)
                return;

            var pawn = player.PlayerPawn.Value;
            if (pawn?.ItemServices == null)
                return;

            var itemServices = new CCSPlayer_ItemServices(pawn.ItemServices.Handle);
            itemServices.HasDefuser = true;
        }
    }
}