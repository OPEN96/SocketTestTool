using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SocketTestTool.Services;

/// <summary>
/// TCP 服务端：监听指定端口，维护已连接客户端列表，
/// 支持向单个客户端或全部客户端发送数据。
/// 所有事件均在后台线程触发，调用方需自行切换到 UI 线程。
/// </summary>
public sealed class TcpServerService
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();

    /// <summary>新客户端接入，参数为客户端 EndPoint 字符串</summary>
    public event Action<string>? ClientConnected;
    /// <summary>客户端断开</summary>
    public event Action<string>? ClientDisconnected;
    /// <summary>收到数据：(客户端 EndPoint, 原始字节)</summary>
    public event Action<string, byte[]>? DataReceived;
    /// <summary>监听循环异常</summary>
    public event Action<string>? Error;

    public int ClientCount => _clients.Count;

    public void Start(int port)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client = await _listener!.AcceptTcpClientAsync(ct);
                client.NoDelay = true;
                string ep = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                _clients[ep] = client;
                ClientConnected?.Invoke(ep);
                _ = ReceiveLoopAsync(ep, client, ct);
            }
        }
        catch (OperationCanceledException) { /* 正常停止 */ }
        catch (SocketException) { /* listener 被 Stop */ }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
    }

    private async Task ReceiveLoopAsync(string ep, TcpClient client, CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            NetworkStream stream = client.GetStream();
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(buffer, ct);
                if (n == 0) break; // 对端正常关闭
                DataReceived?.Invoke(ep, buffer[..n]);
            }
        }
        catch { /* 连接中断，走 finally 清理 */ }
        finally
        {
            if (_clients.TryRemove(ep, out var removed))
            {
                removed.Dispose();
                ClientDisconnected?.Invoke(ep);
            }
            client.Dispose();
        }
    }

    /// <summary>向指定客户端发送</summary>
    public async Task SendAsync(string endPoint, byte[] data)
    {
        if (_clients.TryGetValue(endPoint, out var client))
            await client.GetStream().WriteAsync(data);
        else
            throw new InvalidOperationException($"客户端 {endPoint} 不存在或已断开");
    }

    /// <summary>向全部客户端广播</summary>
    public async Task BroadcastAsync(byte[] data)
    {
        foreach (var (_, client) in _clients)
        {
            try { await client.GetStream().WriteAsync(data); }
            catch { /* 单个发送失败不中断其他客户端 */ }
        }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        foreach (var (_, client) in _clients)
        {
            try { client.Dispose(); } catch { }
        }
        _clients.Clear();
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }
}
