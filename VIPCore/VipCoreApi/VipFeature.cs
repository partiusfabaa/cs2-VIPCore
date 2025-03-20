using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Interface;
using VipCoreApi.Enums;

namespace VipCoreApi;

/// <summary>
/// A generic VIP feature class that allows working with typed values.
/// </summary>
/// <typeparam name="T">The type of the feature value.</typeparam>
public class VipFeature<T> : VipFeature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VipFeature{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the feature.</param>
    /// <param name="api">The VIP Core API instance.</param>
    /// <param name="type">The type of the feature (default is <see cref="FeatureType.Toggle"/>).</param>
    /// <param name="state">The initial state of the feature (default is <see cref="FeatureState.Enabled"/>).</param>
    /// <param name="onFeatureDisplay">An optional delegate for handling feature display events.</param>
    public VipFeature(string name,
        IVipCoreApi api,
        FeatureType type = FeatureType.Toggle,
        FeatureState state = FeatureState.Enabled,
        OnFeatureDisplayDelegate? onFeatureDisplay = null) : base(name, api, type, state, onFeatureDisplay)
    {
        Api = api;
    }

    /// <summary>
    /// Gets the value of the feature for the specified player.
    /// </summary>
    /// <param name="player">The player whose feature value is requested.</param>
    /// <returns>The feature value of type <typeparamref name="T"/> if it exists; otherwise, <c>null</c>.</returns>
    public T? GetValue(CCSPlayerController player)
    {
        return Api.GetFeatureValue<T?>(player, Name);
    }
}

/// <summary>
/// The base abstract class for VIP features, providing event handling and basic operations.
/// </summary>
public abstract class VipFeature : IDisposable
{
    /// <summary>
    /// Gets the name of the feature.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the feature.
    /// </summary>
    public FeatureType Type { get; }

    /// <summary>
    /// Gets the default state of the feature.
    /// </summary>
    public FeatureState State { get; set; }

    /// <summary>
    /// Delegate for handling the feature display event.
    /// </summary>
    public readonly OnFeatureDisplayDelegate? OnFeatureDisplayEvent;

    /// <summary>
    /// Gets or sets the instance of the VIP Core API.
    /// </summary>
    public IVipCoreApi Api { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VipFeature"/> class.
    /// </summary>
    /// <param name="name">The name of the feature.</param>
    /// <param name="api">The VIP Core API instance.</param>
    /// <param name="type">The type of the feature (default is <see cref="FeatureType.Toggle"/>).</param>
    /// <param name="state">The initial state of the feature (default is <see cref="FeatureState.Enabled"/>).</param>
    /// <param name="onFeatureDisplay">An optional delegate for handling feature display events.</param>
    public VipFeature(string name,
        IVipCoreApi api,
        FeatureType type = FeatureType.Toggle,
        FeatureState state = FeatureState.Enabled,
        OnFeatureDisplayDelegate? onFeatureDisplay = null)
    {
        Name = name;
        Type = type;
        State = state;
        Api = api;

        OnFeatureDisplayEvent = onFeatureDisplay;

        // Register the feature in the manager.
        api.FeatureManager.Register(this);

        // Subscribe to events.
        api.OnPlayerAuthorized += OnPlayerAuthorized;
        api.OnPlayerDisconnect += OnPlayerDisconnect;
        api.OnPlayerSpawn += OnPlayerSpawn;
        api.OnPlayerUseFeature += OnPlayerUseFeature;
    }

    /// <summary>
    /// Handles the event when a player is authorized.
    /// </summary>
    /// <param name="player">The authorized player.</param>
    /// <param name="group">Vip group of the player.</param>
    public virtual void OnPlayerAuthorized(CCSPlayerController player, string group)
    {
    }

    /// <summary>
    /// Handles the event when a player disconnects.
    /// </summary>
    /// <param name="player">The player who disconnected.</param>
    /// <param name="vip">vip</param>
    public virtual void OnPlayerDisconnect(CCSPlayerController player, bool vip)
    {
    }

    /// <summary>
    /// Handles the event when a player spawns.
    /// </summary>
    /// <param name="player">The player who spawned.</param>
    /// <param name="vip"><c>true</c> if the player is VIP; otherwise, <c>false</c>.</param>
    public virtual void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
    }

