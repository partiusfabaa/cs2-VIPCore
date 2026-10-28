using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using RanksApi;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_ExperienceMultiplier;

public class VipExperienceMultiplier : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Experience Multiplier";
    public override string ModuleVersion => "v2.0.0";

    private IVipCoreApi? _vipApi;
    private IRanksApi? _ranksApi;
    private ExperienceMultiplier? _experienceMultiplier;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _vipApi = PluginCapability.Get();
        if (_vipApi == null) return;

        _ranksApi = IRanksApi.Capability.Get();
        if (_ranksApi is null) return;

        _experienceMultiplier = new ExperienceMultiplier(this, _vipApi, _ranksApi);
    }

    public override void Unload(bool hotReload)
    {
        _experienceMultiplier?.Dispose();
    }
}

public class ExperienceMultiplier : VipFeature<float>
{
    private readonly float[] _multiplier = new float[70];

    public ExperienceMultiplier(BasePlugin basePlugin, IVipCoreApi api, IRanksApi ranksApi) : base("exp_multiplier", api)
    {
        ranksApi.PlayerExperienceChanged += (controller, i) => (int)(i * _multiplier[controller.Slot]);

        basePlugin.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is null) return HookResult.Continue;

            _multiplier[player.Slot] = 1;
            return HookResult.Continue;
        });
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        _multiplier[player.Slot] = GetPlayerFeatureState(player) is FeatureState.Enabled ? GetValue(player) : 1;
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !PlayerHasFeature(player)) return;

        _multiplier[player.Slot] = GetPlayerFeatureState(player) is FeatureState.Enabled ? GetValue(player) : 1;
    }
}