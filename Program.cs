using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace HDRReset
{
    internal class Program
    {
        // ============================================================
        // DDC/CI
        // ============================================================

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData);

        private delegate bool MonitorEnumProc(
            IntPtr hMonitor,
            IntPtr hdcMonitor,
            IntPtr lprcMonitor,
            IntPtr dwData);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            out uint pdwNumberOfPhysicalMonitors);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool DestroyPhysicalMonitors(
            uint dwPhysicalMonitorArraySize,
            [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool SetVCPFeature(
            IntPtr hMonitor,
            byte bVCPCode,
            uint dwNewValue);

        // ============================================================
        // HDR 参数
        // ============================================================

        const byte HDR_VCP_CODE = 0xEF;

        // HDR Photo
        const uint HDR_PHOTO = 0x09;

        // ============================================================
        // 自动刷新
        // ============================================================

        static readonly TimeSpan RefreshInterval =
            TimeSpan.FromMinutes(30);

        static readonly object RefreshLock =
            new object();

        static System.Threading.Timer? refreshTimer;

        static DateTime lastRefreshCompleted =
            DateTime.MinValue;

        static DateTime nextRefreshTime =
            DateTime.MinValue;

        static bool isRefreshing = false;

        static MainWindow? mainWindow;

        // ============================================================
        // 程序入口
        // ============================================================

        [STAThread]
        static void Main()
        {
            // ========================================================
            // 防止程序重复运行
            // ========================================================

            using Mutex mutex = new Mutex(
                true,
                "HDRReset_SingleInstance",
                out bool isNewInstance);

            if (!isNewInstance)
            {
                return;
            }

            // ========================================================
            // WPF 应用
            //
            // WPF/XAML 负责 GUI。
            // 核心 DDC/CI、Timer、刷新锁逻辑保持原来的结构。
            // ========================================================

            System.Windows.Application app =
                new System.Windows.Application
                {
                    ShutdownMode =
                        ShutdownMode.OnExplicitShutdown
                };

            mainWindow = new MainWindow();

            // 创建托盘图标。
            mainWindow.CreateTrayIcon();

            // --------------------------------------------------------
            // 不调用 Show()。
            // 程序启动时只运行在系统托盘。
            // --------------------------------------------------------

            mainWindow.Dispatcher.BeginInvoke(
                new Action(
                    delegate
                    {
                        ExecuteRefresh("启动");
                    }));

            // 不把 MainWindow 作为 Run(Window) 参数，
            // 避免 WPF 自动显示窗口。
            app.Run();

            mainWindow.DisposeTrayIcon();
        }

        // ============================================================
        // HDR 刷新
        // ============================================================

        static void ExecuteRefresh(string type)
        {
            lock (RefreshLock)
            {
                ExecuteRefreshLocked(type);
            }
        }

        static void ExecuteRefreshLocked(string type)
        {
            if (isRefreshing)
                return;

            isRefreshing = true;

            bool success = false;
            string? errorMessage = null;

            try
            {
                success =
                    SetAllMonitorsVcp(
                        HDR_PHOTO,
                        out errorMessage);
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }

            // --------------------------------------------------------
            // 本次刷新完成后重新开始 30 分钟计时
            // --------------------------------------------------------

            lastRefreshCompleted = DateTime.Now;

            nextRefreshTime =
                lastRefreshCompleted +
                RefreshInterval;

            // --------------------------------------------------------
            // 重新设置计时器
            // --------------------------------------------------------

            if (refreshTimer == null)
            {
                refreshTimer =
                    new System.Threading.Timer(
                        RefreshTimerCallback,
                        null,
                        RefreshInterval,
                        Timeout.InfiniteTimeSpan);
            }
            else
            {
                refreshTimer.Change(
                    RefreshInterval,
                    Timeout.InfiniteTimeSpan);
            }

            isRefreshing = false;

            // --------------------------------------------------------
            // 更新界面
            // --------------------------------------------------------

            if (mainWindow != null &&
                !mainWindow.Dispatcher.HasShutdownStarted)
            {
                mainWindow.AddRefreshRecord(
                    lastRefreshCompleted,
                    type,
                    nextRefreshTime,
                    success,
                    errorMessage);
            }
        }

        static void RefreshTimerCallback(object? state)
        {
            lock (RefreshLock)
            {
                TimeSpan remaining =
                    nextRefreshTime -
                    DateTime.Now;

                if (remaining > TimeSpan.Zero)
                {
                    refreshTimer?.Change(
                        remaining,
                        Timeout.InfiniteTimeSpan);

                    return;
                }

                ExecuteRefreshLocked("自动");
            }
        }

        // ============================================================
        // DDC/CI 设置 HDR Photo
        // ============================================================

        static bool SetAllMonitorsVcp(
            uint value,
            out string? errorMessage)
        {
            string? localErrorMessage = null;

            bool foundMonitor = false;
            bool allSuccess = true;

            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,

                (
                    hMonitor,
                    hdcMonitor,
                    lprcMonitor,
                    dwData) =>
                {
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(
                        hMonitor,
                        out uint monitorCount))
                    {
                        int error =
                            Marshal.GetLastWin32Error();

                        allSuccess = false;

                        localErrorMessage =
                            "获取物理显示器数量失败，" +
                            "错误代码：" +
                            error;

                        return true;
                    }

                    if (monitorCount == 0)
                        return true;

                    PHYSICAL_MONITOR[] monitors =
                        new PHYSICAL_MONITOR[monitorCount];

                    if (!GetPhysicalMonitorsFromHMONITOR(
                        hMonitor,
                        monitorCount,
                        monitors))
                    {
                        int error =
                            Marshal.GetLastWin32Error();

                        allSuccess = false;

                        localErrorMessage =
                            "获取物理显示器失败，" +
                            "错误代码：" +
                            error;

                        return true;
                    }

                    try
                    {
                        foreach (
                            PHYSICAL_MONITOR monitor
                            in monitors)
                        {
                            foundMonitor = true;

                            bool success =
                                SetVCPFeature(
                                    monitor.hPhysicalMonitor,
                                    HDR_VCP_CODE,
                                    value);

                            if (!success)
                            {
                                int error =
                                    Marshal.GetLastWin32Error();

                                allSuccess = false;

                                string description =
                                    monitor.szPhysicalMonitorDescription;

                                if (string.IsNullOrWhiteSpace(
                                    description))
                                {
                                    description = "未知显示器";
                                }

                                localErrorMessage =
                                    "显示器「" +
                                    description +
                                    "」设置 HDR Photo 失败，" +
                                    "VCP=0x" +
                                    HDR_VCP_CODE.ToString("X2") +
                                    "，Value=0x" +
                                    value.ToString("X2") +
                                    "，错误代码：" +
                                    error;
                            }
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(
                            monitorCount,
                            monitors);
                    }

                    return true;
                },

                IntPtr.Zero);

            if (!foundMonitor)
            {
                localErrorMessage =
                    "没有找到可用的物理显示器。";

                allSuccess = false;
            }

            errorMessage = localErrorMessage;

            return allSuccess;
        }

        // ============================================================
        // 手动刷新
        // ============================================================

        static void ManualRefresh()
        {
            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    ExecuteRefresh("手动");
                });
        }

        // ============================================================
        // GUI 调用入口
        // ============================================================

        internal static void RequestManualRefresh()
        {
            ManualRefresh();
        }

        internal static void Stop()
        {
            lock (RefreshLock)
            {
                if (refreshTimer != null)
                {
                    refreshTimer.Dispose();
                    refreshTimer = null;
                }
            }
        }
    }
}