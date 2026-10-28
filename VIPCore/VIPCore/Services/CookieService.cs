using System.Text.Json;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Models;

namespace VIPCore.Services;

public class CookieService(Plugin plugin)
{
    private static readonly string FilePath = Path.Combine(ConfigSystem.CoreConfigDirectory, "vip_core_cookie.json");

    private Dictionary<ulong, PlayerCookie> _cookies = [];

    public void Set<T>(ulong steamId64, string key, T value)
    {
        if (!_cookies.TryGetValue(steamId64, out var cookie))
        {
            cookie = new PlayerCookie { SteamId64 = steamId64 };
            _cookies[steamId64] = cookie;
        }

        cookie.Features[key] = value!;
    }

    public T Get<T>(ulong steamId64, string key)
    {
        return TryGet<T>(steamId64, key, out var value) ? value : default!;
    }

    public bool TryGet<T>(ulong steamId64, string key, out T value)
    {
        value = default!;

        if (!_cookies.TryGetValue(steamId64, out var cookie) || !cookie.Features.TryGetValue(key, out var raw))
            return false;

        try
        {
            switch (raw)
            {
                case T typed:
                    value = typed;
                    return true;
                case JsonElement element:
                    value = element.Deserialize<T>(ConfigSystem.ConfigJsonOptions)!;
                    cookie.Features[key] = value!;
                    return true;
                default:
                    value = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(raw), ConfigSystem.ConfigJsonOptions)!;
                    return true;
            }
        }
        catch (Exception e)
        {
            plugin.Logger.LogError("Failed to deserialize cookie value '{key}': {e}", key, e);
            return false;
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, "[]");
                return;
            }

            var cookies = JsonSerializer.Deserialize<List<PlayerCookie>>(File.ReadAllText(FilePath),
                ConfigSystem.ConfigJsonOptions);

            _cookies = cookies?.ToDictionary(c => c.SteamId64) ?? [];
        }
        catch (Exception e)
        {
            plugin.Logger.LogError("Failed to load cookies: {e}", e);
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(_cookies.Values.ToList(), ConfigSystem.ConfigJsonOptions));
        }
        catch (Exception e)
        {
            plugin.Logger.LogError("Failed to save cookies: {e}", e);
        }
    }
}
