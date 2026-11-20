using System;
using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace HDRReset
{
    internal static class UiProcess
    {
        public static void Run(string pipeName)
        {
            UiPipeClient? client = null;
            try
            {
                client = new UiPipeClient(pipeName);
                var app = new WpfApplication
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose
                };

                LanguageManager.Initialize();
                var window = new MainWindow(client);
                app.MainWindow = window;

                client.MessageReceived += window.OnHostMessage;
                client.ConnectionClosed += () =>
                {
                    try
                    {
                        app.Dispatcher.BeginInvoke(
                            DispatcherPriority.Send,
                            new Action(() => app.Shutdown()));
                    }
                    catch { }
                };

                client.StartReading();
                app.Run(window);
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}
