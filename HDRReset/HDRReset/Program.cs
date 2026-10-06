using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace HDRReset
{
    internal class Program
    {
        // ============================================================
        // 启动错误日志
        // ============================================================

        private static readonly string LogFile =
            Path.Combine(
                AppContext.BaseDirectory,
                "HDRReset.log");

        private static void WriteStartupError(
            Exception ex)
        {
            try
            {
                File.AppendAllText(
                    LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]" +
                    Environment.NewLine +
                    "HDRReset 启动异常" +
                    Environment.NewLine +
                    ex.ToString() +
                    Environment.NewLine +
                    "--------------------------------------------------" +
                    Environment.NewLine);
            }
            catch
            {
                // 日志写入失败时不再抛出异常。
            }
        }

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

        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Unicode)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;

            [MarshalAs(
                UnmanagedType.ByValTStr,
                SizeConst = 128)]
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
        // HDR
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
        // 稳定的 DDC/CI 操作结果
        //
        // 注意：
        // 这里不使用 out 参数。
        // 这样 EnumDisplayMonitors 的 lambda 就不会产生 CS1628。
        // ============================================================

        private sealed class VcpRefreshResult
        {
            public bool Success { get; init; }

            public RefreshError Error { get; init; }

            public int? Win32ErrorCode { get; init; }

            public string? MonitorDescription { get; init; }
        }

        // ============================================================
        // 程序入口
        // ============================================================

        [STAThread]
        static void Main()
        {
            try
            {
                // ====================================================
                // 单实例
                // ====================================================

                using Mutex mutex =
                    new Mutex(
                        true,
                        "HDRReset_SingleInstance",
                        out bool isNewInstance);

                if (!isNewInstance)
                {
                    return;
                }

                // ====================================================
                // WPF
                // ====================================================

                System.Windows.Application app =
                    new System.Windows.Application
                    {
                        ShutdownMode =
                            ShutdownMode.OnExplicitShutdown
                    };

                // ====================================================
                // 初始化语言
                // ====================================================

                LanguageManager.Initialize();

                // ====================================================
                // 创建主窗口
                // ====================================================

                mainWindow =
                    new MainWindow();

                // ====================================================
                // 创建原有托盘
                //
                // 不增加任何托盘功能。
                // ====================================================

                mainWindow.CreateTrayIcon();

                // ====================================================
                // 启动时立即刷新
                // ====================================================

                mainWindow.Dispatcher.BeginInvoke(
                    new Action(
                        delegate
                        {
                            ExecuteRefresh(
                                RefreshType.Startup);
                        }));

                // ====================================================
                // WPF 消息循环
                // ====================================================

                app.Run();

                // ====================================================
                // 退出时释放托盘
                // ====================================================

                mainWindow.DisposeTrayIcon();
            }
            catch (Exception ex)
            {
                WriteStartupError(ex);
            }
        }

        // ============================================================
        // HDR 刷新入口
        // ============================================================

        static void ExecuteRefresh(
            RefreshType type)
        {
            lock (RefreshLock)
            {
                ExecuteRefreshLocked(type);
            }
        }

        // ============================================================
        // HDR 刷新核心
        // ============================================================

        static void ExecuteRefreshLocked(
            RefreshType type)
        {
            if (isRefreshing)
                return;

            isRefreshing = true;

            bool success = false;

            RefreshError error =
                RefreshError.None;

            int? win32ErrorCode =
                null;

            string? monitorDescription =
                null;

            string? exceptionMessage =
                null;

            try
            {
                // ====================================================
                // 执行 DDC/CI
                // ====================================================

                VcpRefreshResult vcpResult =
                    SetAllMonitorsVcp(
                        HDR_PHOTO);

                success =
                    vcpResult.Success;

                error =
                    vcpResult.Error;

                win32ErrorCode =
                    vcpResult.Win32ErrorCode;

                monitorDescription =
                    vcpResult.MonitorDescription;
            }
            catch (Exception ex)
            {
                success = false;

                error =
                    RefreshError.UnexpectedException;

                exceptionMessage =
                    ex.Message;
            }

            // ========================================================
            // 刷新完成
            //
            // 手动刷新、启动刷新、自动刷新都会从这里重新计算
            // 下一次 30 分钟时间。
            // ========================================================

            lastRefreshCompleted =
                DateTime.Now;

            nextRefreshTime =
                lastRefreshCompleted +
                RefreshInterval;

            // ========================================================
            // 设置下一次自动刷新
            // ========================================================

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

            // ========================================================
            // 生成稳定的核心结果
            //
            // 这里不产生中文/英文。
            // ========================================================

            RefreshResult result =
                new RefreshResult
                {
                    Type =
                        type,

                    RefreshTime =
                        lastRefreshCompleted,

                    NextRefreshTime =
                        nextRefreshTime,

                    Success =
                        success,

                    Error =
                        error,

                    Win32ErrorCode =
                        win32ErrorCode,

                    MonitorDescription =
                        monitorDescription,

                    VcpCode =
                        HDR_VCP_CODE,

                    VcpValue =
                        HDR_PHOTO,

                    ExceptionMessage =
                        exceptionMessage
                };

            // ========================================================
            // 发送给 UI
            // ========================================================

            if (mainWindow != null &&
                !mainWindow.Dispatcher.HasShutdownStarted)
            {
                mainWindow.AddRefreshRecord(
                    result);
            }
        }

        // ============================================================
        // 自动刷新 Timer
        // ============================================================

        static void RefreshTimerCallback(
            object? state)
        {
            lock (RefreshLock)
            {
                TimeSpan remaining =
                    nextRefreshTime -
                    DateTime.Now;

                if (remaining >
                    TimeSpan.Zero)
                {
                    refreshTimer?.Change(
                        remaining,
                        Timeout.InfiniteTimeSpan);

                    return;
                }

                ExecuteRefreshLocked(
                    RefreshType.Automatic);
            }
        }

        // ============================================================
        // DDC/CI：设置所有物理显示器
        //
        // 注意：
        // 不使用任何 out 参数作为本函数参数。
        // ============================================================

        static VcpRefreshResult SetAllMonitorsVcp(
            uint value)
        {
            RefreshError error =
                RefreshError.None;

            int? win32ErrorCode =
                null;

            string? monitorDescription =
                null;

            bool foundMonitor =
                false;

            bool allSuccess =
                true;

            // ========================================================
            // 枚举显示器
            // ========================================================

            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (
                    hMonitor,
                    hdcMonitor,
                    lprcMonitor,
                    dwData) =>
                {
                    // =================================================
                    // 获取物理显示器数量
                    // =================================================

                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(
                        hMonitor,
                        out uint monitorCount))
                    {
                        int lastError =
                            Marshal.GetLastWin32Error();

                        allSuccess = false;

                        if (error ==
                            RefreshError.None)
                        {
                            error =
                                RefreshError
                                    .GetPhysicalMonitorCountFailed;

                            win32ErrorCode =
                                lastError;
                        }

                        return true;
                    }

                    if (monitorCount == 0)
                    {
                        return true;
                    }

                    // =================================================
                    // 获取物理显示器
                    // =================================================

                    PHYSICAL_MONITOR[] monitors =
                        new PHYSICAL_MONITOR[
                            monitorCount];

                    if (!GetPhysicalMonitorsFromHMONITOR(
                        hMonitor,
                        monitorCount,
                        monitors))
                    {
                        int lastError =
                            Marshal.GetLastWin32Error();

                        allSuccess = false;

                        if (error ==
                            RefreshError.None)
                        {
                            error =
                                RefreshError
                                    .GetPhysicalMonitorFailed;

                            win32ErrorCode =
                                lastError;
                        }

                        return true;
                    }

                    try
                    {
                        foreach (
                            PHYSICAL_MONITOR monitor
                            in monitors)
                        {
                            foundMonitor =
                                true;

                            // =========================================
                            // 设置 HDR Photo
                            // =========================================

                            bool success =
                                SetVCPFeature(
                                    monitor.hPhysicalMonitor,
                                    HDR_VCP_CODE,
                                    value);

                            if (!success)
                            {
                                int lastError =
                                    Marshal.GetLastWin32Error();

                                allSuccess =
                                    false;

                                if (error ==
                                    RefreshError.None)
                                {
                                    error =
                                        RefreshError
                                            .SetHdrPhotoFailed;

                                    win32ErrorCode =
                                        lastError;

                                    monitorDescription =
                                        monitor
                                            .szPhysicalMonitorDescription;
                                }
                            }
                        }
                    }
                    finally
                    {
                        // =============================================
                        // 释放物理显示器句柄
                        // =============================================

                        DestroyPhysicalMonitors(
                            monitorCount,
                            monitors);
                    }

                    return true;
                },
                IntPtr.Zero);

            // ========================================================
            // 没有找到物理显示器
            // ========================================================

            if (!foundMonitor)
            {
                error =
                    RefreshError.NoPhysicalMonitor;

                allSuccess =
                    false;
            }

            // ========================================================
            // 返回稳定结果对象
            // ========================================================

            return new VcpRefreshResult
            {
                Success =
                    allSuccess,

                Error =
                    error,

                Win32ErrorCode =
                    win32ErrorCode,

                MonitorDescription =
                    monitorDescription
            };
        }

        // ============================================================
        // 手动刷新
        // ============================================================

        static void ManualRefresh()
        {
            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    ExecuteRefresh(
                        RefreshType.Manual);
                });
        }

        // ============================================================
        // UI 调用
        // ============================================================

        internal static void RequestManualRefresh()
        {
            ManualRefresh();
        }

        // ============================================================
        // 停止程序
        // ============================================================

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