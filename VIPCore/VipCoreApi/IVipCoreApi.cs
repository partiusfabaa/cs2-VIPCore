using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CS2ScreenMenuAPI;
using VipCoreApi.Enums;

namespace VipCoreApi;

/// <summary>
/// API interface for the VIP Core plugin. Provides access to configuration directories, VIP functions, and event handlers.
/// </summary>
public interface IVipCoreApi
{
    /// <summary>
    /// Gets the plugin capability for VIP Core API.
    /// </summary>
    public static PluginCapability<IVipCoreApi> Capability => new("vipcore:core");

    /// <summary>
    /// Gets the directory for core configuration files.
    /// </summary>
    string CoreConfigDirectory { get; }

    /// <summary>
    /// Gets the directory for module configuration files.
    /// </summary>
    string ModulesConfigDirectory { get; }

    /// <summary>
    /// Gets the database connection string.
    /// </summary>
    string DatabaseConnectionString { get; }

    /// <summary>
    /// Gets the server identifier.
    /// </summary>
    int ServerId { get; }

    /// <summary>
    /// Gets the feature manager instance.
    /// </summary>
    IFeatureManager FeatureManager { get; }

    /// <summary>
    /// Gets the current state of a specified feature for the given player.
    /// </summary>
    /// <param name="player">The player whose feature state is requested.</param>
    /// <param name="feature">The name of the feature.</param>
    /// <returns>The current feature state (<see cref="FeatureState"/>).</returns>
    FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature);

    /// <summary>
    /// Sets the state of a specified feature for the given player.
    /// </summary>
    /// <param name="player">The player for whom the feature state is being set.</param>
    /// <param name="feature">The name of the feature.</param>
    /// <param name="newState">The new feature state.</param>
    void SetPlayerFeatureState(CCSPlayerController player, string feature, FeatureState newState);

    /// <summary>
    /// Checks whether the specified player is a VIP.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns><c>true</c> if the player is VIP; otherwise, <c>false</c>.</returns>
    bool IsPlayerVip(CCSPlayerController player);

    /// <summary>
    /// Checks if the player has access to the specified feature.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <param name="feature">The name of the feature.</param>
    /// <returns><c>true</c> if the player has access; otherwise, <c>false</c>.</returns>
    bool PlayerHasFeature(CCSPlayerController player, string feature);

    /// <summary>
    /// Gets the value associated with a feature for the specified player.
    /// </summary>
    /// <typeparam name="T">The expected type of the feature value.</typeparam>
    /// <param name="player">The player whose feature value is requested.</param>
    /// <param name="feature">The name of the feature.</param>
    /// <returns>The feature value of type <typeparamref name="T"/> if it exists; otherwise, <c>null</c>.</returns>
    T? GetFeatureValue<T>(CCSPlayerController player, string feature);

    /// <summary>
    /// Gets the VIP group of the specified player.
    /// </summary>
    /// <param name="player">The player whose VIP group is requested.</param>
    /// <returns>The VIP group as a string.</returns>
    string GetPlayerVipGroup(CCSPlayerController player);

    /// <summary>
    /// Gets the VIP groups defined in the vip.json file.
    /// </summary>
    /// <returns>An array of strings representing the VIP groups.</returns>
    string[] GetVipGroups();

    /// <summary>
    /// Updates the VIP information for the specified player.
    /// </summary>
    /// <param name="player">The player for whom the VIP information is updated.</param>
    /// <param name="name">The name of the player (if an update is needed).</param>
    /// <param name="group">The VIP group to set (if an update is needed).</param>
    /// <param name="time">
    /// The duration for which VIP status is valid (for example, in minutes). 
    /// A value of -1 indicates no change.
    /// </param>
    void UpdatePlayerVip(CCSPlayerController player, string name = "", string group = "", int time = -1);

    /// <summary>
    /// Sets the VIP status for the specified player.
    /// </summary>
    /// <param name="player">The player for whom VIP status is set.</param>
    /// <param name="group">The VIP group to assign.</param>
    /// <param name="time">The duration for the VIP status.</param>
    void SetPlayerVip(CCSPlayerController player, string group, int time);

    /// <summary>
    /// Grants VIP status to the specified player.
    /// </summary>
    /// <param name="player">The player to whom VIP status is granted.</param>
    /// <param name="group">The VIP group to assign.</param>
    /// <param name="time">The duration for the VIP status.</param>
    void GivePlayerVip(CCSPlayerController player, string group, int time);

    /// <summary>
    /// Grants temporary VIP status (for one game session) to the specified player.
    /// </summary>
    /// <param name="player">The player to whom temporary VIP status is granted.</param>
    /// <param name="group">The VIP group to assign.</param>
    /// <param name="time">The duration for the temporary VIP status.</param>
    void GivePlayerTemporaryVip(CCSPlayerController player, string group, int time);

    /// <summary>
    /// Removes the VIP status from the specified player.
    /// </summary>
    /// <param name="player">The player whose VIP status is removed.</param>
    void RemovePlayerVip(CCSPlayerController player);

    /// <summary>
    /// Saves a cookie value for the player based on their SteamID.
    /// </summary>
    /// <typeparam name="T">The type of the cookie value.</typeparam>
    /// <param name="player">The player for whom the cookie is set.</param>
    /// <param name="key">The key of the cookie.</param>
    /// <param name="value">The value to be saved.</param>
    void SetPlayerCookie<T>(CCSPlayerController player, string key, T value) => SetPlayerCookie(player.SteamID, key, value);

    /// <summary>
    /// Saves a cookie value for the player using their SteamID.
    /// </summary>
    /// <typeparam name="T">The type of the cookie value.</typeparam>
    /// <param name="steamId64">The SteamID of the player.</param>
    /// <param name="key">The key of the cookie.</param>
    /// <param name="value">The value to be saved.</param>
    void SetPlayerCookie<T>(ulong steamId64, string key, T value);

    /// <summary>
    /// Retrieves a cookie value for the player based on their SteamID.
    /// </summary>
    /// <typeparam name="T">The expected type of the cookie value.</typeparam>
    /// <param name="player">The player whose cookie is requested.</param>
    /// <param name="key">The key of the cookie.</param>
    /// <returns>The cookie value of type <typeparamref name="T"/> if it exists; otherwise, <c>null</c>.</returns>
    T GetPlayerCookie<T>(CCSPlayerController player, string key) => GetPlayerCookie<T>(player.SteamID, key);

    /// <summary>
    /// Retrieves a cookie value for the player using their SteamID.
    /// </summary>
    /// <typeparam name="T">The expected type of the cookie value.</typeparam>
    /// <param name="steamId64">The SteamID of the player.</param>
    /// <param name="key">The key of the cookie.</param>
    /// <returns>The cookie value of type <typeparamref name="T"/> if it exists; otherwise, <c>null</c>.</returns>
    T GetPlayerCookie<T>(ulong steamId64, string key);

    /// <summary>
    /// Sends a message to the in-game chat for the specified player.
    /// </summary>
    /// <param name="player">The player to whom the message is sent.</param>
    /// <param name="message">The message text.</param>
    void PrintToChat(CCSPlayerController player, string message);

    /// <summary>
    /// Sends a message to the in-game chat for all players.
    /// </summary>
    /// <param name="message">The message text.</param>
    void PrintToChatAll(string message);

    /// <summary>
    /// Retrieves the translated text for the specified key and optional format arguments for a given player.
    /// </summary>
    /// <param name="player">The player for whom the translation is requested.</param>
    /// <param name="name">The key or name for the translation.</param>
    /// <param name="args">Optional arguments for formatting the text.</param>
    /// <returns>A localized string based on the provided key and arguments.</returns>
    string GetTranslatedText(CCSPlayerController player, string name, params object[] args);

    /// <summary>
    /// Retrieves the translated text for the specified key and optional format arguments.
    /// </summary>
    /// <param name="name">The key or name for the translation.</param>
    /// <param name="args">Optional arguments for formatting the text.</param>
    /// <returns>A localized string based on the provided key and arguments.</returns>
    string GetTranslatedText(string name, params object[] args);

    /// <summary>
    /// Checks if the current game round is a pistol round.
    /// </summary>
    /// <returns><c>true</c> if it is a pistol round; otherwise, <c>false</c>.</returns>
    bool IsPistolRound();

    /// <summary>
    /// Loads a configuration file from the specified path.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    /// <param name="name">The name of the configuration file.</param>
    /// <param name="path">The path to the configuration file directory.</param>
    /// <returns>An object of type <typeparamref name="T"/> representing the configuration.</returns>
    T LoadConfig<T>(string name, string path);

    /// <summary>
    /// Loads a configuration file from the standard directory.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    /// <param name="name">The name of the configuration file.</param>
    /// <returns>An object of type <typeparamref name="T"/> representing the configuration.</returns>
    T LoadConfig<T>(string name);

    /// <summary>
    /// Creates a menu based on the configuration settings.
    /// </summary>
    /// <param name="player">player</param>
    /// <param name="title">The title of the menu.</param>
    /// <returns>An instance of <see cref="IMenu"/> representing the created menu.</returns>
    Menu CreateMenu(CCSPlayerController player, string title);
        

    /// <summary>
    /// Event triggered when a player is authorized.
    /// </summary>
    event OnPlayerAuthorizedDelegate? OnPlayerAuthorized;

    /// <summary>
    /// Event triggered when a player disconnects.
    /// </summary>
    event OnPlayerDisconnectDelegate? OnPlayerDisconnect;

    /// <summary>
    /// Event triggered when a player spawns.
    /// </summary>
    event OnPlayerSpawnDelegate? OnPlayerSpawn;

    /// <summary>
    /// Event triggered when a player uses a feature.
    /// </summary>
    event OnPlayerUseFeatureDelegate? OnPlayerUseFeature;

    /// <summary>
    /// Event triggered when the core module is ready.
    /// </summary>
    event Action? OnCoreReady;
}

