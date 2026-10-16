using System.Collections;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace VIP_Tag;

/// <summary>
/// Reads Iks_Admin's gag/silence state through its capability ("iksadmin:core" -> IIksAdminApi) using
/// reflection, so this module has NO compile-time dependency on IksAdminApi.dll. Iks_Admin keeps the
/// active chat punishments in memory as a List&lt;PlayerComm&gt; on the api's "Comms" property; each
/// PlayerComm has SteamId, MuteType (0=Mute, 1=Gag, 2=Silence), and IsExpired / IsUnbanned flags.
///
/// A player is blocked when they have an active (not expired, not unbanned) Gag or Silence.
/// </summary>
public sealed class IksAdminGagReader
{
    private readonly object _api;
    private readonly PropertyInfo? _commsProp;

    private IksAdminGagReader(object api, PropertyInfo commsProp)
    {
        _api = api;
        _commsProp = commsProp;
    }

    /// <summary>Resolves Iks_Admin if present. Returns null when the plugin/capability is absent.</summary>
    public static IksAdminGagReader? TryCreate()
    {
        try
        {
            var cap = new PluginCapability<object>("iksadmin:core");
            var api = cap.Get();
            if (api == null) return null;

            var commsProp = api.GetType().GetProperty("Comms", BindingFlags.Public | BindingFlags.Instance);
            if (commsProp == null) return null;

            return new IksAdminGagReader(api, commsProp);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True if the player has an active Gag or Silence in Iks_Admin.</summary>
    public bool IsGaggedOrSilenced(CCSPlayerController player)
    {
        try
        {
            var steamId = player.AuthorizedSteamID?.SteamId64.ToString() ?? player.SteamID.ToString();

            if (_commsProp!.GetValue(_api) is not IEnumerable comms)
                return false;

            foreach (var comm in comms)
            {
                if (comm == null) continue;
                var t = comm.GetType();

                var commSteam = t.GetProperty("SteamId")?.GetValue(comm) as string;
                if (commSteam != steamId) continue;

                var muteType = t.GetProperty("MuteType")?.GetValue(comm) as int? ?? -1;
                if (muteType is not (1 or 2)) continue; // 1 = Gag, 2 = Silence

                var isExpired = t.GetProperty("IsExpired")?.GetValue(comm) as bool? ?? false;
                var isUnbanned = t.GetProperty("IsUnbanned")?.GetValue(comm) as bool? ?? false;
                if (!isExpired && !isUnbanned)
                    return true;
            }
        }
        catch
        {
            // On any reflection mismatch, fail open (don't block) rather than break chat.
        }

        return false;
    }
}
