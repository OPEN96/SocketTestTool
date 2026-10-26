using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocketTestTool.Helpers;
using SocketTestTool.Models;
using SocketTestTool.Services;

namespace SocketTestTool.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private TcpServerService? _tcpServer;
    private TcpClientService? _tcpClient;
    private UdpService? _udp;
    private LogFileWriter? _logWriter;

    private bool _manualDisconnect;
    private bool _reconnecting;
    private CancellationTokenSource? _reconnectCts;

    private readonly List<LogEntry> _entries = new();
    private const int MaxEntries = 2000;

    // ---------- 连接配置 ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClientMode), nameof(ActionText), nameof(ShowReconnect))]
    private bool _isServerMode = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReconnect))]
    private bool _isTcp = true;

    [ObservableProperty]
    private string _host = "0.0.0.0";

    [ObservableProperty]
    private string _port = "9000";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionText))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendToAllCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusText = "未连接";

    [ObservableProperty]
    private bool _autoReconnect = true;

    // ---------- 数据格式 ----------

    /// <summary>发送区使用 HEX 格式</summary>
    [ObservableProperty]
    private bool _sendHex;

    /// <summary>日志区使用 HEX 格式显示</summary>
    [ObservableProperty]
    private bool _displayHex;

    /// <summary>是否将日志实时写入文本文件（logs/ 目录）</summary>
    [ObservableProperty]
    private bool _isFileLoggingEnabled;

    // ---------- 发送区 / 日志区 ----------

    [ObservableProperty]
    private string _sendText = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private ClientItem? _selectedClient;

    /// <summary>已连接客户端（服务端模式）</summary>
    public ObservableCollection<ClientItem> Clients { get; } = new();

    public bool IsClientMode => !IsServerMode;
    public bool ShowReconnect => IsClientMode && IsTcp;
    public string ActionText => IsConnected ? "停止 / 断开" : (IsServerMode ? "启动监听" : "连接");

    private string Peer => $"{Host.Trim()}:{Port}";

    partial void OnIsServerModeChanged(bool value)
        => Host = value ? "0.0.0.0" : "127.0.0.1";

    partial void OnDisplayHexChanged(bool value) => RefreshLog();

    partial void OnIsFileLoggingEnabledChanged(bool value)
    {
        if (value)
        {
            try
            {
                _logWriter = LogFileWriter.Create();
                AddLog(LogDirection.Info, message: $"文本日志记录已开启：{_logWriter.FilePath}");
            }
            catch (Exception ex)
            {
                _logWriter = null;
                AddLog(LogDirection.Error, message: $"无法创建日志文件：{ex.Message}");
                IsFileLoggingEnabled = false; // 重新触发本方法（value=false），此时 _logWriter 为空，不会重复记录
            }
        }
        else if (_logWriter != null)
        {
            _logWriter.Dispose();
            _logWriter = null;
            AddLog(LogDirection.Info, message: "文本日志记录已关闭");
        }
    }

    // ---------- 启动 / 停止 ----------

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            StopAll(log: true);
            return;
        }

        if (!int.TryParse(Port, out int port) || port is < 1 or > 65535)
        {
            AddLog(LogDirection.Error, message: "端口号无效，请输入 1-65535 之间的整数");
            return;
        }
        if (IsClientMode && string.IsNullOrWhiteSpace(Host))
        {
            AddLog(LogDirection.Error, message: "客户端模式下必须填写目标地址");
            return;
        }

        try
        {
            if (IsTcp && IsServerMode) StartTcpServer(port);
            else if (IsTcp) await ConnectTcpClientAsync(port);
            else if (IsServerMode) StartUdpServer(port);
            else StartUdpClient(port);
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"启动失败：{ex.Message}");
            StopAll(log: false);
        }
    }

    private void StartTcpServer(int port)
    {
        var server = new TcpServerService();
        server.ClientConnected += ep => Ui(() =>
        {
            Clients.Add(new ClientItem(ep));
            AddLog(LogDirection.Info, message: $"客户端已接入：{ep}");
        });
        server.ClientDisconnected += ep => Ui(() =>
        {
            var item = Clients.FirstOrDefault(c => c.EndPoint == ep);
            if (item != null) Clients.Remove(item);
            if (SelectedClient?.EndPoint == ep) SelectedClient = null;
            AddLog(LogDirection.Info, message: $"客户端已断开：{ep}");
        });
        server.DataReceived += (ep, data) => AddLog(LogDirection.Received, ep, data);
        server.Error += msg => AddLog(LogDirection.Error, message: msg);

        server.Start(port);
        _tcpServer = server;
        IsConnected = true;
        StatusText = $"TCP 服务端 · 监听 0.0.0.0:{port}";
        AddLog(LogDirection.Info, message: $"TCP 服务端已启动，监听端口 {port}");
    }

    private async Task ConnectTcpClientAsync(int port)
    {
        _manualDisconnect = false;
        var client = new TcpClientService();
        client.DataReceived += data => AddLog(LogDirection.Received, Peer, data);
        client.Disconnected += OnTcpClientDisconnected;
        await client.ConnectAsync(Host.Trim(), port);
        _tcpClient = client;
        IsConnected = true;
        StatusText = $"TCP 客户端 · 已连接 {Peer}";
        AddLog(LogDirection.Info, message: $"已连接到 {Peer}");
    }

    private void OnTcpClientDisconnected()
    {
        Ui(async () =>
        {
            _tcpClient = null;
            IsConnected = false;
            StatusText = "未连接";
            if (_manualDisconnect) return;

            AddLog(LogDirection.Error, message: "与服务器的连接已断开");
            if (AutoReconnect && IsTcp && IsClientMode && !_reconnecting)
                await ReconnectLoopAsync();
        });
    }

    private async Task ReconnectLoopAsync()
    {
        _reconnecting = true;
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;
        try
        {
            while (!ct.IsCancellationRequested && !IsConnected)
            {
                AddLog(LogDirection.Info, message: $"3 秒后尝试重连 {Peer} …");
                await Task.Delay(3000, ct);
                try
                {
                    int port = int.Parse(Port);
                    var client = new TcpClientService();
                    client.DataReceived += data => AddLog(LogDirection.Received, Peer, data);
                    client.Disconnected += OnTcpClientDisconnected;
                    await client.ConnectAsync(Host.Trim(), port, ct);
                    _tcpClient = client;
                    IsConnected = true;
                    StatusText = $"TCP 客户端 · 已连接 {Peer}";
                    AddLog(LogDirection.Info, message: $"重连成功：{Peer}");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    AddLog(LogDirection.Error, message: $"重连失败：{ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }
        finally { _reconnecting = false; }
    }

    private void StartUdpServer(int port)
    {
        var udp = new UdpService();
        udp.DataReceived += (ep, data) => AddLog(LogDirection.Received, ep, data);
        udp.RemoteDiscovered += ep => Ui(() =>
        {
            Clients.Add(new ClientItem(ep));
            AddLog(LogDirection.Info, message: $"发现对端：{ep}（UDP 无连接，收到首包后记录）");
        });

        udp.StartServer(port);
        _udp = udp;
        IsConnected = true;
        StatusText = $"UDP 服务端 · 绑定 0.0.0.0:{port}";
        AddLog(LogDirection.Info, message: $"UDP 已绑定本地端口 {port}，等待数据…");
    }

    private void StartUdpClient(int port)
    {
        var udp = new UdpService();
        udp.DataReceived += (ep, data) => AddLog(LogDirection.Received, ep, data);

        udp.StartClient(Host.Trim(), port);
        _udp = udp;
        IsConnected = true;
        StatusText = $"UDP 客户端 · 目标 {Peer}（本地端口 {udp.LocalPort}）";
        AddLog(LogDirection.Info, message: $"UDP 就绪，目标 {Peer}，本地端口 {udp.LocalPort}");
    }

    private void StopAll(bool log)
    {
        _manualDisconnect = true;
        _reconnectCts?.Cancel();
        try { _tcpServer?.Stop(); } catch { }
        try { _tcpClient?.Disconnect(); } catch { }
        try { _udp?.Stop(); } catch { }
        _tcpServer = null;
        _tcpClient = null;
        _udp = null;
        Clients.Clear();
        IsConnected = false;
        StatusText = "未连接";
        if (log) AddLog(LogDirection.Info, message: "已停止");
    }

    // ---------- 发送 ----------

    private bool CanSend() => IsConnected;

    /// <summary>发送（服务端模式下：选中客户端则单发，未选中则等同发送至全部）</summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (!TryGetPayload(out byte[] data)) return;
        try
        {
            if (IsTcp)
            {
                if (IsServerMode)
                {
                    if (SelectedClient is { } sel)
                    {
                        await _tcpServer!.SendAsync(sel.EndPoint, data);
                        AddLog(LogDirection.Sent, sel.EndPoint, data);
                    }
                    else await SendToAllAsync();
                }
                else
                {
                    await _tcpClient!.SendAsync(data);
                    AddLog(LogDirection.Sent, Peer, data);
                }
            }
            else
            {
                if (IsServerMode)
                {
                    if (SelectedClient is { } sel)
                    {
                        await _udp!.SendToAsync(data, sel.EndPoint);
                        AddLog(LogDirection.Sent, sel.EndPoint, data);
                    }
                    else await SendToAllAsync();
                }
                else
                {
                    await _udp!.SendAsync(data);
                    AddLog(LogDirection.Sent, Peer, data);
                }
            }
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"发送失败：{ex.Message}");
        }
    }

    /// <summary>向全部客户端/对端发送</summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendToAllAsync()
    {
        if (!IsServerMode || !TryGetPayload(out byte[] data)) return;
        try
        {
            int count = IsTcp ? _tcpServer!.ClientCount : _udp!.KnownRemoteCount;
            if (count == 0)
            {
                AddLog(LogDirection.Info, message: "当前没有已连接的对端，未发送");
                return;
            }
            if (IsTcp) await _tcpServer!.BroadcastAsync(data);
            else await _udp!.BroadcastAsync(data);
            AddLog(LogDirection.Sent, $"全部对端 ({count})", data);
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"广播发送失败：{ex.Message}");
        }
    }

    private bool TryGetPayload(out byte[] data)
    {
        data = Array.Empty<byte>();
        if (string.IsNullOrEmpty(SendText))
        {
            AddLog(LogDirection.Info, message: "发送内容为空");
            return false;
        }
        try
        {
            data = SendHex ? HexHelper.Parse(SendText) : Encoding.UTF8.GetBytes(SendText);
            return true;
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"HEX 解析失败：{ex.Message}");
            return false;
        }
    }

    // ---------- 日志 ----------

    [RelayCommand]
    private void ClearLog()
    {
        _entries.Clear();
        LogText = string.Empty;
    }

    private void AddLog(LogDirection dir, string peer = "", byte[]? data = null, string? message = null)
        => Ui(() =>
        {
            var entry = new LogEntry { Direction = dir, Peer = peer, Data = data, Message = message };
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
            RefreshLog();
            // 实时写入文本日志文件（按当前显示格式）
            try { _logWriter?.WriteLine(Format(entry)); } catch { /* 写文件失败不影响界面 */ }
        });

    /// <summary>将当前日志区内容导出为文本文件（logs/ 目录）</summary>
    [RelayCommand]
    private void ExportLog()
    {
        if (string.IsNullOrEmpty(LogText))
        {
            AddLog(LogDirection.Info, message: "当前没有日志可导出");
            return;
        }
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"log-export-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, LogText, Encoding.UTF8);
            AddLog(LogDirection.Info, message: $"日志已导出：{path}");
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"日志导出失败：{ex.Message}");
        }
    }

    private void RefreshLog()
    {
        var sb = new StringBuilder(_entries.Count * 64);
        foreach (var e in _entries)
            sb.AppendLine(Format(e));
        LogText = sb.ToString();
    }

    private string Format(LogEntry e)
    {
        string dir = e.Direction switch
        {
            LogDirection.Received => "接收",
            LogDirection.Sent => "发送",
            LogDirection.Error => "错误",
            _ => "系统"
        };
        string ts = e.Timestamp.ToString("HH:mm:ss.fff");
        if (e.Data is { } d)
        {
            string payload = DisplayHex ? HexHelper.ToHex(d) : Encoding.UTF8.GetString(d);
            return $"[{ts}] [{dir}] [{e.Peer}] {payload}";
        }
        return $"[{ts}] [{dir}] {e.Message}";
    }

    private static void Ui(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public void Dispose()
    {
        StopAll(log: false);
        _logWriter?.Dispose();
        _logWriter = null;
    }
}
