using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_SmokeColor;

public class VipSmokeColor : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Smoke Color";
    public override string ModuleVersion => "v2.0.0";


    private SmokeColor? _smokeColor;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _smokeColor = new SmokeColor(this, _api);
        };
    }

    public override void Unload(bool hotReload)
    {
        _smokeColor?.Dispose();
    }
}

public class SmokeColor : VipFeature<int[]>
{
    public SmokeColor(VipSmokeColor smokeColor, IVipCoreApi api) : base("SmokeColor", api)
    {
        smokeColor.RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
    }
    
    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity.DesignerName != "smokegrenade_projectile") return;

        var smokeGrenade = new CSmokeGrenadeProjectile(entity.Handle);
        if (smokeGrenade.Handle == IntPtr.Zero) return;


        Server.NextFrame(() =>
        {
            var throwerValue = smokeGrenade.Thrower.Value;
            if (throwerValue == null) return;
            var throwerValueController = throwerValue.Controller.Value;
            if (throwerValueController == null) return;
            var controller = new CCSPlayerController(throwerValueController.Handle);

            if (!IsPlayerVip(controller)) return;
            if (!PlayerHasFeature(controller)) return;
            if (GetPlayerFeatureState(controller) is not FeatureState.Enabled)
                return;
            
            var smokeColor = GetValue(controller)!;

            smokeGrenade.SmokeColor.X = smokeColor[0] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[0];
            smokeGrenade.SmokeColor.Y = smokeColor[1] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[1];
            smokeGrenade.SmokeColor.Z = smokeColor[2] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[2];
        });
    }
}