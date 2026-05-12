using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using D2BotNG.Logging;
using static D2BotNG.Windows.NativeMethods;
using static D2BotNG.Windows.NativeTypes;
using ILogger = Serilog.ILogger;

namespace D2BotNG.Windows;

public static class Extensions
{
    private static readonly ILogger Logger = TrackingLoggerFactory.ForContext(typeof(Extensions));

    /// <summary>
    /// Window class names recognized as "the game" by <c>GameWindow</c>. Add new
    /// game variants here as we support them — Project Diablo 2, D2R if we ever do, etc.
    /// </summary>
    private static readonly HashSet<string> GameWindowClassNames = new(StringComparer.Ordinal)
    {
        "Diablo II",
    };

    extension(Process proc)
    {
        /// <summary>
        /// Every top-level window owned by the process.
        /// </summary>
        public IReadOnlyList<nint> TopLevelWindows
        {
            get
            {
                var pid = (uint)proc.Id;
                var windows = new List<nint>();
                EnumWindows((hWnd, _) =>
                {
                    GetWindowThreadProcessId(hWnd, out var windowPid);
                    if (windowPid == pid) windows.Add(hWnd);
                    return true;
                }, 0);
                return windows;
            }
        }

        /// <summary>
        /// The game's primary window, identified by class name match against known game
        /// variants (see <see cref="GameWindowClassNames"/>). Falls back to
        /// <see cref="Process.MainWindowHandle"/> when no top-level window matches — that's
        /// reliable at launch time before the game has spawned secondary windows.
        /// </summary>
        /// <remarks>
        /// We avoid <see cref="Process.MainWindowHandle"/> for ongoing operations because
        /// it uses a heuristic (first top-level window meeting certain style criteria) that
        /// can drift to a non-game window as the game's window set evolves — especially
        /// after a handoff where the successor process inspects a long-running game and
        /// gets a different "main" than the predecessor saw at launch.
        /// </remarks>
        public nint GameWindow
        {
            get
            {
                var sb = new StringBuilder(256);
                foreach (var hwnd in proc.TopLevelWindows)
                {
                    sb.Clear();
                    var written = GetClassNameW(hwnd, sb, sb.Capacity);
                    if (written > 0 && GameWindowClassNames.Contains(sb.ToString()))
                    {
                        return hwnd;
                    }
                }

                return proc.MainWindowHandle;
            }
        }
    }

    /// <summary>
    /// Sends a WM_COPYDATA message to every top-level window owned by the process.
    /// D2BS only hooks one of them and only that hook will fire; the others receive
    /// an unfamiliar WM_COPYDATA and their default WndProc handling is harmless.
    /// We broadcast because the window D2BS is hooked on isn't reliably the one
    /// <see cref="Process.MainWindowHandle"/> returns (see <c>GameWindow</c>).
    /// </summary>
    public static bool SendMessage(this Process proc, MessageType messageType, string data)
    {
        var windows = proc.TopLevelWindows;
        if (windows.Count == 0)
        {
            Logger.Warning("SendMessage: no top-level windows found for PID {Pid}", proc.Id);
            return false;
        }

        Logger.Debug("Sending {MessageType} {Data} to PID {Pid} ({Count} windows)",
            messageType, data, proc.Id, windows.Count);

        var anySucceeded = false;
        foreach (var hwnd in windows)
        {
            if (SendCopyData(hwnd, messageType, data)) anySucceeded = true;
        }
        return anySucceeded;
    }

    private static bool SendCopyData(nint hwnd, MessageType messageType, string data)
    {
        // D2BS reads a null terminated string, add null byte at the end.
        var bytes = Encoding.ASCII.GetBytes(data + '\0');
        var pData = Marshal.AllocHGlobal(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, pData, bytes.Length);

            var copyData = new COPYDATASTRUCT
            {
                dwData = (nint)messageType,
                cbData = bytes.Length,
                lpData = pData
            };

            var pCopyData = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());
            try
            {
                Marshal.StructureToPtr(copyData, pCopyData, false);

                var result = SendMessageTimeout(
                    hwnd,
                    WM_COPYDATA,
                    0,
                    pCopyData,
                    SMTO_ABORTIFHUNG,
                    250,
                    out _);

                if (result == 0)
                {
                    Logger.Warning("Failed to send WM_COPYDATA to {Hwnd}", hwnd);
                    return false;
                }

                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(pCopyData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
        }
    }
}
