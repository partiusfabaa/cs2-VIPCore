using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Modularity;
using VipCoreApi;

namespace VIP_SmokeColor;

public class VipSmokeColor : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Smoke Color";
    public override string ModuleVersion => "v1.0.0";

    private IVipCoreApi _api = null!;
    private static readonly string Feature = "SmokeColor";

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
    }
    
    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature);
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
            if (throwerValue.Controller.Value == null) return;
            var throwerValueController = throwerValue.Controller.Value;
            var controller = new CCSPlayerController(throwerValueController.Handle);
            
            if (_api.GetPlayerFeatureState(controller, Feature) is IVipCoreApi.FeatureState.Disabled
                or IVipCoreApi.FeatureState.NoAccess) return;

            if (!_api.PlayerHasFeature(controller, Feature)) return;

            var smokeColor = _api.GetFeatureValue<int[]>(controller, Feature);

            smokeGrenade.SmokeColor.X = smokeColor[0] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[0];
            smokeGrenade.SmokeColor.Y = smokeColor[1] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[1];
            smokeGrenade.SmokeColor.Z = smokeColor[2] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[2];
        });
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}