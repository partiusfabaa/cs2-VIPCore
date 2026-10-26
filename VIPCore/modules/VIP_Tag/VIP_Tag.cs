using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
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
    public override string ModuleVersion => "v1.2.0";

    private IVipCoreApi? _api;
    private Tag? _tag;

    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _tag = new Tag(this, _api);
        _api.RegisterFeature(_tag, FeatureType.Selectable);
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

public class TagsConfig
{
    public Dictionary<string, List<TagValue>> TagSets { get; set; } = new();
    public Dictionary<string, JsonElement> Players { get; set; } = new();
}

public class Tag : VipFeatureBase
{
    public override string Feature => "Tag";

    private const string ChoiceCookie = "player_tag";
    private const string ChoiceOff = "off";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly VIPTag _plugin;
    private Dictionary<string, List<TagValue>> _tagSets = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<ulong, List<TagValue>> _personalTags = new();
    private readonly HashSet<string> _missingSets = new(StringComparer.OrdinalIgnoreCase);

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

        vipTag.AddCommand("css_tag", "Choose your tag", (player, _) =>
        {
            if (player is { IsValid: true }) OpenTagMenu(player);
        });
        vipTag.AddCommand("css_tag_reload", "Reload vip_tag.json", (player, info) =>
        {
            if (player != null && !AdminManager.PlayerHasPermissions(player, "@css/root")) return;
            LoadTagsConfig();
            _missingSets.Clear();
            foreach (var p in Utilities.GetPlayers()) ApplyScoreboard(p);
            info.ReplyToCommand($"[VIP_Tag] Loaded {_tagSets.Count} tag set(s), personal tags for {_personalTags.Count} player(s).");
        });

        vipTag.RegisterEventHandler<EventPlayerSpawn>((@event, _) =>
        {
            if (@event.Userid is { } player) ApplyScoreboard(player);
            return HookResult.Continue;
        });

        LoadTagsConfig();
        DetectGagBackends();
    }

    private void LoadTagsConfig()
    {
        var sets = new Dictionary<string, List<TagValue>>(StringComparer.OrdinalIgnoreCase);
        var tags = new Dictionary<ulong, List<TagValue>>();
        try
        {
            var config = LoadConfig<TagsConfig>("vip_tag");
            foreach (var (name, list) in config.TagSets)
                sets[name] = list.Where(IsUsable).ToList();
            _tagSets = sets;

            foreach (var (steamId, value) in config.Players)
            {
                if (!ulong.TryParse(steamId, out var steamId64))
                {
                    _plugin.Logger.LogWarning("[VIP_Tag] vip_tag.json: '{SteamId}' is not a SteamID64, skipped.", steamId);
                    continue;
                }

                var list = ParseTags(value);
                if (list.Count > 0) tags[steamId64] = list;
            }
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError("[VIP_Tag] Failed to load vip_tag.json: {Error}", e.Message);
        }

        _tagSets = sets;
        _personalTags = tags;
    }

    private List<TagValue> ParseTags(JsonElement element)
    {
        var tags = new List<TagValue>();
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    tags.AddRange(ParseTags(item));
                break;

            case JsonValueKind.String:
                var name = element.GetString() ?? "";
                if (_tagSets.TryGetValue(name, out var set))
                    tags.AddRange(set);
                else if (_missingSets.Add(name))
                    _plugin.Logger.LogWarning("[VIP_Tag] Tag set '{Name}' not found in vip_tag.json.", name);
                break;

            case JsonValueKind.Object:
                try
                {
                    if (element.Deserialize<TagValue>(JsonOptions) is { } tag && IsUsable(tag))
                        tags.Add(tag);
                }
                catch (JsonException) { }
                break;
        }

        return tags;
    }

    private static bool IsUsable(TagValue tag) =>
        !string.IsNullOrEmpty(tag.ChatTag) || !string.IsNullOrEmpty(tag.ScoreboardTag);

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

    private List<TagValue> GetAvailableTags(CCSPlayerController player)
    {
        var tags = new List<TagValue>();
        if (_personalTags.TryGetValue(player.SteamID, out var personal))
            tags.AddRange(personal);

        if (PlayerHasFeature(player))
        {
            try { tags.AddRange(ParseTags(GetFeatureValue<JsonElement>(player))); }
            catch { }
        }

        return tags.DistinctBy(TagKey).ToList();
    }

    private TagValue? GetActiveTag(CCSPlayerController player)
    {
        var tags = GetAvailableTags(player);
        if (tags.Count == 0) return null;

        var choice = GetPlayerCookie<string>(player.SteamID, ChoiceCookie);
        if (choice == ChoiceOff) return null;

        return tags.FirstOrDefault(t => TagKey(t) == choice) ?? tags[0];
    }

    private static string TagKey(TagValue tag) => $"{tag.ScoreboardTag}|{tag.ChatTag}";

    private static string TagName(TagValue tag)
    {
        var name = TagColors.StripTokens(tag.ChatTag);
        return string.IsNullOrEmpty(name) ? tag.ScoreboardTag : name;
    }

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


    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        OpenTagMenu(player);
    }

    private void OpenTagMenu(CCSPlayerController player)
    {
        var tags = GetAvailableTags(player);
        if (tags.Count == 0) return;

        var current = GetActiveTag(player);
        var menu = CreateMenu(GetTranslatedText(Feature));

        menu.AddMenuOption(GetTranslatedText("tag.Disable"), (controller, _) =>
        {
            SetPlayerCookie(controller.SteamID, ChoiceCookie, ChoiceOff);
            PrintToChat(controller, GetTranslatedText("tag.Off"));
            ApplyScoreboard(controller);
        }, current == null);

        foreach (var tag in tags)
        {
            var key = TagKey(tag);
            menu.AddMenuOption(TagName(tag), (controller, _) =>
            {
                SetPlayerCookie(controller.SteamID, ChoiceCookie, key);
                PrintToChat(controller, GetTranslatedText("tag.On", TagName(tag)));
                ApplyScoreboard(controller);
            }, current != null && TagKey(current) == key);
        }

        menu.Open(player);
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

        var tag = GetActiveTag(player);
        if (tag == null)
            return HookResult.Continue;

        // A gagged / silenced player sends nothing - swallow it so the tag can't bypass the mute.
        if (IsChatBlocked(player))
            return HookResult.Handled;

        if (string.IsNullOrEmpty(tag.ChatTag))
            return HookResult.Continue;

        var team = player.Team;
        var tagColor = TagColors.ColorChar(tag.ColorTag, team);
        var nameColor = TagColors.ColorChar(tag.NameColor, team);
        var msgColor = TagColors.ColorChar(tag.MessageColor, team);

        var teamPrefix = teamOnly ? $"{ChatColors.Default}(team) " : "";
        var line = $" {tagColor}{tag.ChatTag} {teamPrefix}{nameColor}{player.PlayerName}{ChatColors.Default}: {msgColor}{message}";

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

        if (GetAvailableTags(player).Count == 0) return;

        player.Clan = GetActiveTag(player)?.ScoreboardTag ?? string.Empty;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
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
