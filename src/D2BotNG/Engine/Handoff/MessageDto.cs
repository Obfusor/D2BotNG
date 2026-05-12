using System.Text.Json.Serialization;
using D2BotNG.Core.Protos;

namespace D2BotNG.Engine.Handoff;

/// <summary>
/// Snapshot shape for a single console message captured by
/// <c>MessageService</c> during process handoff. The protobuf
/// <c>Message</c> type can't be used directly here because it carries a
/// <c>Timestamp</c> well-known type that JSON-serializes awkwardly.
/// </summary>
internal sealed class MessageDto
{
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; }
    [JsonPropertyName("color")] public MessageColor Color { get; set; }
}
