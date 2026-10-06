using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;

namespace HDRReset
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? trayIcon;

        // 托盘现有的“刷新HDR”菜单项
        private ToolStripMenuItem? resetItem;

        private bool allowClose = false;

        private readonly ObservableCollection<RefreshRecord> records =
            new ObservableCollection<RefreshRecord>();

        public MainWindow()
        {
            InitializeComponent();

            LogGrid.ItemsSource =
                records;

            Closing +=
                MainWindow_Closing;

            LanguageManager.LanguageChanged +=
                LanguageManager_LanguageChanged;

            UpdateLanguage();
        }

        // ============================================================
        // 托盘
        //
        // 功能保持不变：
        // 1. 左键：打开主窗口
        // 2. 右键：只有一个“刷新HDR”菜单
        //
        // 这里只让文字跟随语言切换。
        // ============================================================

        public void CreateTrayIcon()
        {
            if (trayIcon != null)
                return;

            trayIcon =
                new NotifyIcon
                {
                    Icon =
                        SystemIcons.Application,

                    Visible = true
                };

            trayIcon.MouseClick +=
                TrayIcon_MouseClick;

            ContextMenuStrip menu =
                new ContextMenuStrip();

            resetItem =
                new ToolStripMenuItem();

            resetItem.Click += delegate
            {
                Program.RequestManualRefresh();
            };

            menu.Items.Add(resetItem);

            trayIcon.ContextMenuStrip =
                menu;

            // 根据当前语言设置托盘文字
            UpdateTrayLanguage();
        }

        private void TrayIcon_MouseClick(
            object? sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button ==
                MouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            if (!IsVisible)
                Show();

            WindowState =
                WindowState.Normal;

            Activate();

            Topmost = true;
            Topmost = false;

            Focus();
        }

        private void MainWindow_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (!allowClose)
            {
                e.Cancel = true;

                Hide();
            }
        }

        // ============================================================
        // 按钮
        // ============================================================

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Program.RequestManualRefresh();
        }

        private void ExitButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            allowClose = true;

            Program.Stop();

            DisposeTrayIcon();

            System.Windows.Application
                .Current
                .Shutdown();
        }

        private void Window_KeyDown(
            object sender,
            System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.D1 ||
                e.Key == Key.NumPad1)
            {
                Program.RequestManualRefresh();

                e.Handled = true;
            }
        }

        // ============================================================
        // 刷新记录
        // ============================================================

        internal void AddRefreshRecord(
            RefreshResult result)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        delegate
                        {
                            AddRefreshRecord(
                                result);
                        }));

                return;
            }

            records.Add(
                new RefreshRecord
                {
                    RefreshTime =
                        result.RefreshTime
                            .ToString("H:mm:ss"),

                    RefreshType =
                        LanguageManager
                            .GetRefreshTypeText(
                                result.Type),

                    NextRefreshTime =
                        result.NextRefreshTime
                            .ToString("H:mm:ss"),

                    Type =
                        result.Type
                });

            while (records.Count > 3)
            {
                records.RemoveAt(0);
            }

            UpdateErrorDisplay(result);

            if (records.Count > 0)
            {
                LogGrid.SelectedItem =
                    records[records.Count - 1];

                LogGrid.ScrollIntoView(
                    records[records.Count - 1]);
            }
        }

        private void UpdateErrorDisplay(
            RefreshResult result)
        {
            if (result.Success)
            {
                ErrorLabel.Text = "";

                ErrorLabel.Visibility =
                    Visibility.Collapsed;

                return;
            }

            ErrorLabel.Text =
                GetString("ErrorPrefix") +
                LanguageManager
                    .GetErrorText(result);

            ErrorLabel.Visibility =
                Visibility.Visible;
        }

        // ============================================================
        // 语言
        // ============================================================

        private void LanguageComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem
                is System.Windows.Controls.ComboBoxItem item)
            {
                if (item.Tag is string language)
                {
                    LanguageManager.SetLanguage(
                        language);
                }
            }
        }

        private void LanguageManager_LanguageChanged(
            object? sender,
            EventArgs e)
        {
            UpdateLanguage();

            // 托盘文字同步切换
            UpdateTrayLanguage();

            RefreshExistingRecords();
        }

        private void UpdateLanguage()
        {
            Title =
                GetString("WindowTitle");

            RefreshButton.Content =
                GetString("RefreshButton");

            ExitButton.Content =
                GetString("ExitButton");

            LanguageLabel.Text =
                GetString("LanguageLabel");

            RefreshRecordsTextBlock.Text =
                GetString("RefreshRecords");

            RefreshTimeHeader.Header =
                GetString("RefreshTimeHeader");

            TypeHeader.Header =
                GetString("TypeHeader");

            NextRefreshTimeHeader.Header =
                GetString("NextRefreshTimeHeader");

            // 防止初始化时反复触发 SelectionChanged
            if (LanguageManager.CurrentLanguage ==
                "en-US")
            {
                if (LanguageComboBox.SelectedIndex != 1)
                    LanguageComboBox.SelectedIndex = 1;
            }
            else
            {
                if (LanguageComboBox.SelectedIndex != 0)
                    LanguageComboBox.SelectedIndex = 0;
            }
        }

        // ============================================================
        // 托盘语言
        // ============================================================

        private void UpdateTrayLanguage()
        {
            if (trayIcon == null)
                return;

            if (LanguageManager.CurrentLanguage ==
                "en-US")
            {
                trayIcon.Text =
                    "HDR Auto Refresh - For Legion Pro27UD10";

                if (resetItem != null)
                {
                    resetItem.Text =
                        "Refresh HDR";
                }
            }
            else
            {
                trayIcon.Text =
                    "HDR 自动刷新 - 适用于 Legion Pro27UD10";

                if (resetItem != null)
                {
                    resetItem.Text =
                        "刷新HDR";
                }
            }
        }

        private void RefreshExistingRecords()
        {
            foreach (RefreshRecord record in records)
            {
                record.RefreshType =
                    LanguageManager
                        .GetRefreshTypeText(
                            record.Type);
            }

            LogGrid.Items.Refresh();
        }

        private string GetString(
            string key)
        {
            return System.Windows.Application
                .Current
                .TryFindResource(key)
                ?.ToString()
                ?? key;
        }

        // ============================================================
        // 托盘释放
        // ============================================================

        public void DisposeTrayIcon()
        {
            if (trayIcon == null)
                return;

            trayIcon.Visible = false;

            trayIcon.Dispose();

            trayIcon = null;

            resetItem = null;
        }
    }

    public sealed class RefreshRecord
    {
        public string RefreshTime { get; set; } = "";

        public string RefreshType { get; set; } = "";

        public string NextRefreshTime { get; set; } = "";

        internal RefreshType Type { get; set; }
    }
}