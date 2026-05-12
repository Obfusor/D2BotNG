using System.Collections.Concurrent;
using System.Text.Json;
using D2BotNG.Engine.Handoff;

namespace D2BotNG.Legacy.Api;

public class SessionManager : IHandoffParticipant
{
    private readonly ConcurrentDictionary<string, string> _sessions = new();

    public string GetOrCreateSession(string clientIp, string userAgent)
    {
        var key = clientIp + "|" + userAgent;
        return _sessions.GetOrAdd(key, _ => AesEncryption.GenerateKey(32));
    }

    public string HandoffKey => "legacySessions";

    public Task<object?> SnapshotAsync() => Task.FromResult<object?>(new Dictionary<string, string>(_sessions));

    public Task RestoreAsync(JsonElement payload, JsonSerializerOptions options)
    {
        var sessions = payload.Deserialize<Dictionary<string, string>>(options) ?? [];
        foreach (var (k, v) in sessions) _sessions[k] = v;
        return Task.CompletedTask;
    }
}
