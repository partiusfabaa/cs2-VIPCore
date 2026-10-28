using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Services;
using VipCoreApi;
using VipCoreApi.Enums;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIPCore.Player;

public class PlayersManager
{
    private readonly Plugin _plugin;
    private readonly DatabaseService _database;
    private readonly CookieService _cookies;
    private readonly FeatureManager _featureManager;
    private readonly Config<GroupsConfig> _groupsConfig;
    private readonly Config<VipConfig> _coreConfig;

    private readonly VipPlayer?[] _players = new VipPlayer?[65];

    public event Action<VipPlayer>? PlayerLoaded;
    public event Action<VipPlayer, string>? PlayerRemoved;
    public event Action<VipPlayer>? PlayerDisconnect;

    public PlayersManager(
        Plugin plugin,
        DatabaseService database,
        CookieService cookies,
        FeatureManager featureManager,
        Config<GroupsConfig> groupsConfig,
        Config<VipConfig> coreConfig)
    {
        _plugin = plugin;
        _database = database;
        _cookies = cookies;
        _featureManager = featureManager;
        _groupsConfig = groupsConfig;
        _coreConfig = coreConfig;

        featureManager.FeatureRegistered += feature =>
        {
            foreach (var player in Players)
                ApplyFeatureState(player, feature);
        };
    }

    public IEnumerable<VipPlayer> Players => _players.OfType<VipPlayer>();

    public bool TryGetPlayer(CCSPlayerController player, [NotNullWhen(true)] out VipPlayer? vipPlayer)
    {
        vipPlayer = null;
        if (player is not { IsValid: true }) return false;

        var slot = player.Slot;
        vipPlayer = slot >= 0 && slot < _players.Length ? _players[slot] : null;
        return vipPlayer != null;
    }

    public void OnClientAuthorized(int slot, SteamID steamId)
    {
        var controller = Utilities.GetPlayerFromSlot(slot);
        if (controller is null || !controller.IsValid || controller.IsBot) return;

        if (_players[slot] is { } previous)
            previous.Disconnected = true;

        var player = new VipPlayer(controller, steamId);
        _players[slot] = player;

        Reload(player);
    }

    public void OnClientDisconnect(int slot)
    {
        var player = _players[slot];
        if (player is null) return;

        if (player.IsVip)
        {
            foreach (var feature in _featureManager.GetFeatures().Where(f => f.Type is FeatureType.Toggle))
            {
                if (player.FeatureStates.TryGetValue(feature.Name, out var state))
                    _cookies.Set(player.SteamId.SteamId64, feature.Name, (int)state);
            }
        }

        PlayerDisconnect?.Invoke(player);

        player.Disconnected = true;
        _players[slot] = null;

        if (player.Data is null || player.IsTemporary) return;

        var accountId = player.SteamId.AccountId;
        var name = player.Controller.IsValid ? player.Controller.PlayerName : player.Data.Name;
        Task.Run(() => _database.UpdateLastVisitAsync(accountId, name));
    }

    public void LoadConnectedPlayers()
    {
        foreach (var controller in Utilities.GetPlayers())
        {
            if (controller.IsBot || controller.AuthorizedSteamID is not { } steamId) continue;

            OnClientAuthorized(controller.Slot, steamId);
        }
    }

    public void ReloadAll()
    {
        foreach (var player in Players)
            Reload(player);
    }

    public void Reload(VipPlayer player)
    {
        Task.Run(() => LoadAsync(player));
    }

    private async Task LoadAsync(VipPlayer player)
    {
        try
        {
            var accountId = player.SteamId.AccountId;

            await _database.RemoveExpiredUserAsync(accountId);
            var data = await _database.GetUserAsync(accountId);

            await Server.NextFrameAsync(() =>
            {
                if (player.Disconnected || !player.Controller.IsValid) return;

                if (data is null)
                {
                    if (!player.IsTemporary)
                        Remove(player);
                    return;
                }

                SetVip(player, data, isTemporary: false);
                PrintWelcomeWhenConnected(player);
            });
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError("Failed to load VIP data of {steamId}: {e}", player.SteamId.SteamId64, e);
        }
    }

    public void SetVip(VipPlayer player, VipData data, bool isTemporary)
    {
        player.Clear();
        player.Data = data;
        player.IsTemporary = isTemporary;
        player.Features = ResolveGroup(data.Group);

        ApplyFeatureStates(player);

        if (player.IsVip)
            PlayerLoaded?.Invoke(player);
    }

    public bool Remove(VipPlayer player, bool notify = false)
    {
        if (player.Data is null) return false;

        var group = player.Data.Group;
        PlayerRemoved?.Invoke(player, group);
        player.Clear();

        if (notify)
            _plugin.PrintToChat(player.Controller, _plugin.Translate(player.Controller, "vip.Removed", group));

        return true;
    }

    public void RefreshGroups()
    {
        foreach (var player in Players.Where(p => p.Data != null))
        {
            player.Features = ResolveGroup(player.Group);
            player.FeatureValues.Clear();
            ApplyFeatureStates(player);
        }
    }

