using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Modules.Utils;

namespace VIP_Tag;

/// <summary>
/// Converts colour NAMES and {color}/{teamcolor} tokens into CS2 chat control characters, so the
/// chat tag can carry colours WITHOUT depending on ChatManager. Self-contained copy for this module.
/// </summary>
public static class TagColors
{
    private static readonly Regex TokenRegex = new(@"\{[a-zA-Z_ -]+\}", RegexOptions.Compiled);

    /// <summary>Expands {color} / {teamcolor} tokens into chat control chars. Unknown tokens stay.</summary>
    public static string ResolveTokens(string? text, CsTeam team)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return TokenRegex.Replace(text, m =>
        {
            var c = NameToChar(m.Value[1..^1], team);
            return c.HasValue ? c.Value.ToString() : m.Value;
        });
    }

    /// <summary>Removes every {color} token (for the plain-text scoreboard clan tag).</summary>
    public static string StripTokens(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return TokenRegex.Replace(text, string.Empty).Trim();
    }

    /// <summary>Resolves a colour NAME to a chat control char, Default when empty/unknown.</summary>
    public static char ColorChar(string? name, CsTeam team) => NameToChar(name, team) ?? ChatColors.Default;

    private static char? NameToChar(string? name, CsTeam team)
    {
        var n = (name ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("_", string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        if (n.Length == 0) return null;

        if (n is "teamcolor" or "team") return ChatColors.ForTeam(team);

        return n switch
        {
            "default" => ChatColors.Default,
            "white" => ChatColors.White,
            "darkred" => ChatColors.DarkRed,
            "green" => ChatColors.Green,
            "lightyellow" => ChatColors.LightYellow,
            "lightblue" => ChatColors.LightBlue,
            "olive" => ChatColors.Olive,
            "lime" => ChatColors.Lime,
            "red" => ChatColors.Red,
            "lightpurple" => ChatColors.LightPurple,
            "purple" => ChatColors.Purple,
            "grey" or "gray" => ChatColors.Grey,
            "yellow" => ChatColors.Yellow,
            "gold" => ChatColors.Gold,
            "silver" => ChatColors.Silver,
            "blue" => ChatColors.Blue,
            "darkblue" => ChatColors.DarkBlue,
            "bluegrey" or "bluegray" => ChatColors.BlueGrey,
            "magenta" => ChatColors.Magenta,
            "lightred" => ChatColors.LightRed,
            "orange" => ChatColors.Orange,
            _ => null
        };
    }
}
