using D2BotNG.Engine.Handoff;

namespace D2BotNG.Engine;

/// <summary>
/// Hosted service that initializes and manages the profile and schedule engines
/// </summary>
public class EngineHostedService : IHostedService
{
    private readonly ProfileEngine _profileEngine;
    private readonly ScheduleEngine _scheduleEngine;
    private readonly HandoffContext _handoffContext;
    private readonly HandoffManager _handoffManager;

    public EngineHostedService(
        ProfileEngine profileEngine,
        ScheduleEngine scheduleEngine,
        HandoffContext handoffContext,
        HandoffManager handoffManager)
    {
        _profileEngine = profileEngine;
        _scheduleEngine = scheduleEngine;
        _handoffContext = handoffContext;
        _handoffManager = handoffManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _profileEngine.InitializeAsync();

        if (_handoffContext is { IsTakeover: true, Manifest: not null })
        {
            var manifest = _handoffContext.Manifest;
            var manifestPath = _handoffContext.ManifestPath;

            try
            {
                await _handoffManager.RestoreAsync(manifest);
                await _profileEngine.RehydrateAsync(manifest.Profiles);
            }
            finally
            {
                _handoffManager.CleanupManifest(manifestPath);
            }
        }

        _scheduleEngine.Start();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop scheduler first so it doesn't restart profiles during shutdown
        await _scheduleEngine.StopAsync(cancellationToken);
        await _profileEngine.StopAsync(cancellationToken);
    }
}
