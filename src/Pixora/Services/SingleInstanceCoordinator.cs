using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Pixora.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly string _pipeName;
    private Task? _serverTask;
    private bool _ownsMutex;

    private SingleInstanceCoordinator(Mutex mutex, bool ownsMutex, string pipeName)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
        _pipeName = pipeName;
    }

    public bool IsPrimary => _ownsMutex;

    public static SingleInstanceCoordinator Create()
    {
        var identity = GetIdentityToken();
        var mutexName = $"Local\\Pixora.SingleInstance.{identity}";
        var pipeName = $"Pixora.SingleInstance.{identity}";
        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        return new SingleInstanceCoordinator(mutex, createdNew, pipeName);
    }

    public void StartServer(Func<string?, Task> pathHandler)
    {
        ArgumentNullException.ThrowIfNull(pathHandler);
        if (!IsPrimary || _serverTask is not null)
        {
            return;
        }

        _serverTask = RunServerAsync(pathHandler, _cancellation.Token);
    }

    public async Task<bool> TryForwardAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (IsPrimary)
        {
            return false;
        }

        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(2_000, cancellationToken);
            await using var writer = new StreamWriter(client, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };
            await writer.WriteLineAsync(path ?? string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
        _cancellation.Dispose();
    }

    private async Task RunServerAsync(Func<string?, Task> pathHandler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var path = await reader.ReadLineAsync(cancellationToken);
                await pathHandler(string.IsNullOrWhiteSpace(path) ? null : path);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLog.WriteException("SingleInstanceServer", "接收新窗口打开请求失败。", ex);
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    private static string GetIdentityToken()
    {
        string identity;
        try
        {
            identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        }
        catch
        {
            identity = Environment.UserName;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }
}
