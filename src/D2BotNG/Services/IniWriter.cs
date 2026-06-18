using System.Text;
using D2BotNG.Core.Protos;
using D2BotNG.Data;
using D2BotNG.Utilities;

namespace D2BotNG.Services;

/// <summary>
/// Writes profile configurations to d2bs.ini file.
/// </summary>
public class IniWriter
{
    // Cross-process lock shared with every d2bsng instance. d2bsng takes a Win32
    // named mutex of this exact name; .NET's Mutex maps to the same kernel object,
    // so the two serialize against each other.
    private const string IniLockName = @"Local\d2bs-ini-lock";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
    private const int ReplaceAttempts = 5;
    private const int ReplaceRetryMs = 20;

    private readonly ILogger<IniWriter> _logger;
    private readonly Paths _paths;

    public IniWriter(
        ILogger<IniWriter> logger,
        Paths paths)
    {
        _logger = logger;
        _paths = paths;
    }

    /// <summary>
    /// Write all profiles to d2bs.ini.
    /// </summary>
    public Task WriteAsync(IReadOnlyList<Profile> profiles)
    {
        // A Win32 named mutex has thread affinity - it must be released by the
        // same thread that acquired it, and no await may sit between acquire and
        // release. So run the whole locked read-modify-write synchronously on one
        // pool thread rather than interleaving it with async file I/O.
        return Task.Run(() => WriteLocked(profiles));
    }

    private void WriteLocked(IReadOnlyList<Profile> profiles)
    {
        var iniPath = Path.Combine(_paths.D2BSDirectory, "d2bs.ini");
        if (!File.Exists(iniPath))
        {
            _logger.LogError("{iniPath} does not exist", iniPath);
            return;
        }

        // Serialize against d2bsng (and any other cooperating writer) on the
        // shared named mutex, then commit via temp file + atomic replace so no
        // reader ever sees a half-written file.
        using var mutex = new Mutex(false, IniLockName);
        var owned = false;
        string? tempPath = null;
        try
        {
            try
            {
                owned = mutex.WaitOne(LockTimeout);
            }
            catch (AbandonedMutexException)
            {
                // A holder crashed mid-transaction; we now own the mutex. The file
                // is still intact because every writer commits atomically.
                owned = true;
            }
            // Proceed even on timeout (!owned): the atomic replace keeps the file
            // safe, so the worst case is a lost update under pathological contention.

            const string marker = "; gateway=";
            var content = File.ReadAllText(iniPath);
            var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                _logger.LogError("Could not find marker '{marker}' in {iniPath}", marker, iniPath);
                return;
            }

            content = content[..(markerIndex + marker.Length)] + Environment.NewLine + Environment.NewLine;

            var sb = new StringBuilder(content.Length + profiles.Count * 256);
            sb.Append(content);

            foreach (var profile in profiles)
            {
                WriteProfileSection(sb, profile);
            }

            // Stage on a temp file in the same directory (same volume, so the swap
            // is atomic), then replace d2bs.ini.
            tempPath = Path.Combine(_paths.D2BSDirectory, Path.GetRandomFileName());
            File.WriteAllText(tempPath, sb.ToString(), Encoding.Unicode);
            ReplaceWithRetry(tempPath, iniPath);
            tempPath = null; // consumed by the successful replace

            _logger.LogDebug("Wrote d2bs.ini with {Count} profiles", profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write d2bs.ini");
        }
        finally
        {
            if (tempPath is not null)
            {
                TryDelete(tempPath);
            }

            if (owned)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    // Atomically swap the staged temp file over d2bs.ini. A reader that briefly
    // holds the file open (the game-side GetPrivateProfileString) can trip a
    // sharing violation, so retry a few times before giving up.
    private static void ReplaceWithRetry(string tempPath, string iniPath)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                // iniPath is known to exist; File.Replace is atomic on NTFS and
                // preserves the destination's attributes.
                File.Replace(tempPath, iniPath, null);
                return;
            }
            catch (IOException) when (attempt < ReplaceAttempts - 1)
            {
                Thread.Sleep(ReplaceRetryMs);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Best-effort cleanup of the staged temp file; nothing to do on failure.
        }
    }

    private static void WriteProfileSection(StringBuilder sb, Profile profile)
    {
        var difficulty = profile.Difficulty.ToIniString();
        var scriptPath = "kolbot"; // Default bot library folder name
        var entryScript = Path.GetFileName(profile.EntryScript);

        sb.AppendLine($"[{profile.Name}]");
        sb.AppendLine($"Mode={profile.Mode.ToIniString()}");
        sb.AppendLine($"Username={profile.Account}");
        sb.AppendLine($"Password={profile.Password}");
        sb.AppendLine($"gateway={profile.Realm.ToIniString()}");
        sb.AppendLine($"character={profile.Character}");
        sb.AppendLine($"ScriptPath={scriptPath}");
        sb.AppendLine("DefaultGameScript=default.dbj");
        sb.AppendLine($"DefaultStarterScript={entryScript}");
        sb.AppendLine($"spdifficulty={difficulty}");
        sb.AppendLine();
    }
}
