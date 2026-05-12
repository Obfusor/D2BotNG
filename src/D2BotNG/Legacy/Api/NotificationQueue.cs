using System.Collections.Concurrent;
using System.Text.Json;
using D2BotNG.Engine.Handoff;
using D2BotNG.Legacy.Models;

namespace D2BotNG.Legacy.Api;

public class NotificationQueue : IHandoffParticipant
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LegacyResponse>> _queues = new();

    public void Enqueue(string username, LegacyResponse response)
    {
        _queues.GetOrAdd(username, _ => new ConcurrentQueue<LegacyResponse>()).Enqueue(response);
    }

    public List<LegacyResponse> DequeueAll(string username)
    {
        var results = new List<LegacyResponse>();
        if (_queues.TryGetValue(username, out var queue))
        {
            while (queue.TryDequeue(out var item))
            {
                results.Add(item);
            }
        }
        return results;
    }

    public string HandoffKey => "notifications";

    public Task<object?> SnapshotAsync()
    {
        var result = new Dictionary<string, List<LegacyResponse>>();
        foreach (var (user, queue) in _queues)
        {
            result[user] = queue.ToList();
        }
        return Task.FromResult<object?>(result);
    }

    public Task RestoreAsync(JsonElement payload, JsonSerializerOptions options)
    {
        var entries = payload.Deserialize<Dictionary<string, List<LegacyResponse>>>(options) ?? [];
        foreach (var (user, messages) in entries)
        {
            var queue = _queues.GetOrAdd(user, _ => new ConcurrentQueue<LegacyResponse>());
            foreach (var m in messages) queue.Enqueue(m);
        }
        return Task.CompletedTask;
    }
}
