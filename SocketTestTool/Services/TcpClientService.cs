using System.Net.Sockets;

namespace SocketTestTool.Services;

/// <summary>
/// TCP 客户端：连接指定地址/端口并收发数据。
/// 断线时触发 Disconnected 事件（包括被动断开与主动断开，调用方用标志位区分）。
/// 所有事件均在后台线程触发，调用方需自行切换到 UI 线程。
/// </summary>
public sealed class TcpClientService
{
    private TcpClient? _client;
    private CancellationTokenSource? _cts;

    /// <summary>收到数据</summary>
    public event Action<byte[]>? DataReceived;
    /// <summary>连接断开（被动断开或接收循环结束时触发）</summary>
    public event Action? Disconnected;

    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(host, port, ct);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = ReceiveLoopAsync(_client, _cts.Token);
    }

    private async Task ReceiveLoopAsync(TcpClient client, CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            NetworkStream stream = client.GetStream();
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(buffer, ct);
                if (n == 0) break;
                DataReceived?.Invoke(buffer[..n]);
            }
        }
        catch { /* 连接中断 */ }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    public async Task SendAsync(byte[] data)
    {
        if (_client is null)
            throw new InvalidOperationException("尚未连接");
        await _client.GetStream().WriteAsync(data);
    }

    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }
        try { _client?.Dispose(); } catch { }
        _client = null;
    }
}
