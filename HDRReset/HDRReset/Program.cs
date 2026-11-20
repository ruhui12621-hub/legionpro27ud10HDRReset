using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Timer = System.Threading.Timer;

namespace HDRReset
{
    internal static class Program
    {
        private const string MutexName = "HDRReset_SingleInstance";
        private const byte HdrVcpCode = 0xEF;
        private const byte DefaultHdrMode = 0x09;
        private const byte HdrAuto = 0x01;
        private const byte HdrGame = 0x07;
        private const byte HdrMovie = 0x08;
        private const byte HdrPhoto = 0x09;
        private const byte HdrTb400 = 0x10;

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HDRReset");
        private static readonly string HdrModeFile = Path.Combine(SettingsDirectory, "hdr_mode.txt");
        private static readonly string RefreshIntervalFile = Path.Combine(SettingsDirectory, "refresh_interval.txt");
        private static readonly object RefreshLock = new object();
        private static readonly List<RefreshResult> History = new List<RefreshResult>(3);

        private static volatile byte currentHdrMode = DefaultHdrMode;
        private static TimeSpan refreshInterval = TimeSpan.FromMinutes(30);
        private static Timer? refreshTimer;
        private static DateTime nextRefreshTime = DateTime.MinValue;
        private static bool initialized;

        internal static event EventHandler? StateChanged;

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(
            IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitors(
            uint dwPhysicalMonitorArraySize,
            [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                // WPF 入口通过反射延迟到 --ui 子进程分支，托盘宿主不会直接引用 WPF 类型。
                if (args.Length > 0 && string.Equals(args[0], "--ui", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length >= 2)
                    {
                        Type? uiType = typeof(Program).Assembly.GetType("HDRReset.UiProcess", throwOnError: false);
                        MethodInfo? runMethod = uiType?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                        runMethod?.Invoke(null, new object[] { args[1] });
                    }
                    return;
                }

                using Mutex mutex = new Mutex(true, MutexName, out bool isNewInstance);
                if (!isNewInstance)
                    return;

                TrayHost.Run();
            }
            catch (Exception ex)
            {
                WriteStartupError(ex);
            }
        }

        private static void WriteStartupError(Exception ex)
        {
            try
            {
                string file = Path.Combine(AppContext.BaseDirectory, "HDRReset.log");
                File.AppendAllText(file,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] HDRReset 启动异常{Environment.NewLine}" +
                    ex + Environment.NewLine + new string('-', 50) + Environment.NewLine);
            }
            catch { }
        }

        internal static void Initialize()
        {
            if (initialized)
                return;

            LoadHdrMode();
            LoadRefreshInterval();
            initialized = true;
        }

        internal static HostSnapshot GetSnapshot()
        {
            lock (RefreshLock)
            {
                return new HostSnapshot
                {
                    HdrMode = currentHdrMode,
                    RefreshIntervalSeconds = (int)refreshInterval.TotalSeconds,
                    NextRefreshTime = nextRefreshTime,
                    Records = History.ConvertAll(r => new RefreshResult
                    {
                        Type = r.Type,
                        RefreshTime = r.RefreshTime,
                        NextRefreshTime = r.NextRefreshTime,
                        Success = r.Success,
                        Error = r.Error,
                        Win32ErrorCode = r.Win32ErrorCode,
                        MonitorDescription = r.MonitorDescription,
                        VcpCode = r.VcpCode,
                        VcpValue = r.VcpValue,
                        ExceptionMessage = r.ExceptionMessage
                    }).ToArray()
                };
            }
        }

        internal static bool IsSupportedHdrMode(byte mode) =>
            mode == HdrAuto || mode == HdrGame || mode == HdrMovie ||
            mode == HdrPhoto || mode == HdrTb400;

        private static void LoadHdrMode()
        {
            try
            {
                if (!File.Exists(HdrModeFile)) return;
                string text = File.ReadAllText(HdrModeFile).Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    text = text.Substring(2);

                if (byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte mode) &&
                    IsSupportedHdrMode(mode))
                    currentHdrMode = mode;
            }
            catch { }
        }

        private static bool IsValidRefreshInterval(int minutes, int seconds) =>
            minutes >= 0 && minutes <= 99 && seconds >= 0 && seconds <= 59 &&
            (minutes * 60 + seconds) >= 3;

