using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using VipCoreApi;

namespace VIP_SmokeColor;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Smoke Color";
    public override string ModuleVersion => "v2.0.0";

    private SmokeColor? _smokeColor;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _smokeColor = new SmokeColor(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _smokeColor?.Dispose();
    }
}

public class SmokeColor : VipFeature<int[]>
{
    public SmokeColor(Plugin smokeColor, IVipCoreApi api) : base("SmokeColor", api)
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
            var throwerValue = smokeGrenade.Thrower.Value?.Controller.Value;
            if (throwerValue == null) return;
            var controller = new CCSPlayerController(throwerValue.Handle);

            if (!IsPlayerValid(controller)) return;
            var smokeColor = GetValue(controller);
            if (smokeColor is null || smokeColor.Length == 0) return;

            for (var i = 0; i < smokeColor.Length; i++)
                smokeGrenade.SmokeColor[i] = smokeColor[i] == -1 ? Random.Shared.NextSingle() * 255.0f : smokeColor[i];
        });
    }
}