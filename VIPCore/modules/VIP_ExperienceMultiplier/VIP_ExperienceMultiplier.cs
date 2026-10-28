using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using RanksApi;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;
using System.Globalization;

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
    private readonly float[] _multiplier = Enumerable.Repeat(1f, 70).ToArray();

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

    public override void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        UpdateMultiplier(player);
    }

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        _multiplier[player.Slot] = 1;
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        UpdateMultiplier(player);
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        UpdateMultiplier(player);
    }

    private void UpdateMultiplier(CCSPlayerController player)
    {
        _multiplier[player.Slot] = IsFeatureEnabled(player) ? GetValue(player) : 1;
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
            args.Display = $"[x{GetValue(args.Controller).ToString("0.##", CultureInfo.InvariantCulture)}]";
    }
}