    public void RemoveExpired()
    {
        foreach (var player in Players.ToList())
        {
            if (player.Data is not { IsExpired: true } data) continue;

            var isTemporary = player.IsTemporary;
            Remove(player);

            _plugin.PrintToChat(player.Controller, _plugin.Translate(player.Controller, "vip.Expired", data.Group));
            _plugin.LogInfo("User '{name} [{accountId}]' has been removed due to expired VIP status.", data.Name,
                data.AccountId);

            if (!isTemporary)
                Task.Run(() => _database.RemoveExpiredUserAsync(data.AccountId));
        }
    }

    public void GiveVip(CCSPlayerController? controller, VipData data, bool isTemporary = false, bool notify = false)
    {
        if (!isTemporary)
            Task.Run(() => _database.UpsertUserAsync(data));

        if (controller is null || !TryGetPlayer(controller, out var player)) return;

        SetVip(player, data, isTemporary);

        if (notify)
            PrintWelcome(player);
    }

    public void UpdateVip(int accountId, CCSPlayerController? controller, string? name, string? group, long? expires,
        bool notify = false)
    {
        if (controller != null && TryGetPlayer(controller, out var player) && player.Data is { } data)
        {
            if (name != null)
                data.Name = name;

            if (group != null && group != data.Group)
            {
                data.Group = group;
                SetVip(player, data, player.IsTemporary);

                if (notify)
                    _plugin.PrintToChat(controller, _plugin.Translate(controller, "vip.GroupChanged", group));
            }

            if (expires != null)
            {
                data.Expires = expires.Value;

                if (notify)
                    _plugin.PrintToChat(controller,
                        $"{_plugin.Translate(controller, "vip.TimeChanged")} {ExpiresText(controller, data.Expires)}");
            }

            if (player.IsTemporary) return;
        }

        Task.Run(() => _database.UpdateUserAsync(accountId, name, group, expires));
    }

    public void RemoveVip(int accountId, CCSPlayerController? controller, bool notify = false)
    {
        var player = controller != null && TryGetPlayer(controller, out var found) ? found : null;
        var isTemporary = player is { IsTemporary: true };

        if (player != null)
            Remove(player, notify);

        if (!isTemporary)
            Task.Run(() => _database.RemoveUserAsync(accountId));
    }

    public VipData CreateVipData(int accountId, string name, string group, long expires) => new()
    {
        AccountId = accountId,
        Name = name,
        Group = group,
        Expires = expires,
        LastVisit = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ServerId = _coreConfig.Value.ServerId
    };

    public void PrintWelcome(VipPlayer player)
    {
        if (player.Data is not { } data) return;

        var controller = player.Controller;
        var welcome = _plugin.Translate(controller, "vip.WelcomeToTheServer", data.Name).TrimEnd();

        _plugin.PrintToChat(controller, data.Expires == 0
            ? welcome
            : $"{welcome} {_plugin.Translate(controller, "vip.Expires", data.Group, FormatTime(data.Expires))}");
    }

    private void PrintWelcomeWhenConnected(VipPlayer player)
    {
        Timer? timer = null;
        timer = _plugin.AddTimer(1.0f, () =>
        {
            if (player.Disconnected || !player.Controller.IsValid)
            {
                timer?.Kill();
                return;
            }

            if (player.Controller.Connected != PlayerConnectedState.Connected) return;

            timer?.Kill();
            PrintWelcome(player);
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    private string ExpiresText(CCSPlayerController controller, long expires)
    {
        return expires == 0
            ? _plugin.Translate(controller, "vip.Permanent")
            : _plugin.Translate(controller, "vip.Until", FormatTime(expires));
    }

    private static string FormatTime(long unixTime)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixTime).ToString("G");
    }

    private Dictionary<string, object>? ResolveGroup(string group)
    {
        try
        {
            var features = _groupsConfig.Value.ResolveGroup(group);
            if (features is null)
                _plugin.LogWarning("VIP group '{group}' was not found in vip.json", group);

            return features;
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError("Failed to resolve VIP group '{group}': {e}", group, e.Message);
            return null;
        }
    }

    private void ApplyFeatureStates(VipPlayer player)
    {
        player.FeatureStates.Clear();

        foreach (var feature in _featureManager.GetFeatures())
            ApplyFeatureState(player, feature);
    }

    private void ApplyFeatureState(VipPlayer player, VipFeature feature)
    {
        if (player.Data is null) return;

        if (!player.HasFeature(feature.Name))
        {
            player.FeatureStates[feature.Name] = FeatureState.NoAccess;
            return;
        }

        var state = _cookies.TryGet<int>(player.SteamId.SteamId64, feature.Name, out var cookie)
            ? (FeatureState)cookie
            : feature.State;

        player.FeatureStates[feature.Name] = state is FeatureState.NoAccess ? FeatureState.Enabled : state;
    }
}
