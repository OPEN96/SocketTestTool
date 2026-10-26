using System.Net;
using System.Net.Sockets;

namespace SocketTestTool.Services;

/// <summary>
/// 双向透传（端口转发）：
/// 监听本地 <c>listenPort</c>，接受一个下游连接后，主动连接 <c>targetHost:targetPort</c>（上游），
/// 随后将下游→上游、上游→下游的数据原样互发（全双工，不做任何解析）。
/// 每一条转发的数据都通过 <see cref="DataRelayed"/> 事件上报，供调用方记录日志。
/// 所有事件均在后台线程触发，调用方需自行切换到 UI 线程。
/// </summary>
public sealed class RelayService
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private TcpClient? _downstream; // 本地监听侧接入的连接
    private TcpClient? _upstream;   // 连接目标服务的连接

    /// <summary>监听侧有新连接接入，参数为下游 EndPoint 字符串</summary>
    public event Action<string>? DownstreamConnected;
    /// <summary>任一侧断开，参数为断开方向描述</summary>
    public event Action<string>? RelayClosed;
    /// <summary>转发了一条数据：(方向描述, 原始字节)</summary>
    public event Action<string, byte[]>? DataRelayed;
    /// <summary>异常信息</summary>
    public event Action<string>? Error;

    /// <summary>启动透传：监听本地端口，收到连接后连到目标地址</summary>
    public void Start(int listenPort, string targetHost, int targetPort)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, listenPort);
        _listener.Start();
        _ = AcceptLoopAsync(listenPort, targetHost, targetPort, _cts.Token);
    }

    private async Task AcceptLoopAsync(int listenPort, string targetHost, int targetPort, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient downstream = await _listener!.AcceptTcpClientAsync(ct);
                downstream.NoDelay = true;
                string downstreamEp = downstream.Client.RemoteEndPoint?.ToString() ?? "unknown";
                DownstreamConnected?.Invoke(downstreamEp);

                // 每个下游连接建立一条独立转发会话（互不影响）
                _ = RelaySessionAsync(downstream, downstreamEp, targetHost, targetPort, ct);
            }
        }
        catch (OperationCanceledException) { /* 正常停止 */ }
        catch (SocketException) { /* listener 被 Stop */ }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
    }

    private async Task RelaySessionAsync(TcpClient downstream, string downstreamEp, string targetHost, int targetPort, CancellationToken ct)
    {
        TcpClient? upstream = null;
        try
        {
            upstream = new TcpClient { NoDelay = true };
            await upstream.ConnectAsync(targetHost, targetPort, ct);
            string upstreamEp = upstream.Client.RemoteEndPoint?.ToString() ?? targetHost;

            _downstream = downstream;
            _upstream = upstream;

            // 双向各起一个读取任务，收到即转发；任一方向结束则整体结束
            Task a = PumpAsync(downstream, upstream, $"{downstreamEp} → {upstreamEp}", ct);
            Task b = PumpAsync(upstream, downstream, $"{upstreamEp} → {downstreamEp}", ct);
            await Task.WhenAny(a, b);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Error?.Invoke($"透传会话异常：{ex.Message}");
        }
        finally
        {
            try { downstream.Dispose(); } catch { }
            try { upstream?.Dispose(); } catch { }
            RelayClosed?.Invoke(downstreamEp);
        }
    }

    private async Task PumpAsync(TcpClient from, TcpClient to, string dir, CancellationToken ct)
    {
        var buffer = new byte[8192];
        NetworkStream inStream = from.GetStream();
        NetworkStream outStream = to.GetStream();
        while (!ct.IsCancellationRequested)
        {
            int n = await inStream.ReadAsync(buffer, ct);
            if (n == 0) break; // 对端关闭
            byte[] chunk = buffer[..n];
            await outStream.WriteAsync(chunk, ct);
            DataRelayed?.Invoke(dir, chunk);
        }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _downstream?.Dispose(); } catch { }
        try { _upstream?.Dispose(); } catch { }
        try { _listener?.Stop(); } catch { }
        _downstream = null;
        _upstream = null;
        _listener = null;
    }
}