    /// <summary>
    /// Handles the event when a player selects an item related to the feature.
    /// </summary>
    /// <param name="player">The player who selected the item.</param>
    /// <param name="feature">The feature associated with the selected item.</param>
    public virtual void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
    }

    /// <summary>
    /// Handles the event when a player uses a feature.
    /// </summary>
    /// <param name="args">The event arguments of type <see cref="PlayerUseFeatureEventArgs"/>.</param>
    public virtual void OnPlayerUseFeature(PlayerUseFeatureEventArgs args)
    {
    }

    /// <summary>
    /// Handles the event when the feature is displayed.
    /// </summary>
    /// <param name="args">The event arguments of type <see cref="FeatureDisplayArgs"/>.</param>
    public virtual void OnFeatureDisplay(FeatureDisplayArgs args)
    {
    }

    /// <summary>
    /// Retrieves the translated text for the specified key with optional formatting arguments for the given player.
    /// </summary>
    /// <param name="player">The player for whom the translation is requested.</param>
    /// <param name="key">The translation key.</param>
    /// <param name="args">Optional formatting arguments.</param>
    /// <returns>A localized string.</returns>
    public string GetTranslatedText(CCSPlayerController player, string key, params object[] args)
        => Api.GetTranslatedText(player, key, args);

    /// <summary>
    /// Retrieves the translated text for the specified key with optional formatting arguments.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <param name="args">Optional formatting arguments.</param>
    /// <returns>A localized string.</returns>
    public string GetTranslatedText(string key, params object[] args)
        => Api.GetTranslatedText(key, args);

    /// <summary>
    /// Creates a menu with the specified title.
    /// </summary>
    /// <param name="title">The title of the menu.</param>
    /// <returns>An instance of <see cref="IMenu"/> representing the created menu.</returns>
    public IMenu CreateMenu(string title) => Api.CreateMenu(title);

    /// <summary>
    /// Checks if the player is valid for using this feature, i.e. is VIP, has access to the feature, and the feature is enabled.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns><c>true</c> if the player meets all criteria; otherwise, <c>false</c>.</returns>
    public bool IsPlayerValid(CCSPlayerController player)
        => IsPlayerVip(player) && PlayerHasFeature(player) && GetPlayerFeatureState(player) is FeatureState.Enabled;

    /// <summary>
    /// Checks if the specified player is VIP.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns><c>true</c> if the player is VIP; otherwise, <c>false</c>.</returns>
    public bool IsPlayerVip(CCSPlayerController player) => Api.IsPlayerVip(player);

    /// <summary>
    /// Checks if the specified player has access to this feature.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns><c>true</c> if the player has access; otherwise, <c>false</c>.</returns>
    public bool PlayerHasFeature(CCSPlayerController player) => Api.PlayerHasFeature(player, Name);

    /// <summary>
    /// Gets the value of this feature for the specified player.
    /// </summary>
    /// <typeparam name="T">The expected type of the feature value.</typeparam>
    /// <param name="player">The player whose feature value is requested.</param>
    /// <returns>The feature value of type <typeparamref name="T"/> if it exists; otherwise, <c>null</c>.</returns>
    public T? GetFeatureValue<T>(CCSPlayerController player) => Api.GetFeatureValue<T>(player, Name);

    /// <summary>
    /// Gets the current state of this feature for the specified player.
    /// </summary>
    /// <param name="player">The player whose feature state is requested.</param>
    /// <returns>The feature state (<see cref="FeatureState"/>).</returns>
    public FeatureState GetPlayerFeatureState(CCSPlayerController player) => Api.GetPlayerFeatureState(player, Name);

    /// <summary>
    /// Sets the state of this feature for the specified player.
    /// </summary>
    /// <param name="player">The player for whom the feature state is being set.</param>
    /// <param name="newState">The new feature state.</param>
    public void SetPlayerFeatureState(CCSPlayerController player, FeatureState newState) =>
        Api.SetPlayerFeatureState(player, Name, newState);

    /// <summary>
    /// Gets the VIP group of the specified player.
    /// </summary>
    /// <param name="player">The player whose VIP group is requested.</param>
    /// <returns>The VIP group as a string.</returns>
    public string GetPlayerVipGroup(CCSPlayerController player) => Api.GetPlayerVipGroup(player);

    /// <summary>
    /// Gets the VIP groups defined in the vip.json file.
    /// </summary>
    /// <returns>An array of strings representing the VIP groups.</returns>
    public string[] GetVipGroups() => Api.GetVipGroups();

    /// <summary>
    /// Updates the VIP information for the specified player.
    /// </summary>
    /// <param name="player">The player for whom the VIP information is updated.</param>
    /// <param name="name">The name of the player (if an update is needed).</param>
    /// <param name="group">The VIP group to set (if an update is needed).</param>
    /// <param name="time">The duration for which VIP status is valid (for example, in minutes). A value of -1 indicates no change.</param>
    public void UpdatePlayerVip(CCSPlayerController player, string name = "", string group = "", int time = -1)
        => Api.UpdatePlayerVip(player, name, group, time);

    /// <summary>
    /// Sets the VIP status for the specified player.
    /// </summary>
    /// <param name="player">The player for whom VIP status is set.</param>
    /// <param name="group">The VIP group to assign.</param>
    /// <param name="time">The duration for the VIP status.</param>
    public void SetPlayerVip(CCSPlayerController player, string group, int time)
        => Api.SetPlayerVip(player, group, time);

    /// <summary>
    /// Grants VIP status to the specified player.
    /// </summary>
    /// <param name="player">The player to whom VIP status is granted.</param>
    /// <param name="group">The VIP group to assign.</param>
    /// <param name="time">The duration for the VIP status.</param>
    public void GivePlayerVip(CCSPlayerController player, string group, int time)
        => Api.GivePlayerVip(player, group, time);

    /// <summary>
    /// Grants temporary VIP status (for one game session) to the specified player.
    /// </summary>
    /// <param name="player">The player to whom temporary VIP status is granted.</param>
    /// <param name="group">The VIP group to assign.</param>
    /// <param name="time">The duration for the temporary VIP status.</param>
    public void GivePlayerTemporaryVip(CCSPlayerController player, string group, int time)
        => Api.GivePlayerTemporaryVip(player, group, time);

    /// <summary>
    /// Removes the VIP status from the specified player.
    /// </summary>
    /// <param name="player">The player whose VIP status is removed.</param>
    public void RemovePlayerVip(CCSPlayerController player)
        => Api.RemovePlayerVip(player);

    /// <summary>
    /// Saves a cookie value for the specified player.
    /// </summary>
    /// <typeparam name="T">The type of the cookie value.</typeparam>
    /// <param name="player">The player for whom the cookie is set.</param>
    /// <param name="key">The key of the cookie.</param>
    /// <param name="value">The value to be saved.</param>
    public void SetPlayerCookie<T>(CCSPlayerController player, string key, T value)
        => Api.SetPlayerCookie(player, key, value);

    /// <summary>
    /// Retrieves a cookie value for the specified player.
    /// </summary>
    /// <typeparam name="T">The expected type of the cookie value.</typeparam>
    /// <param name="player">The player whose cookie is requested.</param>
    /// <param name="key">The key of the cookie.</param>
    /// <returns>The cookie value of type <typeparamref name="T"/> if it exists; otherwise, <c>null</c>.</returns>
    public T? GetPlayerCookie<T>(CCSPlayerController player, string key)
        => Api.GetPlayerCookie<T>(player, key);

    /// <summary>
    /// Sends a message to the in-game chat for the specified player.
    /// </summary>
    /// <param name="controller">The player who will receive the message.</param>
    /// <param name="msg">The message text.</param>
    public void PrintToChat(CCSPlayerController controller, string msg)
        => Api.PrintToChat(controller, msg);

    /// <summary>
    /// Sends a message to the in-game chat for all players.
    /// </summary>
    /// <param name="message">The message text.</param>
    public void PrintToChatAll(string message)
        => Api.PrintToChatAll(message);

    /// <summary>
    /// Checks if the current game round is a pistol round.
    /// </summary>
    /// <returns><c>true</c> if it is a pistol round; otherwise, <c>false</c>.</returns>
    public bool IsPistolRound()
        => Api.IsPistolRound();

    public T LoadConfig<T>(string name)
        => Api.LoadConfig<T>(name);

    public T LoadConfig<T>(string name, string path)
        => Api.LoadConfig<T>(name, path);

    /// <summary>
    /// Releases resources and unregisters the feature from the manager.
    /// </summary>
    public virtual void Dispose()
    {
        Api.FeatureManager.Unregister(this);
    }
}