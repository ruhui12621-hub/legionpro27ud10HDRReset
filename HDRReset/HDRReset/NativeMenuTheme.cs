using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HDRReset
{
    /// <summary>
    /// Enables the Windows native dark-menu mode. The uxtheme exports used here
    /// are undocumented Windows APIs; calls are optional and safely ignored
    /// when the current Windows build does not expose them.
    /// </summary>
    internal static class NativeMenuTheme
    {
        private enum PreferredAppMode
        {
            Default = 0,
            AllowDark = 1,
            ForceDark = 2,
            ForceLight = 3,
            Max = 4
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate PreferredAppMode SetPreferredAppModeDelegate(PreferredAppMode mode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FlushMenuThemesDelegate();

        private static IntPtr uxThemeModule;
        private static SetPreferredAppModeDelegate? setPreferredAppMode;
        private static FlushMenuThemesDelegate? flushMenuThemes;
        private static bool initialized;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string fileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr procedureNameOrOrdinal);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        internal static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            try
            {
                uxThemeModule = LoadLibraryW("uxtheme.dll");
                if (uxThemeModule != IntPtr.Zero)
                {
                    // uxtheme ordinal 135: SetPreferredAppMode
                    IntPtr setModeProc = GetProcAddress(uxThemeModule, new IntPtr(135));
                    if (setModeProc != IntPtr.Zero)
                    {
                        setPreferredAppMode = Marshal.GetDelegateForFunctionPointer<SetPreferredAppModeDelegate>(setModeProc);
                        // AllowDark follows the user's current Windows app theme (light or dark).
                        setPreferredAppMode(PreferredAppMode.AllowDark);
                    }

                    // uxtheme ordinal 136: FlushMenuThemes
                    IntPtr flushProc = GetProcAddress(uxThemeModule, new IntPtr(136));
                    if (flushProc != IntPtr.Zero)
                        flushMenuThemes = Marshal.GetDelegateForFunctionPointer<FlushMenuThemesDelegate>(flushProc);

                    flushMenuThemes?.Invoke();
                }

                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
            catch
            {
                // Native dark-menu support is best-effort; the standard Win32
                // menu remains usable even on Windows versions without these exports.
            }
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            try
            {
                // Windows chooses the light/dark palette for AllowDark; flushing
                // the cached menu theme makes the next popup reflect the new setting.
                flushMenuThemes?.Invoke();
            }
            catch { }
        }

        internal static void Shutdown()
        {
            if (!initialized)
                return;

            try { SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; } catch { }
            try { FreeLibrary(uxThemeModule); } catch { }

            uxThemeModule = IntPtr.Zero;
            setPreferredAppMode = null;
            flushMenuThemes = null;
            initialized = false;
        }
    }
}
