using System.Text.Json.Serialization;

namespace D2BotNG.Engine.Handoff;

/// <summary>
/// Snapshot of <c>KeyListRepository</c>'s non-persisted state (round-robin
/// cursors and the transient "held" markers on individual keys). Stored in
/// the manifest under the participant's <c>Key</c>.
/// </summary>
internal sealed class KeyStateDto
{
    [JsonPropertyName("rotationCursors")] public Dictionary<string, int> RotationCursors { get; set; } = new();
    [JsonPropertyName("heldKeys")] public List<HeldKeyDto> HeldKeys { get; set; } = new();
}

internal sealed class HeldKeyDto
{
    [JsonPropertyName("keyList")] public string KeyList { get; set; } = "";
    [JsonPropertyName("keyName")] public string KeyName { get; set; } = "";
}
