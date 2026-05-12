namespace D2BotNG.Engine.Handoff;

/// <summary>
/// Carries handoff state from process startup into DI-built services.
/// In normal startup, <see cref="IsTakeover"/> is false. When the process is launched
/// with <c>--takeover</c>, this is populated with the duplicated job handle and parsed manifest.
/// </summary>
public class HandoffContext
{
    public bool IsTakeover { get; init; }
    public nint AdoptedJobHandle { get; init; }
    public HandoffManifest? Manifest { get; init; }
    public string? ManifestPath { get; init; }
}