        private static void LoadRefreshInterval()
        {
            try
            {
                if (!File.Exists(RefreshIntervalFile)) return;
                string[] parts = File.ReadAllText(RefreshIntervalFile).Trim().Split(',');
                if (parts.Length != 2) return;
                if (int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) &&
                    int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) &&
                    IsValidRefreshInterval(minutes, seconds))
                {
                    refreshInterval = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
                }
            }
            catch { }
        }

        internal static bool TrySetRefreshInterval(int minutes, int seconds)
        {
            if (!IsValidRefreshInterval(minutes, seconds))
                return false;

            TimeSpan newInterval = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            lock (RefreshLock)
            {
                if (refreshInterval == newInterval)
                    return true;

                refreshInterval = newInterval;
                try
                {
                    Directory.CreateDirectory(SettingsDirectory);
                    File.WriteAllText(RefreshIntervalFile,
                        minutes.ToString(CultureInfo.InvariantCulture) + "," +
                        seconds.ToString(CultureInfo.InvariantCulture));
                }
                catch { }

                if (refreshTimer != null)
                {
                    nextRefreshTime = DateTime.Now + refreshInterval;
                    refreshTimer.Change(refreshInterval, Timeout.InfiniteTimeSpan);
                    if (History.Count > 0)
                        History[History.Count - 1].NextRefreshTime = nextRefreshTime;
                }
            }

            NotifyStateChanged();
            return true;
        }

        internal static bool SetHdrMode(byte mode)
        {
            if (!IsSupportedHdrMode(mode) || currentHdrMode == mode)
                return false;

            currentHdrMode = mode;
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(HdrModeFile, mode.ToString("X2", CultureInfo.InvariantCulture));
            }
            catch { }

            NotifyStateChanged();
            return true;
        }

        internal static void RequestStartupRefresh()
        {
            ThreadPool.QueueUserWorkItem(_ => ExecuteRefresh(RefreshType.Startup));
        }

        internal static void RequestManualRefresh()
        {
            ThreadPool.QueueUserWorkItem(_ => ExecuteRefresh(RefreshType.Manual));
        }

        private static void ExecuteRefresh(RefreshType type)
        {
            lock (RefreshLock)
            {
                ExecuteRefreshLocked(type);
            }
            NotifyStateChanged();
        }

        private static void ExecuteRefreshLocked(RefreshType type)
        {
            bool success = false;
            RefreshError error = RefreshError.None;
            int? win32ErrorCode = null;
            string? monitorDescription = null;
            string? exceptionMessage = null;
            byte hdrMode = currentHdrMode;

            try
            {
                VcpRefreshResult result = SetAllMonitorsVcp(hdrMode);
                success = result.Success;
                error = result.Error;
                win32ErrorCode = result.Win32ErrorCode;
                monitorDescription = result.MonitorDescription;
            }
            catch (Exception ex)
            {
                error = RefreshError.UnexpectedException;
                exceptionMessage = ex.Message;
            }

            DateTime refreshedAt = DateTime.Now;
            nextRefreshTime = refreshedAt + refreshInterval;

            if (refreshTimer == null)
            {
                refreshTimer = new Timer(RefreshTimerCallback, null,
                    refreshInterval, Timeout.InfiniteTimeSpan);
            }
            else
            {
                refreshTimer.Change(refreshInterval, Timeout.InfiniteTimeSpan);
            }

            var refreshResult = new RefreshResult
            {
                Type = type,
                RefreshTime = refreshedAt,
                NextRefreshTime = nextRefreshTime,
                Success = success,
                Error = error,
                Win32ErrorCode = win32ErrorCode,
                MonitorDescription = monitorDescription,
                VcpCode = HdrVcpCode,
                VcpValue = hdrMode,
                ExceptionMessage = exceptionMessage
            };

            History.Add(refreshResult);
            while (History.Count > 3)
                History.RemoveAt(0);
        }

        private static void RefreshTimerCallback(object? state)
        {
            lock (RefreshLock)
            {
                TimeSpan remaining = nextRefreshTime - DateTime.Now;
                if (remaining > TimeSpan.Zero)
                {
                    refreshTimer?.Change(remaining, Timeout.InfiniteTimeSpan);
                    return;
                }

                ExecuteRefreshLocked(RefreshType.Automatic);
            }
            NotifyStateChanged();
        }

        private static void NotifyStateChanged()
        {
            // IPC 不参与 HDR 刷新锁，通知失败或 UI 未打开都不会影响刷新。
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { StateChanged?.Invoke(null, EventArgs.Empty); }
                catch { }
            });
        }

        private sealed class VcpRefreshResult
        {
            public bool Success { get; init; }
            public RefreshError Error { get; init; }
            public int? Win32ErrorCode { get; init; }
            public string? MonitorDescription { get; init; }
        }

        private static VcpRefreshResult SetAllMonitorsVcp(uint value)
        {
            RefreshError error = RefreshError.None;
            int? win32ErrorCode = null;
            string? monitorDescription = null;
            bool foundMonitor = false;
            bool allSuccess = true;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
                {
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint monitorCount))
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        allSuccess = false;
                        if (error == RefreshError.None)
                        {
                            error = RefreshError.GetPhysicalMonitorCountFailed;
                            win32ErrorCode = lastError;
                        }
                        return true;
                    }

                    if (monitorCount == 0)
                        return true;

                    PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[monitorCount];
                    if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, monitorCount, monitors))
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        allSuccess = false;
                        if (error == RefreshError.None)
                        {
                            error = RefreshError.GetPhysicalMonitorFailed;
                            win32ErrorCode = lastError;
                        }
                        return true;
                    }

                    try
                    {
                        foreach (PHYSICAL_MONITOR monitor in monitors)
                        {
                            foundMonitor = true;
                            bool setSuccess = SetVCPFeature(monitor.hPhysicalMonitor, HdrVcpCode, value);
                            if (!setSuccess)
                            {
                                int lastError = Marshal.GetLastWin32Error();
                                allSuccess = false;
                                if (error == RefreshError.None)
                                {
                                    error = RefreshError.SetHdrModeFailed;
                                    win32ErrorCode = lastError;
                                    monitorDescription = monitor.szPhysicalMonitorDescription;
                                }
                            }
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(monitorCount, monitors);
                    }

                    return true;
                }, IntPtr.Zero);

            if (!foundMonitor)
            {
                error = RefreshError.NoPhysicalMonitor;
                allSuccess = false;
            }

            return new VcpRefreshResult
            {
                Success = allSuccess,
                Error = error,
                Win32ErrorCode = win32ErrorCode,
                MonitorDescription = monitorDescription
            };
        }

        internal static void Stop()
        {
            lock (RefreshLock)
            {
                refreshTimer?.Dispose();
                refreshTimer = null;
            }
        }
    }
}
