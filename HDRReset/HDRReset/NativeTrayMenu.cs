using System;
using System.Runtime.InteropServices;

namespace HDRReset
{
    /// <summary>
    /// Creates a standard Win32 popup menu so Windows supplies its own menu
    /// font, DPI scaling, colors, and interaction behavior.
    /// </summary>
    internal static class NativeTrayMenu
    {
        internal const uint OpenMainPanelCommand = 1001;
        internal const uint RefreshHdrCommand = 1002;

        private const uint MfString = 0x00000000;
        private const uint MfSeparator = 0x00000800;
        private const uint TpmRightButton = 0x0002;
        private const uint TpmReturnCommand = 0x0100;
        private const uint WmNull = 0x0000;
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 (Windows 10 1703+).
        private static readonly IntPtr PerMonitorV2DpiContext = new IntPtr(-4);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", EntryPoint = "SetThreadDpiAwarenessContext", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr itemId, string? text);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint TrackPopupMenuEx(
            IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        internal static uint Show(IntPtr owner, string openMainPanelText, string refreshHdrText)
        {
            IntPtr previousDpiContext = IntPtr.Zero;
            bool restoreDpiContext = false;
            IntPtr menu = IntPtr.Zero;

            try
            {
                // A popup menu is created on the calling thread. Explicitly use
                // Per-Monitor V2 for its lifetime so Windows calculates native
                // menu metrics (including menu font size) for the active monitor.
                // The app manifest also sets process-wide Per-Monitor V2 awareness.
                try
                {
                    previousDpiContext = SetThreadDpiAwarenessContext(PerMonitorV2DpiContext);
                    restoreDpiContext = previousDpiContext != IntPtr.Zero;
                }
                catch (EntryPointNotFoundException)
                {
                    // Older Windows builds fall back to the process manifest.
                }

                menu = CreatePopupMenu();
                if (menu == IntPtr.Zero)
                    return 0;

                if (!AppendMenu(menu, MfString, new UIntPtr(OpenMainPanelCommand), openMainPanelText))
                    return 0;

                if (!AppendMenu(menu, MfSeparator, UIntPtr.Zero, null))
                    return 0;

                if (!AppendMenu(menu, MfString, new UIntPtr(RefreshHdrCommand), refreshHdrText))
                    return 0;

                if (!GetCursorPos(out NativePoint point))
                    return 0;

                // Required for notification-icon context menus to dismiss correctly.
                SetForegroundWindow(owner);
                uint command = TrackPopupMenuEx(
                    menu,
                    TpmRightButton | TpmReturnCommand,
                    point.X,
                    point.Y,
                    owner,
                    IntPtr.Zero);

                PostMessage(owner, WmNull, IntPtr.Zero, IntPtr.Zero);
                return command;
            }
            finally
            {
                if (menu != IntPtr.Zero)
                    DestroyMenu(menu);

                if (restoreDpiContext)
                {
                    try { SetThreadDpiAwarenessContext(previousDpiContext); }
                    catch { }
                }
            }
        }
    }
}
