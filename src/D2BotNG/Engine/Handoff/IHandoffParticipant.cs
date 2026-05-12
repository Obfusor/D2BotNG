using System.Text.Json;

namespace D2BotNG.Engine.Handoff;

/// <summary>
/// Services that hold non-persisted in-memory state implement this to participate
/// in process handoff. Register them as <c>IHandoffParticipant</c> in DI; the
/// <see cref="HandoffManager"/> discovers them automatically.
/// </summary>
public interface IHandoffParticipant
{
    /// <summary>
    /// Stable unique key identifying this participant's payload in the manifest.
    /// Used as the dictionary key, so it must be unique across participants and
    /// remain stable across versions for backward compatibility.
    /// </summary>
    string HandoffKey { get; }

    /// <summary>
    /// Returns a snapshot object that the manifest will serialize to JSON, or
    /// null to skip this participant for this handoff. The snapshot type is the
    /// participant's choice; <see cref="RestoreAsync"/> must accept the same shape.
    /// </summary>
    Task<object?> SnapshotAsync();

    /// <summary>
    /// Restore from a previously-snapshotted payload. The participant deserializes
    /// the <see cref="JsonElement"/> into whatever concrete type it returned from
    /// <see cref="SnapshotAsync"/>.
    /// </summary>
    Task RestoreAsync(JsonElement payload, JsonSerializerOptions options);
}
