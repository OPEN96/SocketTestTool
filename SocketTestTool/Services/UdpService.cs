using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SocketTestTool.Services;

/// <summary>
/// UDP 通信（无连接）：
/// - 服务端用法 StartServer(port)：绑定本地端口接收任意对端数据，
///   自动记录所有来包对端，可向选中的对端单独回复或向全部已知对端广播。
/// - 客户端用法 StartClient(host, port)：绑定本地临时端口，向固定目标收发。
/// 所有事件均在后台线程触发，调用方需自行切换到 UI 线程。
/// </summary>
public sealed class UdpService
{
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private IPEndPoint? _target;
    private bool _isServer;
    private readonly ConcurrentDictionary<string, IPEndPoint> _knownRemotes = new();

    /// <summary>收到数据：(对端 EndPoint, 原始字节)</summary>
    public event Action<string, byte[]>? DataReceived;
    /// <summary>服务端模式下发现新对端</summary>
    public event Action<string>? RemoteDiscovered;

    public int KnownRemoteCount => _knownRemotes.Count;

    /// <summary>本地绑定的端口号</summary>
    public int LocalPort => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0;

    /// <summary>服务端模式：绑定本地端口</summary>
    public void StartServer(int port)
    {
        _isServer = true;
        _udp = new UdpClient(port);
        _cts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_cts.Token);
    }

    /// <summary>客户端模式：本地随机端口，发往固定目标</summary>
    public void StartClient(string host, int port)
    {
        _isServer = false;
        IPAddress address = Dns.GetHostAddresses(host)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? Dns.GetHostAddresses(host).FirstOrDefault()
            ?? throw new InvalidOperationException($"无法解析主机地址：{host}");
        _target = new IPEndPoint(address, port);
        _udp = new UdpClient(0);
        _cts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result = await _udp!.ReceiveAsync(ct);
                string ep = result.RemoteEndPoint.ToString()!;
                if (_isServer && _knownRemotes.TryAdd(ep, result.RemoteEndPoint))
                    RemoteDiscovered?.Invoke(ep);
                DataReceived?.Invoke(ep, result.Buffer);
            }
        }
        catch (OperationCanceledException) { /* 正常停止 */ }
        catch (ObjectDisposedException) { /* socket 已释放 */ }
        catch (SocketException) { /* socket 关闭导致的中断 */ }
    }

    /// <summary>客户端模式：向目标发送</summary>
    public async Task SendAsync(byte[] data)
    {
        if (_udp is null || _target is null)
            throw new InvalidOperationException("尚未启动");
        await _udp.SendAsync(data, _target);
    }

    /// <summary>服务端模式：向指定对端发送</summary>
    public async Task SendToAsync(byte[] data, string remoteEndPoint)
    {
        if (_udp is null)
            throw new InvalidOperationException("尚未启动");
        if (!_knownRemotes.TryGetValue(remoteEndPoint, out var ep))
            throw new InvalidOperationException($"对端 {remoteEndPoint} 不存在（UDP 只能回复曾发来数据的对端）");
        await _udp.SendAsync(data, ep);
    }

    /// <summary>服务端模式：向全部已知对端广播</summary>
    public async Task BroadcastAsync(byte[] data)
    {
        if (_udp is null) return;
        foreach (var ep in _knownRemotes.Values)
        {
            try { await _udp.SendAsync(data, ep); }
            catch { /* 单个失败不中断 */ }
        }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _udp?.Dispose(); } catch { }
        _udp = null;
        _knownRemotes.Clear();
    }
}