/// <summary>
/// Delegate for handling the player authorization event.
/// </summary>
/// <param name="player">The player who has been authorized.</param>
public delegate void OnPlayerAuthorizedDelegate(CCSPlayerController player, string group);

/// <summary>
/// Delegate for handling the player disconnect event.
/// </summary>
/// <param name="player">The player who disconnected.</param>
public delegate void OnPlayerDisconnectDelegate(CCSPlayerController player, bool vip);

/// <summary>
/// Delegate for handling the player spawn event.
/// </summary>
/// <param name="player">The player who spawned.</param>
/// <param name="vip"><c>true</c> if the player is VIP; otherwise, <c>false</c>.</param>
public delegate void OnPlayerSpawnDelegate(CCSPlayerController player, bool vip);

/// <summary>
/// Delegate for handling the event when a player uses a feature.
/// </summary>
/// <param name="args">The event arguments of type <see cref="PlayerUseFeatureEventArgs"/>.</param>
public delegate void OnPlayerUseFeatureDelegate(PlayerUseFeatureEventArgs args);

/// <summary>
/// Delegate for handling the event when a feature is displayed.
/// </summary>
/// <param name="args">The event arguments of type <see cref="FeatureDisplayArgs"/>.</param>
public delegate void OnFeatureDisplayDelegate(FeatureDisplayArgs args);