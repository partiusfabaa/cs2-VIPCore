using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace VipCoreApi;

/// <summary>
/// Api for VIP Core plugin
/// </summary>
public interface IVipCoreApi
{
    /// <summary>
    /// Represents the state of a feature (Enabled, Disabled, NoAccess).
    /// </summary>
    enum FeatureState
    {
        Enabled,
        Disabled,
        NoAccess
    }

    /// <summary>
    /// Represents the type of a feature (Toggle, Selectable, Hide).
    /// </summary>
    enum FeatureType
    {
        Toggle,
        Selectable,
        Hide
    }

    /// <summary>
    /// Returns the directory for core configuration.
    /// </summary>
    string CoreConfigDirectory { get; }

    /// <summary>
    /// Returns the directory for modules configuration.
    /// </summary>
    string ModulesConfigDirectory { get; }

    /// <summary>
    /// Returns the database connection string
    /// </summary>
    string GetDatabaseConnectionString { get; }

    /// <summary>
    /// Registers a VIP feature with specified parameters.
    /// </summary>
    /// <param name="vipFeatureBase"></param>
    /// <param name="featureType"></param>
    void RegisterFeature(VipFeatureBase vipFeatureBase, FeatureType featureType = FeatureType.Toggle);

    ///// <param name="selectItem"></param>
    // void RegisterFeature(VipFeatureBase vipFeatureBase, FeatureType featureType = FeatureType.Toggle,
    //     Action<CCSPlayerController, FeatureState>? selectItem = null); 

    /// <summary>
    ///  Unregisters a VIP feature.
    /// </summary>
    /// <param name="vipFeatureBase"></param>
    void UnRegisterFeature(VipFeatureBase vipFeatureBase);

    /// <summary>
    ///  Gets all registered functions
    /// </summary>
    IEnumerable<(string feature, object value)> GetAllRegisteredFeatures();

    /// <summary>
    /// Gets the state of a feature for a specific player.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="feature"></param>
    /// <returns></returns>
    FeatureState GetPlayerFeatureState(CCSPlayerController player, string feature);

    /// <summary>
    /// Sets the state of the function for a specific player.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="feature"></param>
    /// <param name="newState"></param>
    void SetPlayerFeatureState(CCSPlayerController player, string feature, FeatureState newState);
    
    /// <summary>
    /// Turns off all functions
    /// </summary>
    void DisableAllFeatures();
    
    /// /// <summary>
    /// Turns on all the functions
    /// </summary>
    void EnableAllFeatures();

    /// <summary>
    /// Checks if a player is a VIP client.
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    bool IsClientVip(CCSPlayerController player);

    /// <summary>
    /// Checks if a player has a specific feature.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="feature"></param>
    /// <returns></returns>
    bool PlayerHasFeature(CCSPlayerController player, string feature);

    /// <summary>
    /// Gets the value of a feature for a specific player.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="feature"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T GetFeatureValue<T>(CCSPlayerController player, string feature);

    /// <summary>
    /// Gets the VIP group of a player
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    string GetClientVipGroup(CCSPlayerController player);

    /// <summary>
    /// Gets the VIP groups from the vip.json file
    /// </summary>
    /// <returns></returns>
    string[] GetVipGroups();

    /// <summary>
    /// Updates VIP information for a player.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="name"></param>
    /// <param name="group"></param>
    /// <param name="time"></param>
    void UpdateClientVip(CCSPlayerController player, string name = "", string group = "", int time = -1);

    /// <summary>
    /// Manage VIP status for a player.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="group"></param>
    /// <param name="time"></param>
    void SetClientVip(CCSPlayerController player, string group, int time);

    /// <summary>
    /// Manage VIP status for a player.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="group"></param>
    /// <param name="time"></param>
    void GiveClientVip(CCSPlayerController player, string group, int time);

    /// <summary>
    /// Gives VIP player for 1 game session
    /// </summary>
    /// <param name="player"></param>
    /// <param name="group"></param>
    /// <param name="time"></param>
    void GiveClientTemporaryVip(CCSPlayerController player, string group, int time);

    /// <summary>
    /// Remove VIP status for a player.
    /// </summary>
    /// <param name="player"></param>
    void RemoveClientVip(CCSPlayerController player);

    /// <summary>
    /// Saves the player's cookie
    /// </summary>
    /// <param name="steamId64"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    void SetPlayerCookie<T>(ulong steamId64, string key, T value);

    /// <summary>
    /// Returns the player's cookie
    /// </summary>
    /// <param name="steamId64"></param>
    /// <param name="key"></param>
    /// <typeparam name="T"></typeparam>
    T GetPlayerCookie<T>(ulong steamId64, string key);

    /// <summary>
    /// Prints messages to the in-game chat.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="message"></param>
    void PrintToChat(CCSPlayerController player, string message);

    /// <summary>
    /// Prints messages to the in-game chat.
    /// </summary>
    /// <param name="message"></param>
    void PrintToChatAll(string message);

    /// <summary>
    /// Retrieves translated text based on a name and optional arguments.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    string GetTranslatedText(string name, params object[] args);

    /// <summary>
    /// Checks if it's a pistol round.
    /// </summary>
    /// <returns></returns>
    bool IsPistolRound();

    /// <summary>
    /// Loads a configuration file.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T LoadConfig<T>(string name, string path);

    /// <summary>
    /// Loads a configuration file.
    /// </summary>
    /// <param name="name"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T LoadConfig<T>(string name);

    /// <summary>
    /// Returns a menu depending on the UseCenterHtmlMenu parameter from the config.
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    IMenu CreateMenu(string title);

    
    /// <summary>
    /// Returns server id
    /// </summary>
    /// <returns>int</returns>
    int GetServerId();

    /// <summary>
    /// Event triggered when a player is spawned.
    /// </summary>
    event Action<CCSPlayerController>? OnPlayerSpawn;

    /// <summary>
    /// Event triggered when a player is loaded.
    /// </summary>
    event Action<CCSPlayerController, string>? PlayerLoaded;

    /// <summary>
    /// Event triggered when a player is removed.
    /// </summary>
    event Action<CCSPlayerController, string>? PlayerRemoved;

    /// <summary>
    /// Event checks if the core is loaded
    /// </summary>
    event Action? OnCoreReady;

    /// <summary>
    /// Event for prevent toggle use of VIP menu.
    /// </summary>
    event Func<CCSPlayerController, string, FeatureState, FeatureType, HookResult?> OnPlayerUseFeature;
}