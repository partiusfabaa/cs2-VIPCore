using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_SimpleAdminApi;
using Microsoft.Extensions.Logging;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Tag;

public class VIPTag : BasePlugin
{
    public override string ModuleAuthor => "Toil";
    public override string ModuleName => "[VIP] Tag";
    public override string ModuleVersion => "v1.1.0";

    private IVipCoreApi? _api;
    private Tag? _tag;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _tag = new Tag(this, _api);
        // Toggle: the player can Enable/Disable the tag from the !vip menu.
        _api.RegisterFeature(_tag, FeatureType.Toggle);
    }

    public override void Unload(bool hotReload)
    {
        if (_tag != null) _api?.UnRegisterFeature(_tag);
    }
}

/// <summary>
/// The "Tag" feature value from vip.json, per VIP group. Everything (scoreboard text, chat tag,
/// and all colours) lives here. The feature is a toggle in the !vip menu.
///
/// vip.json:
///   "Tag": {
///     "ScoreboardTag": "ADMIN",     // plain text shown on the scoreboard (clan tag)
///     "ChatTag":       "[ADMIN]",   // prefix shown before the name in chat
///     "ColorTag":      "red",       // colour of the chat tag prefix
///     "NameColor":     "red",       // colour of the player name in chat
///     "MessageColor":  "grey"       // colour of the message body in chat
///   }
/// </summary>
public class TagValue
{
    [JsonPropertyName("ScoreboardTag")] public string ScoreboardTag { get; set; } = "";
    [JsonPropertyName("ChatTag")] public string ChatTag { get; set; } = "";
    [JsonPropertyName("ColorTag")] public string ColorTag { get; set; } = "";
    [JsonPropertyName("NameColor")] public string NameColor { get; set; } = "";
    [JsonPropertyName("MessageColor")] public string MessageColor { get; set; } = "";
}

public class Tag : VipFeatureBase
{
    public override string Feature => "Tag";

    private readonly VIPTag _plugin;

    // Gag/silence sources, auto-detected at load. Any, all, or none may be present.
    private ICS2_SimpleAdminApi? _simpleAdmin;
    private AdminPlusGagReader? _adminPlus;
    private IksAdminGagReader? _iksAdmin;

    public Tag(VIPTag vipTag, IVipCoreApi api) : base(api)
    {
        _plugin = vipTag;

        // Standalone chat tag (no ChatManager): own the chat hook and render the coloured line.
        vipTag.AddCommandListener("say", OnSay, HookMode.Pre);
        vipTag.AddCommandListener("say_team", OnSayTeam, HookMode.Pre);

        DetectGagBackends();
    }

    /// <summary>
    /// Auto-detects which admin plugin(s) provide gag/silence and wires to them:
    ///   - CS2-SimpleAdmin via its public API (capability "simpleadmin:api"),
    ///   - Iks_Admin via its capability "iksadmin:core" (reflection, no DLL dependency),
    ///   - CS2-AdminPlus via its communication_data.json file.
    /// If none is present, logs a warning: VIP_Tag re-renders chat itself, so without a gag source
    /// a gagged player could still talk through the tag.
    /// </summary>
    private void DetectGagBackends()
    {
        try { _simpleAdmin = ICS2_SimpleAdminApi.PluginCapability.Get(); }
        catch { _simpleAdmin = null; }

        _iksAdmin = IksAdminGagReader.TryCreate();

        // AdminPlus has no API - detect it by its data file under the game root.
        var adminPlusPath = Path.Combine(GameRoot(_plugin.ModuleDirectory),
            "addons", "counterstrikesharp", "plugins", "AdminPlus", "communication_data.json");
        var reader = new AdminPlusGagReader(adminPlusPath);
        _adminPlus = reader.FileExists ? reader : null;

        var found = new List<string>();
        if (_simpleAdmin != null) found.Add("CS2-SimpleAdmin");
        if (_iksAdmin != null) found.Add("Iks_Admin");
        if (_adminPlus != null) found.Add("CS2-AdminPlus");

        if (found.Count > 0)
            _plugin.Logger.LogInformation("[VIP_Tag] Gag/silence source(s): {Sources}.", string.Join(", ", found));
        else
            _plugin.Logger.LogWarning("[VIP_Tag] No admin plugin found (CS2-SimpleAdmin / Iks_Admin / CS2-AdminPlus). " +
                                      "Gag and silence will NOT be enforced for the chat tag - a gagged player could still talk through it.");
    }

    /// <summary>True only when the player is VIP, has the feature, and it is toggled Enabled.</summary>
    private bool IsTagEnabled(CCSPlayerController player)
        => PlayerHasFeature(player) && GetPlayerFeatureState(player) == FeatureState.Enabled;

