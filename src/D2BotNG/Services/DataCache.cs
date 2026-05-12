using System.Collections.Concurrent;
using System.Text.Json;
using D2BotNG.Engine.Handoff;

namespace D2BotNG.Services;

/// <summary>
/// In-memory cache for D2BS store/retrieve/delete operations.
/// </summary>
public class DataCache : IHandoffParticipant
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public void Store(string key, string value)
    {
        _cache[key] = value;
    }

    public string? Retrieve(string key)
    {
        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    public bool Delete(string key)
    {
        return _cache.TryRemove(key, out _);
    }

    public string HandoffKey => "dataCache";

    public Task<object?> SnapshotAsync() => Task.FromResult<object?>(new Dictionary<string, string>(_cache));

    public Task RestoreAsync(JsonElement payload, JsonSerializerOptions options)
    {
        var entries = payload.Deserialize<Dictionary<string, string>>(options) ?? [];
        foreach (var (k, v) in entries)
            _cache[k] = v;
        return Task.CompletedTask;
    }
}
