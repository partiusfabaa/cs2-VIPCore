using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_FeaturesManager;

public class FeatureConfig
{
    public int DefaultState { get; set; }
    public List<int> Rounds { get; set; } = new();
    public bool DisableOnPistolRound { get; set; }
    public bool DisableOnWarmup { get; set; }
}

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] FeaturesManager";
    public override string ModuleVersion => "v2.0.0";

    private int _currentRound;
    private IVipCoreApi? _api;
    private Dictionary<string, FeatureConfig> _features = new();
    private readonly Dictionary<CCSPlayerController, Dictionary<string, FeatureState>> _playerFeatureStates = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = IVipCoreApi.Capability.Get();
        if (_api == null) return;

        _features = _api.LoadConfig<Dictionary<string, FeatureConfig>>("vip_featuresmanager");
        RegisterEventHandlers();
    }

    private void RegisterEventHandlers()
    {
        RegisterListener<Listeners.OnMapStart>(name =>
        {
            _currentRound = 0;
            _playerFeatureStates.Clear();
        });

        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        _api!.OnPlayerAuthorized += OnPlayerAuthorized;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (IsWarmup()) return HookResult.Continue;
        
        _currentRound++;
        Logger.LogInformation($"Current round: {_currentRound}");

        foreach (var (featureName, config) in _features)
        {
            if (ShouldSkipFeature(config)) continue;

            var targetState = config.Rounds.Contains(_currentRound)
                ? FeatureState.Enabled
                : (FeatureState)config.DefaultState;

            ToggleFeature(featureName, targetState);
        }

        return HookResult.Continue;
    }

    private void ToggleFeature(string featureName, FeatureState newState)
    {
        foreach (var player in Utilities.GetPlayers().Where(p => _api!.IsPlayerVip(p)))
        {
            _api!.SetPlayerFeatureState(player, featureName, newState);
        }
    }

    private bool ShouldSkipFeature(FeatureConfig config)
    {
        if (config.DisableOnPistolRound && _api!.IsPistolRound())
            return true;

        if (config.DisableOnWarmup && IsWarmup())
            return true;

        return false;
    }

    private bool IsWarmup()
    {
        var gameRulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").ToList();
        if (gameRulesProxy.Count == 0) return false;
        
        var gameRules = gameRulesProxy.First().GameRules;
        return gameRules?.WarmupPeriod ?? false;
    }

    private void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
        if (!_api!.IsPlayerVip(player)) return;

        var states = new Dictionary<string, FeatureState>();
        foreach (var (featureName, config) in _features)
        {
            states[featureName] = (FeatureState)config.DefaultState;
            _api.SetPlayerFeatureState(player, featureName, (FeatureState)config.DefaultState);
        }

        _playerFeatureStates[player] = states;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _playerFeatureStates.Remove(player);
        }

        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        if (_api != null)
        {
            _api.OnPlayerAuthorized -= OnPlayerAuthorized;
        }
    }
}