using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Apmas.Server.Agents;

/// <summary>
/// Platform-specific native methods for process termination.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class NativeMethods
{
    /// <summary>
    /// Control event type for console applications.
    /// </summary>
    public enum CtrlType : uint
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    /// <summary>
    /// Attaches the calling process to the console of the specified process.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(uint dwProcessId);

    /// <summary>
    /// Detaches the calling process from its console.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeConsole();

    /// <summary>
    /// Sends a control signal to a console process group.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GenerateConsoleCtrlEvent(CtrlType dwCtrlEvent, uint dwProcessGroupId);

    /// <summary>
    /// Sets the control handler function for the calling process.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetConsoleCtrlHandler(IntPtr handlerRoutine, [MarshalAs(UnmanagedType.Bool)] bool add);
}
