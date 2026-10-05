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
        private bool allowClose = false;

        private readonly ObservableCollection<RefreshRecord> records =
            new ObservableCollection<RefreshRecord>();

        public MainWindow()
        {
            InitializeComponent();

            LogGrid.ItemsSource = records;

            Closing += MainWindow_Closing;
        }

        public void CreateTrayIcon()
        {
            if (trayIcon != null)
                return;

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "HDR 自动刷新 - 适用于 Legion Pro27UD10",
                Visible = true
            };

            trayIcon.MouseClick += TrayIcon_MouseClick;

            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem resetItem =
                new ToolStripMenuItem("刷新HDR");

            resetItem.Click += delegate
            {
                Program.RequestManualRefresh();
            };

            menu.Items.Add(resetItem);

            trayIcon.ContextMenuStrip = menu;
        }

        private void TrayIcon_MouseClick(
            object? sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            if (!IsVisible)
                Show();

            WindowState = WindowState.Normal;
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

            System.Windows.Application.Current.Shutdown();
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

        public void AddRefreshRecord(
            DateTime refreshTime,
            string type,
            DateTime nextTime,
            bool success,
            string? errorMessage)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
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

            records.Add(
                new RefreshRecord
                {
                    RefreshTime =
                        refreshTime.ToString("H:mm:ss"),

                    RefreshType =
                        type,

                    NextRefreshTime =
                        nextTime.ToString("H:mm:ss")
                });

            while (records.Count > 3)
            {
                records.RemoveAt(0);
            }

            if (success)
            {
                ErrorLabel.Text = "";
                ErrorLabel.Visibility =
                    Visibility.Collapsed;
            }
            else
            {
                string errorText;

                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorText =
                        "HDR 刷新失败，但没有获取到具体错误信息。";
                }
                else
                {
                    errorText = errorMessage;
                }

                ErrorLabel.Text =
                    "错误：" + errorText;

                ErrorLabel.Visibility =
                    Visibility.Visible;
            }

            if (records.Count > 0)
            {
                LogGrid.SelectedItem =
                    records[records.Count - 1];

                LogGrid.ScrollIntoView(
                    records[records.Count - 1]);
            }
        }

        public void DisposeTrayIcon()
        {
            if (trayIcon == null)
                return;

            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }
    }

    public sealed class RefreshRecord
    {
        public string RefreshTime { get; set; } = "";
        public string RefreshType { get; set; } = "";
        public string NextRefreshTime { get; set; } = "";
    }
}