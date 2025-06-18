using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Services;
using VipCoreApi.Enums;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace VIPCore.Player;

public class PlayersManager
{
    private readonly Plugin _plugin;
    private readonly Lazy<VipCoreApi> _api;
    private readonly Config<GroupsConfig> _groupsConfig;
    private readonly DatabaseService _databaseService;

    public PlayerDataContainer<VipPlayer> Players { get; }

    public PlayersManager(
        IServiceProvider serviceProvider,
        Plugin plugin,
        Lazy<VipCoreApi> api,
        Config<GroupsConfig> groupsConfig,
        DatabaseService databaseService)
    {
        _plugin = plugin;
        _api = api;
        _groupsConfig = groupsConfig;
        _databaseService = databaseService;

        Players = new PlayerDataContainer<VipPlayer>(plugin, i =>
        {
            var player = serviceProvider.GetRequiredService<VipPlayer>();
            player.Controller = Utilities.GetPlayerFromSlot(i);
            player.OnDisconnect += OnDisconnect;

            return player;
        });

        plugin.RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
    }

    public void OnClientAuthorized(int slot, SteamID steamId)
    {
        var player = Players[slot]!;

        player.SteamId = steamId;
        Task.Run(() => OnClientAuthorizedAsync(player, steamId));
    }

    private async Task? OnClientAuthorizedAsync(VipPlayer player, SteamID steamId)
    {
        try
        {
            var data = await _databaseService.GetUserAsync(steamId.AccountId);
            if (data is null) return;

            player.Data = data;

            if (_groupsConfig.Value.TryGetValue(data.Group, out var group))
            {
                player.Group = group;
            }

            await Server.NextFrameAsync(() =>
            {
                var controller = player.Controller!;
                var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(data.Expires);
                SetClientFeature(player);

                Timer? timer = null;
                timer = _plugin.AddTimer(1.0f, () =>
                {
                    if (player.Disconnected)
                    {
                        timer!.Kill();
                        return;
                    }

                    if (!controller.IsValid || controller.Connected != PlayerConnectedState.PlayerConnected) return;

                    PrintToChat(player, _plugin.Localizer.ForPlayer(controller, "vip.WelcomeToTheServer", data.Name) +
                                        (data.Expires == 0
                                            ? string.Empty
                                            : _plugin.Localizer.ForPlayer(controller, "vip.Expires", data.Group,
                                                timeRemaining.ToString("G"))));

                    _api.Value.InvokeOnPlayerAuthorized(controller);

                    timer!.Kill();
                }, TimerFlags.REPEAT);
            });
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError(e.ToString());
        }
    }

    public void UpdatePlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            UpdatePlayer(player);
        }
    }

    public void UpdatePlayer(CCSPlayerController player)
    {
        var steamId = player.AuthorizedSteamID;
        if (!TryGetPlayer(player, out var vipPlayer) || steamId is null) return;
        
        Task.Run(() => OnClientAuthorizedAsync(vipPlayer, steamId));
    }

    public void SetClientFeature(VipPlayer? player)
    {
        if (player == null)
        {
            _plugin.Logger.LogError($"Player is null");
            return;
        }

        if (player.Group is null)
            return;

        foreach (var feature in _api.Value.FeatureManager.GetFeatures())
        {
            if (!player.Group.TryGetValue(feature.Name, out _))
            {
                player.FeatureStates[feature] = FeatureState.NoAccess;
                continue;
            }

            var cookie = _api.Value.GetPlayerCookie<int>(player.SteamId.SteamId64, feature.Name);

            var cookieValue = cookie == 2 ? 0 : cookie;
            player.FeatureStates[feature] = (FeatureState)cookieValue;
        }
    }

    private void OnDisconnect(VipPlayer vipPlayer)
    {
        if (vipPlayer.IsVip)
        {
            foreach (var (feature, state) in vipPlayer.FeatureStates.Where(f => f.Key.Type is FeatureType.Toggle))
            {
                _api.Value.SetPlayerCookie(vipPlayer.SteamId.SteamId64, feature.Name, (int)state);
            }
        }

        var player = vipPlayer.Controller;
        if (player is null || player.IsBot) return;
        
        _api.Value.InvokeOnPlayerDisconnect(player, vipPlayer.IsVip);

        var playerName = player.PlayerName;
        if (vipPlayer.Data != null)
        {
            vipPlayer.Data.LastVisit = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            UpdateUser(vipPlayer.SteamId.AccountId, name: playerName);
        }
    }

    public void PrintToChat(CCSPlayerController? player, string msg)
    {
        if (player is null || !player.IsValid) return;

        player.PrintToChat($"{_plugin.Localizer.ForPlayer(player, "vip.Prefix")} {msg}");
    }

    public void PrintToChat(VipPlayer player, string msg)
    {
        PrintToChat(player.Controller, msg);
    }

    public bool TryGetPlayer(int slot, [NotNullWhen(true)] out VipPlayer? vipPlayer)
    {
        vipPlayer = Players[slot];

        return vipPlayer != null;
    }

    public bool TryGetPlayer(CCSPlayerController player, [NotNullWhen(true)] out VipPlayer? vipPlayer)
    {
        return TryGetPlayer(player.Slot, out vipPlayer);
    }

    public void AddPlayerVip(CCSPlayerController? player, VipData data)
    {
        Task.Run(() => _databaseService.AddUserAsync(data));
        AddTemporaryUser(player, data);
    }

    public void AddTemporaryUser(CCSPlayerController? player, VipData data)
    {
        if (player != null && TryGetPlayer(player, out var vipPlayer))
        {
            vipPlayer.Data = data;
            vipPlayer.Group = _groupsConfig.Value[data.Group];

            SetClientFeature(vipPlayer);

            PrintToChat(player, _plugin.Localizer.ForPlayer(player, "vip.WelcomeToTheServer", data.Name) +
                                (data.Expires == 0
                                    ? string.Empty
                                    : _plugin.Localizer.ForPlayer(player, "vip.Expires",
                                        data.Group,
                                        DateTimeOffset.FromUnixTimeSeconds(data.Expires).ToString("G"))));

            return;
        }
    }
    

    public void RemovePlayerVip(CCSPlayerController? player, int accountId)
    {
        Task.Run(() => _databaseService.RemoveUserAsync(accountId));
        if (player != null && TryGetPlayer(player, out var vipPlayer))
        {
            vipPlayer.Data = null;
            vipPlayer.Group = null;

            PrintToChat(player, _plugin.Localizer.ForPlayer(player, "vip.NoLongerVIPPlayer"));
        }
    }

    public void UpdateUser(int accountId, string name = "", string group = "", int time = -1)
    {
        Task.Run(() => _databaseService.UpdateVipAsync(accountId, name, group, time));
    }
}