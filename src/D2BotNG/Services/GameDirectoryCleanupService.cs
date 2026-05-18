using D2BotNG.Data;

namespace D2BotNG.Services;

/// <summary>
/// Background service that periodically deletes old screenshots and BlizzardError
/// crash log directories from the configured Diablo II install path. Retention is
/// configured via <c>Settings.Game.ScreenshotRetentionDays</c> and
/// <c>Settings.Game.CrashLogRetentionDays</c>; 0 disables that cleanup.
/// </summary>
public class GameDirectoryCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<GameDirectoryCleanupService> _logger;
    private readonly SettingsRepository _settingsRepository;

    public GameDirectoryCleanupService(
        ILogger<GameDirectoryCleanupService> logger,
        SettingsRepository settingsRepository)
    {
        _logger = logger;
        _settingsRepository = settingsRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game directory cleanup service started");

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Game directory cleanup pass failed");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Game directory cleanup service stopped");
    }

    private async Task RunCleanupAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        var installPath = settings.Game?.D2InstallPath;
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var screenshotDays = settings.Game?.ScreenshotRetentionDays ?? 0;
        var crashLogDays = settings.Game?.CrashLogRetentionDays ?? 0;

        if (screenshotDays > 0)
        {
            CleanScreenshots(installPath, now - TimeSpan.FromDays(screenshotDays));
        }

        if (crashLogDays > 0)
        {
            CleanCrashLogs(installPath, now - TimeSpan.FromDays(crashLogDays));
        }
    }

    private void CleanScreenshots(string installPath, DateTime cutoffUtc)
    {
        var deleted = 0;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(installPath, "Screenshot*.jpg", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate screenshots in {Path}", installPath);
            return;
        }

        foreach (var file in files)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete screenshot {File}", file);
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old screenshot(s) from {Path}", deleted, installPath);
        }
    }

    private void CleanCrashLogs(string installPath, DateTime cutoffUtc)
    {
        var crashDir = Path.Combine(installPath, "BlizzardError");
        if (!Directory.Exists(crashDir)) return;

        var deleted = 0;
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(crashDir);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate crash logs in {Path}", crashDir);
            return;
        }

        foreach (var dir in dirs)
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) < cutoffUtc)
                {
                    Directory.Delete(dir, recursive: true);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete crash log directory {Dir}", dir);
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old crash log(s) from {Path}", deleted, crashDir);
        }
    }
}
