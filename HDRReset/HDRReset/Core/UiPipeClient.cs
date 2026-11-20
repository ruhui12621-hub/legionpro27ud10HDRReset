using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HDRReset
{
    internal sealed class UiPipeClient : IDisposable
    {
        private readonly NamedPipeClientStream pipe;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        private volatile bool disposed;

        internal event Action<PipeMessage>? MessageReceived;
        internal event Action? ConnectionClosed;

        internal UiPipeClient(string pipeName)
        {
            pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            pipe.Connect(7000);
            reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true);
        }

        internal void StartReading()
        {
            _ = Task.Run(ReadLoopAsync);
        }

        internal async Task SendAsync(PipeMessage message)
        {
            if (disposed || !pipe.IsConnected)
                return;

            await writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (disposed || !pipe.IsConnected)
                    return;

                string json = JsonSerializer.Serialize(message);
                await writer.WriteLineAsync(json).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            finally
            {
                writeLock.Release();
            }
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (!disposed)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                        break;

                    try
                    {
                        PipeMessage? message = JsonSerializer.Deserialize<PipeMessage>(line);
                        if (message != null)
                            MessageReceived?.Invoke(message);
                    }
                    catch (JsonException) { }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                if (!disposed)
                    ConnectionClosed?.Invoke();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            try { writer.Dispose(); } catch { }
            try { reader.Dispose(); } catch { }
            try { pipe.Dispose(); } catch { }
        }
    }
}
