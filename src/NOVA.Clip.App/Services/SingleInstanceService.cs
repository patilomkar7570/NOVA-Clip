using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NOVA.Clip.App.Services;

public class SingleInstanceService : IDisposable
{
    private const string MutexName = "NOVA_CLIP_SINGLE_INSTANCE_MUTEX_V1";
    private const string PipeName = "NOVA_CLIP_PIPE_V1";
    private readonly ILogger<SingleInstanceService> _logger;
    private Mutex? _mutex;
    private bool _hasHandle;
    private CancellationTokenSource? _cts;

    public event EventHandler? AnotherInstanceStarted;

    public SingleInstanceService(ILogger<SingleInstanceService> logger)
    {
        _logger = logger;
    }

    public bool CheckIsFirstInstance()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out _hasHandle);
            if (!_hasHandle)
            {
                // Signal running instance
                SignalRunningInstance();
                return false;
            }

            StartPipeServer();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking single instance mutex");
            return true;
        }
    }

    private void StartPipeServer()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? msg = await reader.ReadLineAsync(_cts.Token);
                    if (msg == "ACTIVATE")
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            AnotherInstanceStarted?.Invoke(this, EventArgs.Empty);
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        });
    }

    private static void SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.WriteLine("ACTIVATE");
            writer.Flush();
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (_hasHandle && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch { }
        }
        _mutex?.Dispose();
        GC.SuppressFinalize(this);
    }
}
