using System.Text.Json;
using System.Text.Json.Serialization;

namespace VIP_Tag;

/// <summary>
/// Reads CS2-AdminPlus's communication_data.json to tell whether a player is currently gagged.
/// AdminPlus exposes no public API, so this file is the only integration point. The file is a JSON
/// array of punishment records; a "GAG" (AdminPlus stores a silence as GAG + MUTE) blocks chat.
/// The data is cached and reloaded only when the file's last-write time changes.
///
/// AdminPlus writes EndTime using the server's LOCAL time (DateTime.Now), with permanent
/// punishments as Duration 0, so active checks compare against DateTime.Now.
/// </summary>
public sealed class AdminPlusGagReader(string filePath)
{
    private readonly string _filePath = filePath;
    private DateTime _lastWriteUtc = DateTime.MinValue;
    private readonly HashSet<ulong> _gaggedSteamIds = [];
    private readonly object _lock = new();

    /// <summary>Does the AdminPlus data file exist (i.e. is AdminPlus installed)?</summary>
    public bool FileExists => File.Exists(_filePath);

    /// <summary>True if the given SteamID64 currently has an active GAG in AdminPlus.</summary>
    public bool IsGagged(ulong steamId64)
    {
        RefreshIfChanged();
        lock (_lock)
            return _gaggedSteamIds.Contains(steamId64);
    }

    private void RefreshIfChanged()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                lock (_lock) _gaggedSteamIds.Clear();
                return;
            }

            var writeUtc = File.GetLastWriteTimeUtc(_filePath);
            if (writeUtc == _lastWriteUtc)
                return;

            _lastWriteUtc = writeUtc;

            var json = File.ReadAllText(_filePath);
            var records = JsonSerializer.Deserialize<List<CommunicationPunishment>>(json) ?? [];

            var now = DateTime.Now;
            var active = records
                .Where(r => string.Equals(r.Type, "GAG", StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Duration == 0 || r.EndTime > now)
                .Select(r => r.SteamID);

            lock (_lock)
            {
                _gaggedSteamIds.Clear();
                foreach (var id in active)
                    _gaggedSteamIds.Add(id);
            }
        }
        catch
        {
            // Malformed/locked file: keep the previous snapshot rather than blocking chat.
        }
    }

    private sealed class CommunicationPunishment
    {
        [JsonPropertyName("SteamID")] public ulong SteamID { get; set; }
        [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("Duration")] public int Duration { get; set; }
        [JsonPropertyName("EndTime")] public DateTime EndTime { get; set; }
    }
}
