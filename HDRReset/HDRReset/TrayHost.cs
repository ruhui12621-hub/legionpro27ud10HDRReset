using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace HDRReset
{
    internal sealed class TrayHost : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly string pipeName;
        private string language = "zh-CN";
        private Process? uiProcess;
        private NamedPipeServerStream? pipe;
        private StreamReader? pipeReader;
        private StreamWriter? pipeWriter;
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        private readonly object pipeSync = new object();
        private readonly Control dispatcherControl = new Control();
        private int pipeGeneration;
        private volatile bool shuttingDown;

        private TrayHost()
        {
            pipeName = "HDRReset_UI_" + Environment.ProcessId.ToString();
            _ = dispatcherControl.Handle;
            language = LoadLanguage();

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true
            };
            trayIcon.MouseClick += TrayIcon_MouseClick;
            UpdateTrayLanguage();

            Program.StateChanged += Program_StateChanged;
            Program.Initialize();
            Program.RequestStartupRefresh();
        }

        internal static void Run()
        {
            // The manifest declares Per-Monitor V2 process awareness. Set the
            // WinForms high-DPI mode before creating any HWND as an additional
            // safeguard for its hidden dispatcher window.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            NativeMenuTheme.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayHost());
        }

        private static string LoadLanguage()
        {
            try
            {
                string file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HDRReset", "language.txt");
                string value = File.Exists(file) ? File.ReadAllText(file).Trim() : "zh-CN";
                return value == "en-US" ? "en-US" : "zh-CN";
            }
            catch
            {
                return "zh-CN";
            }
        }

        private void UpdateTrayLanguage()
        {
            if (language == "en-US")
            {
                trayIcon.Text = "HDR Auto Refresh - For Legion Pro27UD10";
            }
            else
            {
                trayIcon.Text = "HDR 自动刷新 - 适用于 Legion Pro27UD10";
            }
        }

        private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenUi();
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowNativeTrayMenu();
            }
        }

        private void ShowNativeTrayMenu()
        {
            string openText = language == "en-US" ? "Open main panel" : "打开主面板";
            string refreshText = language == "en-US" ? "Refresh HDR" : "刷新HDR";

            uint command = NativeTrayMenu.Show(dispatcherControl.Handle, openText, refreshText);
            switch (command)
            {
                case NativeTrayMenu.OpenMainPanelCommand:
                    OpenUi();
                    break;
                case NativeTrayMenu.RefreshHdrCommand:
                    Program.RequestManualRefresh();
                    break;
            }
        }

        private void OpenUi()
        {
            if (shuttingDown)
                return;

            if (uiProcess != null)
            {
                try
                {
                    if (!uiProcess.HasExited)
                    {
                        _ = SendToUiAsync(new PipeMessage { Type = "Activate" });
                        return;
                    }
                }
                catch { }
                uiProcess.Dispose();
                uiProcess = null;
            }

            CleanupPipe();
            int generation = Interlocked.Increment(ref pipeGeneration);
            var server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            lock (pipeSync)
                pipe = server;

            _ = AcceptUiAsync(server, generation);

            try
            {
                string executable = Environment.ProcessPath
                    ?? throw new InvalidOperationException("无法定位当前程序路径。");
                var startInfo = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory
                };
                startInfo.ArgumentList.Add("--ui");
                startInfo.ArgumentList.Add(pipeName);

                uiProcess = Process.Start(startInfo);
                if (uiProcess == null)
                    throw new InvalidOperationException("无法启动界面进程。");

                Process startedProcess = uiProcess ?? throw new InvalidOperationException("无法启动界面进程。");
                int startedPid = startedProcess.Id;
                startedProcess.EnableRaisingEvents = true;
                startedProcess.Exited += (_, _) =>
                {
                    try
                    {
                        dispatcherControl.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (uiProcess != null && uiProcess.Id == startedPid)
                                {
                                    uiProcess.Dispose();
                                    uiProcess = null;
                                }
                            }
                            catch { }
                        }));
                    }
                    catch { }
                };

            }
            catch (Exception ex)
            {
                CleanupPipe();
                MessageBox.Show(
                    ex.Message,
                    "HDRReset",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task AcceptUiAsync(NamedPipeServerStream server, int generation)
        {
            try
            {
                await server.WaitForConnectionAsync().ConfigureAwait(false);
                if (shuttingDown || generation != Volatile.Read(ref pipeGeneration))
                    return;

                var reader = new StreamReader(server, new UTF8Encoding(false), false, 4096, leaveOpen: true);
                var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true);
                lock (pipeSync)
                {
                    if (generation != Volatile.Read(ref pipeGeneration))
                    {
                        reader.Dispose();
                        writer.Dispose();
                        return;
                    }
                    pipeReader = reader;
                    pipeWriter = writer;
                }

                await SendToUiAsync(new PipeMessage
                {
                    Type = "Snapshot",
                    Snapshot = Program.GetSnapshot()
                }).ConfigureAwait(false);

                while (!shuttingDown && server.IsConnected)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                        break;

                    PipeMessage? message;
                    try { message = JsonSerializer.Deserialize<PipeMessage>(line); }
                    catch (JsonException) { continue; }
                    if (message != null)
                        HandleUiMessage(message);
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                CleanupPipe(generation);
            }
        }

        private void HandleUiMessage(PipeMessage message)
        {
            switch (message.Type)
            {
                case "ManualRefresh":
                    Program.RequestManualRefresh();
                    break;
                case "SetMode":
                    if (Program.SetHdrMode(message.Mode))
                        Program.RequestManualRefresh();
                    break;
                case "SetInterval":
                    Program.TrySetRefreshInterval(message.Minutes, message.Seconds);
                    break;
                case "SetLanguage":
                    dispatcherControl.BeginInvoke(new Action(() =>
                    {
                        language = message.Language == "en-US" ? "en-US" : "zh-CN";
                        UpdateTrayLanguage();
                    }));
                    break;
                case "Shutdown":
                    dispatcherControl.BeginInvoke(new Action(() =>
                    {
                        shuttingDown = true;
                        ExitThread();
                    }));
                    break;
            }
        }

        private async Task SendToUiAsync(PipeMessage message)
        {
            await writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                StreamWriter? writer;
                NamedPipeServerStream? activePipe;
                lock (pipeSync)
                {
                    writer = pipeWriter;
                    activePipe = pipe;
                }
                if (writer == null || activePipe == null || !activePipe.IsConnected)
                    return;

                await writer.WriteLineAsync(JsonSerializer.Serialize(message)).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                writeLock.Release();
            }
        }

        private void Program_StateChanged(object? sender, EventArgs e)
        {
            _ = SendToUiAsync(new PipeMessage
            {
                Type = "Snapshot",
                Snapshot = Program.GetSnapshot()
            });
        }

        private void CleanupPipe(int? expectedGeneration = null)
        {
            StreamReader? reader;
            StreamWriter? writer;
            NamedPipeServerStream? activePipe;
            lock (pipeSync)
            {
                if (expectedGeneration.HasValue &&
                    expectedGeneration.Value != Volatile.Read(ref pipeGeneration))
                    return;

                reader = pipeReader;
                writer = pipeWriter;
                activePipe = pipe;
                pipeReader = null;
                pipeWriter = null;
                pipe = null;
            }

            try { reader?.Dispose(); } catch { }
            try { writer?.Dispose(); } catch { }
            try { activePipe?.Dispose(); } catch { }
        }

        protected override void ExitThreadCore()
        {
            shuttingDown = true;
            Program.StateChanged -= Program_StateChanged;
            Program.Stop();
            NativeMenuTheme.Shutdown();

            try
            {
                _ = SendToUiAsync(new PipeMessage { Type = "HostClosing" });
            }
            catch { }

            CleanupPipe();
            try
            {
                trayIcon.Visible = false;
                trayIcon.MouseClick -= TrayIcon_MouseClick;
                trayIcon.Dispose();
            }
            catch { }

            // 退出请求来自 UI 时，关闭命名管道会通知子进程自行结束 WPF 消息循环。
            // 不强制 Kill，避免窗口退出过程中被突然终止。
            uiProcess?.Dispose();
            uiProcess = null;
            dispatcherControl.Dispose();
            base.ExitThreadCore();
        }
    }
}
