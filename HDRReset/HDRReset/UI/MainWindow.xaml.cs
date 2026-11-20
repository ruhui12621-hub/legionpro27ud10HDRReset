using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace HDRReset
{
    public partial class MainWindow : Window
    {
        private readonly UiPipeClient pipeClient;
        private readonly ObservableCollection<RefreshRecord> records = new ObservableCollection<RefreshRecord>();
        private bool initializingHdrModeSelection;
        private byte? lastKnownHdrMode;
        private int? lastKnownIntervalSeconds;
        private RefreshResult? latestResult;

        internal MainWindow(UiPipeClient client)
        {
            pipeClient = client;
            InitializeComponent();

            initializingHdrModeSelection = true;
            HdrModeComboBox.SelectedValue = "09";
            initializingHdrModeSelection = false;

            RefreshIntervalMinutesTextBox.Text = "30";
            RefreshIntervalSecondsTextBox.Text = "0";
            LogGrid.ItemsSource = records;

            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            Closing += MainWindow_Closing;
            UpdateLanguage();
        }

        internal void OnHostMessage(PipeMessage message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnHostMessage(message)));
                return;
            }

            switch (message.Type)
            {
                case "Snapshot":
                    if (message.Snapshot != null)
                        ApplySnapshot(message.Snapshot);
                    break;
                case "Activate":
                    ActivateWindow();
                    break;
                case "HostClosing":
                    WpfApplication.Current.Shutdown();
                    break;
            }
        }

        private void ApplySnapshot(HostSnapshot snapshot)
        {
            if (lastKnownHdrMode != snapshot.HdrMode)
            {
                initializingHdrModeSelection = true;
                HdrModeComboBox.SelectedValue = snapshot.HdrMode.ToString("X2", CultureInfo.InvariantCulture);
                initializingHdrModeSelection = false;
                lastKnownHdrMode = snapshot.HdrMode;
            }

            if (lastKnownIntervalSeconds != snapshot.RefreshIntervalSeconds)
            {
                int totalSeconds = Math.Max(0, snapshot.RefreshIntervalSeconds);
                RefreshIntervalMinutesTextBox.Text = (totalSeconds / 60).ToString(CultureInfo.InvariantCulture);
                RefreshIntervalSecondsTextBox.Text = (totalSeconds % 60).ToString(CultureInfo.InvariantCulture);
                lastKnownIntervalSeconds = snapshot.RefreshIntervalSeconds;
            }

            records.Clear();
            foreach (RefreshResult result in snapshot.Records.OrderBy(r => r.RefreshTime))
            {
                records.Add(CreateRefreshRecord(result));
            }

            latestResult = snapshot.Records.Length > 0
                ? snapshot.Records.OrderBy(r => r.RefreshTime).Last()
                : null;
            if (latestResult != null)
                UpdateErrorDisplay(latestResult);
            else
                ClearErrorDisplay();

            if (records.Count > 0)
            {
                LogGrid.SelectedItem = records[records.Count - 1];
                LogGrid.ScrollIntoView(records[records.Count - 1]);
            }
        }

        private static RefreshRecord CreateRefreshRecord(RefreshResult result)
        {
            return new RefreshRecord
            {
                RefreshTime = result.RefreshTime.ToString("H:mm:ss"),
                RefreshType = LanguageManager.GetRefreshTypeText(result.Type),
                NextRefreshTime = result.NextRefreshTime.ToString("H:mm:ss"),
                Type = result.Type
            };
        }

        private void ClearErrorDisplay()
        {
            ErrorLabel.Text = string.Empty;
            ErrorLabel.Visibility = Visibility.Collapsed;
        }

        private void UpdateErrorDisplay(RefreshResult result)
        {
            if (result.Success)
            {
                ClearErrorDisplay();
                return;
            }

            ErrorLabel.Text = GetString("ErrorPrefix") + LanguageManager.GetErrorText(result);
            ErrorLabel.Visibility = Visibility.Visible;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = pipeClient.SendAsync(new PipeMessage { Type = "ManualRefresh" });
        }

        private async void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            await pipeClient.SendAsync(new PipeMessage { Type = "Shutdown" });
            WpfApplication.Current.Shutdown();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 输入刷新间隔时，数字 1 作为文本输入，不触发快捷刷新。
            if (e.OriginalSource is System.Windows.Controls.TextBox)
                return;

            if (e.Key == Key.D1 || e.Key == Key.NumPad1)
            {
                _ = pipeClient.SendAsync(new PipeMessage { Type = "ManualRefresh" });
                e.Handled = true;
            }
        }

        private void LanguageComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
                item.Tag is string language)
            {
                LanguageManager.SetLanguage(language);
            }
        }

        private void HdrModeComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (initializingHdrModeSelection)
                return;

            if (HdrModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
                item.Tag is string value &&
                byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte mode))
            {
                _ = pipeClient.SendAsync(new PipeMessage
                {
                    Type = "SetMode",
                    Mode = mode
                });
            }
        }

        private async void ApplyRefreshIntervalButton_Click(object sender, RoutedEventArgs e)
        {
            bool minutesValid = int.TryParse(
                RefreshIntervalMinutesTextBox.Text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int minutes);
            bool secondsValid = int.TryParse(
                RefreshIntervalSecondsTextBox.Text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int seconds);

            if (!minutesValid || !secondsValid || minutes < 0 || minutes > 99 ||
                seconds < 0 || seconds > 59 || (minutes * 60 + seconds) < 3)
            {
                WpfMessageBox.Show(this, GetString("InvalidRefreshInterval"),
                    GetString("RefreshIntervalValidationTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await pipeClient.SendAsync(new PipeMessage
            {
                Type = "SetInterval",
                Minutes = minutes,
                Seconds = seconds
            });

            // 规范化输入格式，即使用户应用的数值与当前设置相同也保持一致显示。
            RefreshIntervalMinutesTextBox.Text = minutes.ToString(CultureInfo.InvariantCulture);
            RefreshIntervalSecondsTextBox.Text = seconds.ToString(CultureInfo.InvariantCulture);
        }

        private void LanguageManager_LanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
            RefreshExistingRecords();
            if (latestResult != null)
                UpdateErrorDisplay(latestResult);
            _ = pipeClient.SendAsync(new PipeMessage
            {
                Type = "SetLanguage",
                Language = LanguageManager.CurrentLanguage
            });
        }

        private void UpdateLanguage()
        {
            Title = GetString("WindowTitle");
            RefreshButton.Content = GetString("RefreshButton");
            ExitButton.Content = GetString("ExitButton");
            LanguageLabel.Text = GetString("LanguageLabel");
            HdrModeLabel.Text = GetString("HdrModeLabel");
            RefreshRecordsTextBlock.Text = GetString("RefreshRecords");
            RefreshTimeHeader.Header = GetString("RefreshTimeHeader");
            TypeHeader.Header = GetString("TypeHeader");
            NextRefreshTimeHeader.Header = GetString("NextRefreshTimeHeader");

            int languageIndex = LanguageManager.CurrentLanguage == "en-US" ? 1 : 0;
            if (LanguageComboBox.SelectedIndex != languageIndex)
                LanguageComboBox.SelectedIndex = languageIndex;
        }

        private void RefreshExistingRecords()
        {
            foreach (RefreshRecord record in records)
                record.RefreshType = LanguageManager.GetRefreshTypeText(record.Type);
            LogGrid.Items.Refresh();
        }

        private string GetString(string key)
        {
            return WpfApplication.Current.TryFindResource(key)?.ToString() ?? key;
        }

        private void ActivateWindow()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            if (!IsVisible)
                Show();

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 关闭窗口即结束 UI 子进程；托盘宿主和刷新引擎继续运行。
            LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }
    }

    public sealed class RefreshRecord
    {
        public string RefreshTime { get; set; } = string.Empty;
        public string RefreshType { get; set; } = string.Empty;
        public string NextRefreshTime { get; set; } = string.Empty;
        internal RefreshType Type { get; set; }
    }
}
