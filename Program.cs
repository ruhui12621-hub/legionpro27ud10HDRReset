using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

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

    static System.Threading.Timer refreshTimer;

    static DateTime lastRefreshCompleted =
        DateTime.MinValue;

    static DateTime nextRefreshTime =
        DateTime.MinValue;

    static bool isRefreshing = false;

    static MainForm mainForm;


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
        // 高 DPI
        //
        // 必须在创建任何窗口之前设置
        // ========================================================

        Application.SetHighDpiMode(
            HighDpiMode.PerMonitorV2);

        ApplicationConfiguration.Initialize();


        mainForm = new MainForm();

        // 创建窗口句柄
        _ = mainForm.Handle;

        // 创建托盘图标
        mainForm.CreateTrayIcon();

        // --------------------------------------------------------
        // 注意：
        // 这里故意不调用 Show()
        // 也不调用 Hide()
        //
        // MainForm 的 allowShow 初始值为 false，
        // 所以窗口从程序启动开始就是隐藏状态。
        // --------------------------------------------------------

        // 启动后立即执行一次 HDR 刷新
        mainForm.BeginInvoke(
            new Action(
                delegate
                {
                    ExecuteRefresh("启动");
                }));

        Application.Run(mainForm);
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

        string errorMessage = null;

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

        lastRefreshCompleted =
            DateTime.Now;

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

        mainForm.AddRefreshRecord(
            lastRefreshCompleted,
            type,
            nextRefreshTime,
            success,
            errorMessage);
    }


    static void RefreshTimerCallback(
        object state)
    {
        lock (RefreshLock)
        {
            TimeSpan remaining =
                nextRefreshTime -
                DateTime.Now;

            if (remaining > TimeSpan.Zero)
            {
                refreshTimer.Change(
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
        out string errorMessage)
    {
        string localErrorMessage = null;

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
                    new PHYSICAL_MONITOR[
                        monitorCount];


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
                                monitor
                                    .szPhysicalMonitorDescription;


                            if (string.IsNullOrWhiteSpace(
                                description))
                            {
                                description =
                                    "未知显示器";
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


        errorMessage =
            localErrorMessage;


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
    // 主窗口
    // ============================================================

    public class MainForm : Form
    {
        NotifyIcon trayIcon;

        bool allowShow = false;

        DataGridView logGrid;

        Label errorLabel;


        public MainForm()
        {
            // ====================================================
            // 窗口标题
            // ====================================================

            Text =
                "HDR 自动刷新 - 适用于 Legion Pro27UD10";


            StartPosition =
                FormStartPosition.CenterScreen;


            AutoScaleMode =
                AutoScaleMode.Dpi;


            BackColor =
                Color.Black;

            ForeColor =
                Color.White;


            Size =
                new Size(
                    1500,
                    1050);


            MinimumSize =
                new Size(
                    1300,
                    900);


            Padding =
                new Padding(30);


            KeyPreview =
                true;


            FormClosing +=
                MainForm_FormClosing;


            KeyDown +=
                MainForm_KeyDown;


            BuildInterface();
        }


        // ========================================================
        // 控制窗口是否允许显示
        // ========================================================

        protected override void SetVisibleCore(
            bool value)
        {
            if (!allowShow)
            {
                base.SetVisibleCore(false);

                return;
            }


            base.SetVisibleCore(value);
        }


        // ========================================================
        // 点击 X
        // 隐藏到托盘
        // ========================================================

        void MainForm_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            if (e.CloseReason ==
                CloseReason.UserClosing)
            {
                e.Cancel = true;

                allowShow = false;

                Hide();
            }
        }


        // ========================================================
        // 托盘左键显示窗口
        // ========================================================

        public void ShowMainWindow()
        {
            allowShow = true;

            Show();

            WindowState =
                FormWindowState.Normal;

            BringToFront();

            Activate();
        }


        // ========================================================
        // 键盘
        // ========================================================

        void MainForm_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D1 ||
                e.KeyCode == Keys.NumPad1)
            {
                ManualRefresh();

                e.Handled = true;

                e.SuppressKeyPress = true;
            }
        }


        // ========================================================
        // 创建托盘
        // ========================================================

        public void CreateTrayIcon()
        {
            trayIcon =
                new NotifyIcon
                {
                    Icon =
                        SystemIcons.Application,

                    Text =
                        "HDR 自动刷新 - 适用于 Legion Pro27UD10",

                    Visible =
                        true
                };


            trayIcon.MouseClick +=
                delegate(
                    object sender,
                    MouseEventArgs e)
                {
                    if (e.Button ==
                        MouseButtons.Left)
                    {
                        ShowMainWindow();
                    }
                };


            ContextMenuStrip menu =
                new ContextMenuStrip();


            ToolStripMenuItem resetItem =
                new ToolStripMenuItem(
                    "刷新HDR");


            resetItem.Click +=
                delegate
                {
                    ManualRefresh();
                };


            menu.Items.Add(
                resetItem);


            trayIcon.ContextMenuStrip =
                menu;
        }


        // ========================================================
        // 构建界面
        // ========================================================

        void BuildInterface()
        {
            SuspendLayout();


            // ====================================================
            // 最外层
            // ====================================================

            TableLayoutPanel root =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,

                    ColumnCount = 1,

                    RowCount = 2,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    Margin =
                        new Padding(0),

                    Padding =
                        new Padding(0)
                };


            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));


            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));


            // ====================================================
            // 顶部
            // ====================================================

            TableLayoutPanel top =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Top,

                    AutoSize =
                        true,

                    AutoSizeMode =
                        AutoSizeMode.GrowAndShrink,

                    ColumnCount = 1,

                    RowCount = 2,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    Margin =
                        new Padding(0),

                    Padding =
                        new Padding(
                            0,
                            0,
                            0,
                            25)
                };


            top.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));


            top.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));


            // ====================================================
            // 使用说明
            // ====================================================

            Label instructionLabel =
                new Label
                {
                    AutoSize = true,

                    Dock =
                        DockStyle.Top,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            14F,
                            FontStyle.Regular),

                    Text =
                        "使用说明\r\n\r\n" +
                        "• 每 30 分钟自动刷新一次 HDR。\r\n" +
                        "• 按数字键 1 可立即刷新 HDR，并重新计时 30 分钟。",

                    TextAlign =
                        ContentAlignment.TopLeft,

                    Margin =
                        new Padding(
                            0,
                            0,
                            0,
                            25)
                };


            top.Controls.Add(
                instructionLabel,
                0,
                0);


            // ====================================================
            // 按钮
            // ====================================================

            FlowLayoutPanel buttonPanel =
                new FlowLayoutPanel
                {
                    AutoSize =
                        true,

                    AutoSizeMode =
                        AutoSizeMode.GrowAndShrink,

                    Dock =
                        DockStyle.Top,

                    FlowDirection =
                        FlowDirection.LeftToRight,

                    WrapContents =
                        false,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    Margin =
                        new Padding(0),

                    Padding =
                        new Padding(0)
                };


            // ----------------------------------------------------
            // 刷新 HDR
            // ----------------------------------------------------

            Button refreshButton =
                CreateLargeButton(
                    "刷新HDR",
                    260,
                    80);


            refreshButton.Click +=
                delegate
                {
                    ManualRefresh();
                };


            // ----------------------------------------------------
            // 结束程序
            // ----------------------------------------------------

            Button exitButton =
                CreateLargeButton(
                    "结束程序",
                    190,
                    80);


            exitButton.Click +=
                delegate
                {
                    if (trayIcon != null)
                    {
                        trayIcon.Visible = false;

                        trayIcon.Dispose();

                        trayIcon = null;
                    }


                    if (refreshTimer != null)
                    {
                        refreshTimer.Dispose();

                        refreshTimer = null;
                    }


                    allowShow = true;

                    Application.Exit();
                };


            refreshButton.Margin =
                new Padding(
                    0,
                    0,
                    25,
                    0);


            exitButton.Margin =
                new Padding(0);


            buttonPanel.Controls.Add(
                refreshButton);

            buttonPanel.Controls.Add(
                exitButton);


            top.Controls.Add(
                buttonPanel,
                0,
                1);


            root.Controls.Add(
                top,
                0,
                0);


            // ====================================================
            // 刷新记录
            // ====================================================

            TableLayoutPanel records =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,

                    ColumnCount = 1,

                    RowCount = 3,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    Margin =
                        new Padding(0),

                    Padding =
                        new Padding(0)
                };


            records.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));


            records.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));


            records.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));


            // ====================================================
            // 标题
            // ====================================================

            Label recordTitle =
                new Label
                {
                    AutoSize = true,

                    Dock =
                        DockStyle.Top,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            14F,
                            FontStyle.Bold),

                    Text =
                        "刷新记录",

                    TextAlign =
                        ContentAlignment.MiddleLeft,

                    Margin =
                        new Padding(
                            0,
                            0,
                            0,
                            15)
                };


            records.Controls.Add(
                recordTitle,
                0,
                0);


            // ====================================================
            // 错误
            // ====================================================

            errorLabel =
                new Label
                {
                    AutoSize = true,

                    Dock =
                        DockStyle.Top,

                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.Red,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            12F,
                            FontStyle.Regular),

                    Text = "",

                    Visible = false,

                    Margin =
                        new Padding(
                            0,
                            0,
                            0,
                            12)
                };


            records.Controls.Add(
                errorLabel,
                0,
                1);


            // ====================================================
            // 表格
            // ====================================================

            logGrid =
                new DataGridView
                {
                    Dock =
                        DockStyle.Fill,

                    ReadOnly = true,

                    AllowUserToAddRows =
                        false,

                    AllowUserToDeleteRows =
                        false,

                    AllowUserToResizeRows =
                        false,

                    AllowUserToResizeColumns =
                        false,

                    RowHeadersVisible =
                        false,

                    MultiSelect =
                        false,

                    SelectionMode =
                        DataGridViewSelectionMode.FullRowSelect,

                    AutoGenerateColumns =
                        false,

                    AutoSizeRowsMode =
                        DataGridViewAutoSizeRowsMode.None,

                    BackgroundColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    GridColor =
                        Color.FromArgb(
                            70,
                            70,
                            70),

                    BorderStyle =
                        BorderStyle.FixedSingle,

                    CellBorderStyle =
                        DataGridViewCellBorderStyle.SingleHorizontal,

                    EnableHeadersVisualStyles =
                        false,

                    Margin =
                        new Padding(0)
                };


            // ====================================================
            // 表头
            // ====================================================

            logGrid.ColumnHeadersDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor =
                        Color.FromArgb(
                            35,
                            35,
                            35),

                    ForeColor =
                        Color.White,

                    SelectionBackColor =
                        Color.FromArgb(
                            35,
                            35,
                            35),

                    SelectionForeColor =
                        Color.White,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            12F,
                            FontStyle.Bold),

                    Alignment =
                        DataGridViewContentAlignment.MiddleLeft
                };


            logGrid.ColumnHeadersHeight =
                56;


            // ====================================================
            // 单元格
            // ====================================================

            logGrid.DefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor =
                        Color.Black,

                    ForeColor =
                        Color.White,

                    SelectionBackColor =
                        Color.FromArgb(
                            55,
                            55,
                            55),

                    SelectionForeColor =
                        Color.White,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            12F,
                            FontStyle.Regular),

                    Alignment =
                        DataGridViewContentAlignment.MiddleLeft
                };


            logGrid.AlternatingRowsDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor =
                        Color.FromArgb(
                            15,
                            15,
                            15),

                    ForeColor =
                        Color.White
                };


            logGrid.RowTemplate.Height =
                56;


            // ====================================================
            // 刷新时间
            // ====================================================

            DataGridViewTextBoxColumn timeColumn =
                new DataGridViewTextBoxColumn
                {
                    Name =
                        "RefreshTime",

                    HeaderText =
                        "刷新时间",

                    Width = 300,

                    MinimumWidth = 250,

                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                };


            // ====================================================
            // 类型
            // ====================================================

            DataGridViewTextBoxColumn typeColumn =
                new DataGridViewTextBoxColumn
                {
                    Name =
                        "RefreshType",

                    HeaderText =
                        "类型",

                    Width = 180,

                    MinimumWidth = 150,

                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                };


            // ====================================================
            // 下一次刷新
            // ====================================================

            DataGridViewTextBoxColumn nextColumn =
                new DataGridViewTextBoxColumn
                {
                    Name =
                        "NextRefreshTime",

                    HeaderText =
                        "下一次刷新时间",

                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,

                    MinimumWidth = 350,

                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                };


            logGrid.Columns.Add(
                timeColumn);

            logGrid.Columns.Add(
                typeColumn);

            logGrid.Columns.Add(
                nextColumn);


            records.Controls.Add(
                logGrid,
                0,
                2);


            root.Controls.Add(
                records,
                0,
                1);


            Controls.Add(root);


            ResumeLayout(
                true);
        }


        // ========================================================
        // 创建大按钮
        // ========================================================

        Button CreateLargeButton(
            string text,
            int width,
            int height)
        {
            Button button =
                new Button
                {
                    Text =
                        text,

                    Width =
                        width,

                    Height =
                        height,

                    MinimumSize =
                        new Size(
                            width,
                            height),

                    AutoSize =
                        false,

                    // =================================================
                    // 使用兼容文字渲染
                    // 对混合中文 + Latin 字符的 DPI 显示更稳定
                    // =================================================
                    UseCompatibleTextRendering =
                        true,

                    BackColor =
                        Color.FromArgb(
                            35,
                            35,
                            35),

                    ForeColor =
                        Color.White,

                    FlatStyle =
                        FlatStyle.Flat,

                    Font =
                        new Font(
                            "Microsoft YaHei UI",
                            14F,
                            FontStyle.Bold),

                    TextAlign =
                        ContentAlignment.MiddleCenter,

                    UseVisualStyleBackColor =
                        false,

                    Margin =
                        new Padding(0)
                };


            button.FlatAppearance.BorderColor =
                Color.FromArgb(
                    90,
                    90,
                    90);


            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(
                    55,
                    55,
                    55);


            button.FlatAppearance.MouseDownBackColor =
                Color.FromArgb(
                    75,
                    75,
                    75);


            return button;
        }


        // ========================================================
        // 添加刷新记录
        // ========================================================

        public void AddRefreshRecord(
            DateTime refreshTime,
            string type,
            DateTime nextTime,
            bool success,
            string errorMessage)
        {
            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action(
                        delegate
                        {
                            AddRefreshRecord(
                                refreshTime,
                                type,
                                nextTime,
                                success,
                                errorMessage);
                        }));

                return;
            }


            // ----------------------------------------------------
            // H:mm:ss
            // 最多保存最近 3 条记录
            // ----------------------------------------------------

            logGrid.Rows.Add(
                refreshTime.ToString(
                    "H:mm:ss"),

                type,

                nextTime.ToString(
                    "H:mm:ss"));


            // ----------------------------------------------------
            // 超过 3 条时删除最早的一条
            // ----------------------------------------------------

            while (logGrid.Rows.Count > 3)
            {
                logGrid.Rows.RemoveAt(0);
            }


            // ====================================================
            // 成功
            // ====================================================

            if (success)
            {
                errorLabel.Text = "";

                errorLabel.Visible = false;
            }
            else
            {
                string errorText;


                if (string.IsNullOrWhiteSpace(
                    errorMessage))
                {
                    errorText =
                        "HDR 刷新失败，但没有获取到具体错误信息。";
                }
                else
                {
                    errorText =
                        errorMessage;
                }


                errorLabel.Text =
                    "错误：" +
                    errorText;


                errorLabel.Visible =
                    true;
            }


            // ====================================================
            // 选中最新记录
            // ====================================================

            foreach (
                DataGridViewRow row
                in logGrid.Rows)
            {
                row.Selected = false;
            }


            if (logGrid.Rows.Count > 0)
            {
                int last =
                    logGrid.Rows.Count - 1;


                logGrid.Rows[last].Selected =
                    true;


                logGrid.FirstDisplayedScrollingRowIndex =
                    last;
            }
        }
    }
}