    /// <summary>True when the player is currently gagged or silenced by the detected admin plugin.</summary>
    private bool IsChatBlocked(CCSPlayerController player)
    {
        if (_simpleAdmin != null)
        {
            try
            {
                var status = _simpleAdmin.GetPlayerMuteStatus(player);
                if (HasActivePenalty(status, PenaltyType.Gag) || HasActivePenalty(status, PenaltyType.Silence))
                    return true;
            }
            catch { /* ignore and fall through */ }
        }

        if (_iksAdmin != null && _iksAdmin.IsGaggedOrSilenced(player))
            return true;

        if (_adminPlus != null && _adminPlus.IsGagged(player.SteamID))
            return true;

        return false;
    }

    private static bool HasActivePenalty(
        Dictionary<PenaltyType, List<(DateTime EndDateTime, int Duration, bool Passed)>> status,
        PenaltyType type)
    {
        if (!status.TryGetValue(type, out var list)) return false;
        return list.Any(p => !p.Passed && (p.Duration == 0 || p.EndDateTime > DateTime.UtcNow));
    }

    // --- Toggle from the !vip menu ------------------------------------------------------------

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        // Re-apply the scoreboard tag immediately so the change shows without waiting for a respawn.
        ApplyScoreboard(player);
    }

    // --- Chat rendering (standalone, coloured) ------------------------------------------------

    private HookResult OnSay(CCSPlayerController? player, CommandInfo info) => HandleChat(player, info, false);
    private HookResult OnSayTeam(CCSPlayerController? player, CommandInfo info) => HandleChat(player, info, true);

    private HookResult HandleChat(CCSPlayerController? player, CommandInfo info, bool teamOnly)
    {
        if (player is not { IsValid: true } || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var message = info.GetArg(1);
        if (string.IsNullOrWhiteSpace(message))
            return HookResult.Continue;

        // Let chat command triggers pass through untouched. Use the triggers CounterStrikeSharp is
        // configured with (core.json: PublicChatTrigger / SilentChatTrigger) instead of hardcoding.
        if (IsChatTrigger(message))
            return HookResult.Continue;

        if (!IsTagEnabled(player))
            return HookResult.Continue;

        // A gagged / silenced player sends nothing - swallow it so the tag can't bypass the mute.
        if (IsChatBlocked(player))
            return HookResult.Handled;

        var tag = GetTag(player);
        if (tag == null || string.IsNullOrEmpty(tag.ChatTag))
            return HookResult.Continue;

        var team = player.Team;
        var tagColor = TagColors.ColorChar(tag.ColorTag, team);
        var nameColor = TagColors.ColorChar(tag.NameColor, team);
        var msgColor = TagColors.ColorChar(tag.MessageColor, team);

        var line = $" {tagColor}{tag.ChatTag} {nameColor}{player.PlayerName}{ChatColors.Default}: {msgColor}{message}";

        if (teamOnly)
        {
            foreach (var receiver in Utilities.GetPlayers())
                if (receiver is { IsValid: true, IsBot: false } && receiver.Team == player.Team)
                    receiver.PrintToChat(line);
        }
        else
        {
            Server.PrintToChatAll(line);
        }

        // We rendered the line ourselves, so swallow the default one.
        return HookResult.Handled;
    }

    // --- Scoreboard tag -----------------------------------------------------------------------

    private void ApplyScoreboard(CCSPlayerController player)
    {
        if (player is not { IsValid: true } || player.IsBot || player.IsHLTV) return;

        var tag = IsTagEnabled(player) ? GetTag(player) : null;
        var clan = tag?.ScoreboardTag ?? string.Empty;

        player.Clan = clan;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        ApplyScoreboard(player);
    }

    private TagValue? GetTag(CCSPlayerController player)
    {
        try { return GetFeatureValue<TagValue>(player); }
        catch { return null; }
    }

    /// <summary>
    /// True when the message starts with a chat command trigger, read from CounterStrikeSharp's
    /// own core.json (PublicChatTrigger / SilentChatTrigger).
    /// </summary>
    private static bool IsChatTrigger(string message)
    {
        var trimmed = message.TrimStart();
        if (trimmed.Length == 0) return false;

        foreach (var trigger in CoreConfig.PublicChatTrigger)
            if (!string.IsNullOrEmpty(trigger) && trimmed.StartsWith(trigger, StringComparison.Ordinal))
                return true;

        foreach (var trigger in CoreConfig.SilentChatTrigger)
            if (!string.IsNullOrEmpty(trigger) && trimmed.StartsWith(trigger, StringComparison.Ordinal))
                return true;

        return false;
    }

    /// <summary>The CS2 game root (the folder that contains "addons"), derived from a module dir.</summary>
    private static string GameRoot(string moduleDirectory)
    {
        var dir = new DirectoryInfo(moduleDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "addons")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return moduleDirectory;
    }
}
