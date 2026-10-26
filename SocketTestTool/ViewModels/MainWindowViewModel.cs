using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
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
    private RelayService? _relay;
    private LogFileWriter? _logWriter;
    private readonly FixedCommandStore _commandStore = new();

    private bool _manualDisconnect;
    private bool _reconnecting;
    private CancellationTokenSource? _reconnectCts;

    // 定时心跳
    private CancellationTokenSource? _heartbeatCts;

    // 全量日志（界面显示的是按条件过滤后的 LogEntries）
    private readonly List<LogEntry> _entries = new();
    private const int MaxEntries = 2000;

    // 自动应答持久化
    private readonly AutoReplyStore _autoReplyStore = new();

    // 自动应答触发保护：避免规则造成收发回环时刷爆界面
    private int _autoReplyBurst;
    private DateTime _autoReplyWindowStart = DateTime.Now;

    // 流量统计：上一采样点的累计字节数，用于计算速率
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastRateTime = DateTime.Now;
    private DispatcherTimer? _rateTimer;

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
    [NotifyCanExecuteChangedFor(nameof(ToggleHeartbeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendFixedCommandCommand))]
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
    private ClientItem? _selectedClient;

    // ---------- 双向透传 ----------

    /// <summary>透传监听端口（本机）</summary>
    [ObservableProperty]
    private string _relayListenPort = "9001";

    /// <summary>透传目标地址</summary>
    [ObservableProperty]
    private string _relayTargetHost = "127.0.0.1";

    /// <summary>透传目标端口</summary>
    [ObservableProperty]
    private string _relayTargetPort = "9000";

    /// <summary>透传是否运行中</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelayActionText))]
    private bool _isRelaying;

    // ---------- 定时心跳 ----------

    /// <summary>是否启用定时心跳</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleHeartbeatCommand))]
    private bool _heartbeatEnabled;

    /// <summary>心跳间隔（秒）</summary>
    [ObservableProperty]
    private string _heartbeatInterval = "3";

    /// <summary>心跳内容（ASCII 模式下为文本；HEX 模式下为十六进制）</summary>
    [ObservableProperty]
    private string _heartbeatText = string.Empty;

    /// <summary>心跳运行状态</summary>
    [ObservableProperty]
    private string _heartbeatStatus = "心跳未启动";

    public string RelayActionText => IsRelaying ? "停止透传" : "启动透传";

    /// <summary>已连接客户端（服务端模式）</summary>
    public ObservableCollection<ClientItem> Clients { get; } = new();

    // ---------- 协议工具箱（校验码 / 进制 / 时间） ----------

    /// <summary>由 ViewModel 通知主界面「打开 / 前置协议工具箱窗口」</summary>
    public event Action? OpenToolboxRequested;

    /// <summary>入口按钮：请求打开（若已打开则前置）协议工具箱窗口</summary>
    [RelayCommand]
    private void OpenToolbox() => OpenToolboxRequested?.Invoke();

    /// <summary>
    /// 把工具箱的计算结果追加到发送内容末尾。
    /// preferHex 为真且发送区当前为 ASCII 模式时自动切换为 HEX，
    /// 因为校验码 / 浮点字节这类结果本质是字节序列。
    /// </summary>
    public void AppendToSendText(string? text, bool preferHex)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (preferHex && !SendHex)
        {
            SendHex = true;
            AddLog(LogDirection.Info, message: "发送格式已自动切换为 HEX（插入的是十六进制结果）");
        }

        SendText = string.IsNullOrWhiteSpace(SendText) ? text : SendText.TrimEnd() + " " + text;
        AddLog(LogDirection.Info, message: $"已插入内容：{text}");
    }

    // ---------- 自动应答 ----------

    /// <summary>由 ViewModel 通知主界面「打开 / 前置自动应答规则窗口」</summary>
    public event Action? OpenAutoReplyRequested;

    /// <summary>入口按钮：请求打开（若已打开则前置）自动应答规则窗口</summary>
    [RelayCommand]
    private void OpenAutoReply() => OpenAutoReplyRequested?.Invoke();

    /// <summary>自动应答规则列表</summary>
    public ObservableCollection<AutoReplyRule> AutoReplyRules { get; } = new();

    /// <summary>是否启用自动应答</summary>
    [ObservableProperty]
    private bool _autoReplyEnabled;

    /// <summary>自动应答状态摘要（窗口与主界面状态栏共用）</summary>
    public string AutoReplySummaryText
    {
        get
        {
            int enabled = AutoReplyRules.Count(r => r.Enabled);
            return AutoReplyEnabled ? $"运行中 · {enabled}/{AutoReplyRules.Count} 条规则生效" : "已停用";
        }
    }

    /// <summary>当前选中的规则</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveRuleUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveRuleDownCommand))]
    private AutoReplyRule? _selectedRule;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    private string _ruleName = string.Empty;

    [ObservableProperty] private string _ruleMatchText = string.Empty;
    [ObservableProperty] private bool _ruleMatchIsHex;
    [ObservableProperty] private int _ruleMatchMode;
    [ObservableProperty] private string _ruleReplyText = string.Empty;
    [ObservableProperty] private bool _ruleReplyIsHex;
    [ObservableProperty] private bool _ruleEnabled = true;
    [ObservableProperty] private bool _isEditingRule;

    private AutoReplyRule? _editingRule;

    /// <summary>匹配方式下拉项，顺序与 <see cref="Models.AutoReplyMatchMode"/> 一致</summary>
    public string[] MatchModes => ["包含", "相等", "前缀", "正则"];

    /// <summary>规则表单标题（新增 / 编辑）</summary>
    public string RuleFormTitle => IsEditingRule ? "编辑规则" : "新增规则";

    // ---------- 日志过滤 / 高亮 / 暂停滚动 ----------

    /// <summary>界面实际渲染的日志条目（已按过滤条件筛选）</summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>需要把日志列表滚动到底部（新日志到达且未暂停时触发）</summary>
    public event Action? LogScrollToEndRequested;

    [ObservableProperty] private bool _showReceived = true;
    [ObservableProperty] private bool _showSent = true;
    [ObservableProperty] private bool _showRelay = true;
    [ObservableProperty] private bool _showSystem = true;

    /// <summary>过滤关键字：匹配正文或对端地址</summary>
    [ObservableProperty] private string _filterText = string.Empty;

    /// <summary>高亮关键字：仅着色，不隐藏条目</summary>
    [ObservableProperty] private string _highlightText = string.Empty;

    /// <summary>暂停自动滚动（抓高频数据时冻结视图）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseButtonText))]
    private bool _isLogPaused;

    /// <summary>显示条数 / 全部条数</summary>
    [ObservableProperty] private string _logCountText = "0 / 0";

    public string PauseButtonText => IsLogPaused ? "继续滚动" : "暂停滚动";

    // ---------- 流量统计 ----------

    [ObservableProperty] private long _bytesReceived;
    [ObservableProperty] private long _bytesSent;
    [ObservableProperty] private long _bytesRelayed;
    [ObservableProperty] private long _packetsReceived;
    [ObservableProperty] private long _packetsSent;
    [ObservableProperty] private long _packetsRelayed;
    [ObservableProperty] private double _receiveRate;
    [ObservableProperty] private double _sendRate;
    [ObservableProperty] private string _statsText = "接收 0 B · 发送 0 B";

    // ---------- ASCII 码表 ----------

    /// <summary>由 ViewModel 通知主界面「打开 / 前置 ASCII 码表窗口」</summary>
    public event Action? OpenAsciiTableRequested;

    /// <summary>入口按钮：请求打开（若已打开则前置）ASCII 码表窗口</summary>
    [RelayCommand]
    private void OpenAsciiTable() => OpenAsciiTableRequested?.Invoke();

    // ---------- Base64 工具 ----------

    /// <summary>由 ViewModel 通知主界面「打开 / 前置 Base64 工具窗口」</summary>
    public event Action? OpenBase64ToolRequested;

    /// <summary>入口按钮：请求打开（若已打开则前置）Base64 编解码工具窗口</summary>
    [RelayCommand]
    private void OpenBase64Tool() => OpenBase64ToolRequested?.Invoke();

    /// <summary>
    /// 把 ASCII 码表选中的码值按发送区当前格式追加到发送内容。
    /// HEX 模式下追加两位十六进制（如 41），ASCII 模式下追加字符本体。
    /// </summary>
    public void AppendByteToSendText(int code)
    {
        if (code is < 0 or > 255) return;

        if (SendHex)
        {
            string hex = code.ToString("X2");
            SendText = string.IsNullOrWhiteSpace(SendText) ? hex : SendText.TrimEnd() + " " + hex;
        }
        else
        {
            SendText += ((char)code).ToString();
        }

        AddLog(LogDirection.Info, message: $"已插入字符：{code} (0x{code:X2})");
    }

    // ---------- 固定指令 ----------

    /// <summary>已保存的固定指令列表</summary>
    public ObservableCollection<FixedCommand> FixedCommands { get; } = new();

    /// <summary>固定指令区是否展开（默认折叠，避免占用主界面空间）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FixedCommandsToggleIcon))]
    private bool _isFixedCommandsExpanded;

    /// <summary>固定指令数量（折叠时显示在标题旁）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFixedCommands))]
    private int _fixedCommandCount;

    /// <summary>是否存在已保存指令（用于数量徽标的显隐）</summary>
    public bool HasFixedCommands => FixedCommandCount > 0;

    /// <summary>折叠/展开指示图标</summary>
    public string FixedCommandsToggleIcon => IsFixedCommandsExpanded ? "▾" : "▸";

    /// <summary>当前选中的固定指令</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendFixedCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditFixedCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFixedCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveFixedCommandUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveFixedCommandDownCommand))]
    private FixedCommand? _selectedFixedCommand;

    /// <summary>指令名称输入（新增/编辑对话框）</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFixedCommandCommand))]
    private string _commandName = string.Empty;

    /// <summary>指令内容输入</summary>
    [ObservableProperty]
    private string _commandContent = string.Empty;

    /// <summary>指令内容是否按 HEX 解析</summary>
    [ObservableProperty]
    private bool _commandIsHex;

    /// <summary>是否处于编辑模式（否则为新增模式）</summary>
    [ObservableProperty]
    private bool _isEditingCommand;

    /// <summary>编辑中的指令（用于更新而非新增）</summary>
    private FixedCommand? _editingCommand;

    public bool IsClientMode => !IsServerMode;
    public bool ShowReconnect => IsClientMode && IsTcp;
    public string ActionText => IsConnected ? "停止 / 断开" : (IsServerMode ? "启动监听" : "连接");

    private string Peer => $"{Host.Trim()}:{Port}";

    partial void OnIsServerModeChanged(bool value)
        => Host = value ? "0.0.0.0" : "127.0.0.1";

    partial void OnDisplayHexChanged(bool value)
    {
        // 切换 ASCII/HEX 后重新渲染全部条目（过滤与高亮保持不变）
        foreach (var e in _entries) e.Refresh(value);
    }

    partial void OnShowReceivedChanged(bool value) => ApplyLogFilter();
    partial void OnShowSentChanged(bool value) => ApplyLogFilter();
    partial void OnShowRelayChanged(bool value) => ApplyLogFilter();
    partial void OnShowSystemChanged(bool value) => ApplyLogFilter();
    partial void OnFilterTextChanged(string value) => ApplyLogFilter();
    partial void OnHighlightTextChanged(string value) => ApplyHighlight();

    partial void OnAutoReplyEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(AutoReplySummaryText));
        PersistAutoReply();
        AddLog(LogDirection.Info, message: value ? "自动应答已启用" : "自动应答已停用");
    }

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
        // 停止心跳（主连接已断开，心跳无意义）
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        HeartbeatEnabled = false;
        HeartbeatStatus = "心跳未启动";
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

    // ---------- 双向透传 ----------

    [RelayCommand]
    private void ToggleRelay()
    {
        if (IsRelaying)
        {
            StopRelay();
            return;
        }

        if (!int.TryParse(RelayListenPort, out int listenPort) || listenPort is < 1 or > 65535)
        {
            AddLog(LogDirection.Error, message: "透传监听端口无效，请输入 1-65535 之间的整数");
            return;
        }
        if (!int.TryParse(RelayTargetPort, out int targetPort) || targetPort is < 1 or > 65535)
        {
            AddLog(LogDirection.Error, message: "透传目标端口无效，请输入 1-65535 之间的整数");
            return;
        }
        if (string.IsNullOrWhiteSpace(RelayTargetHost))
        {
            AddLog(LogDirection.Error, message: "透传目标地址不能为空");
            return;
        }

        try
        {
            var relay = new RelayService();
            relay.DownstreamConnected += ep => AddLog(LogDirection.Info, message: $"透传：下游接入 {ep}，正在连接 {RelayTargetHost.Trim()}:{targetPort} …");
            relay.DataRelayed += (dir, data) => AddLog(LogDirection.Relay, dir, data);
            relay.RelayClosed += ep => AddLog(LogDirection.Info, message: $"透传：会话结束（{ep}）");
            relay.Error += msg => AddLog(LogDirection.Error, message: $"透传错误：{msg}");

            relay.Start(listenPort, RelayTargetHost.Trim(), targetPort);
            _relay = relay;
            IsRelaying = true;
            AddLog(LogDirection.Info, message: $"透传已启动：本机 :{listenPort} ⇄ {RelayTargetHost.Trim()}:{targetPort}");
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"透传启动失败：{ex.Message}");
            StopRelay();
        }
    }

    private void StopRelay()
    {
        try { _relay?.Stop(); } catch { }
        _relay = null;
        IsRelaying = false;
    }

    // ---------- 定时心跳 ----------

    [RelayCommand(CanExecute = nameof(CanToggleHeartbeat))]
    private void ToggleHeartbeat()
    {
        if (_heartbeatCts != null)
        {
            // 已运行 -> 停止
            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            HeartbeatEnabled = false;
            HeartbeatStatus = "心跳已停止";
            AddLog(LogDirection.Info, message: "定时心跳已停止");
            return;
        }

        if (!int.TryParse(HeartbeatInterval, out int interval) || interval < 1)
        {
            AddLog(LogDirection.Error, message: "心跳间隔无效，请输入大于 0 的整数（秒）");
            return;
        }

        _heartbeatCts = new CancellationTokenSource();
        HeartbeatEnabled = true;
        HeartbeatStatus = $"心跳运行中 · 每 {interval} 秒";
        AddLog(LogDirection.Info, message: $"定时心跳已启动：每 {interval} 秒发送一次");
        _ = HeartbeatLoopAsync(interval, _heartbeatCts.Token);
    }

    private bool CanToggleHeartbeat() => IsConnected || _heartbeatCts != null;

    private async Task HeartbeatLoopAsync(int interval, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(interval * 1000, ct);
                if (!IsConnected) continue;
                await SendHeartbeatAsync();
            }
        }
        catch (OperationCanceledException) { /* 停止 */ }
    }

    private async Task SendHeartbeatAsync()
    {
        if (!TryGetHeartbeatPayload(out byte[] data)) return;
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
                    else if (_tcpServer!.ClientCount > 0)
                    {
                        await _tcpServer.BroadcastAsync(data);
                        AddLog(LogDirection.Sent, $"全部对端 ({_tcpServer.ClientCount})", data);
                    }
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
                    else if (_udp!.KnownRemoteCount > 0)
                    {
                        await _udp.BroadcastAsync(data);
                        AddLog(LogDirection.Sent, $"全部对端 ({_udp.KnownRemoteCount})", data);
                    }
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
            AddLog(LogDirection.Error, message: $"心跳发送失败：{ex.Message}");
        }
    }

    private bool TryGetHeartbeatPayload(out byte[] data)
    {
        data = Array.Empty<byte>();
        if (string.IsNullOrEmpty(HeartbeatText)) return false;
        try
        {
            data = SendHex ? HexHelper.Parse(HeartbeatText) : Encoding.UTF8.GetBytes(HeartbeatText);
            return true;
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"心跳内容 HEX 解析失败：{ex.Message}");
            return false;
        }
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
        LogEntries.Clear();
        UpdateLogCountText();
    }

    private void AddLog(LogDirection dir, string peer = "", byte[]? data = null, string? message = null)
        => Ui(() =>
        {
            var entry = new LogEntry { Direction = dir, Peer = peer, Data = data, Message = message };
            entry.Refresh(DisplayHex);
            entry.ApplyHighlight(HighlightText);

            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                var oldest = _entries[0];
                _entries.RemoveAt(0);
                if (LogEntries.Count > 0 && ReferenceEquals(LogEntries[0], oldest))
                    LogEntries.RemoveAt(0);
            }

            CountTraffic(dir, data);

            if (MatchesFilter(entry))
            {
                LogEntries.Add(entry);
                if (!IsLogPaused) LogScrollToEndRequested?.Invoke();
            }

            UpdateLogCountText();

            // 实时写入文本日志文件（按当前显示格式）
            try { _logWriter?.WriteLine(entry.ToLine(DisplayHex)); } catch { /* 写文件失败不影响界面 */ }

            // 收到数据时按自动应答规则匹配并回复
            if (dir == LogDirection.Received && data is { Length: > 0 } && AutoReplyEnabled)
                _ = TryAutoReplyAsync(peer, data);
        });

    // ---------- 日志过滤 / 高亮 ----------

    private bool MatchesFilter(LogEntry entry)
    {
        if (!IsDirectionVisible(entry.Direction)) return false;

        string kw = FilterText?.Trim() ?? string.Empty;
        if (kw.Length == 0) return true;

        return entry.ContentText.Contains(kw, StringComparison.OrdinalIgnoreCase)
            || entry.Peer.Contains(kw, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDirectionVisible(LogDirection dir) => dir switch
    {
        LogDirection.Received => ShowReceived,
        LogDirection.Sent => ShowSent,
        LogDirection.Relay => ShowRelay,
        _ => ShowSystem
    };

    /// <summary>按当前过滤条件重建显示集合</summary>
    private void ApplyLogFilter()
    {
        LogEntries.Clear();
        foreach (var e in _entries)
            if (MatchesFilter(e)) LogEntries.Add(e);

        UpdateLogCountText();
        LogScrollToEndRequested?.Invoke();
    }

    /// <summary>按当前高亮关键字重新切分全部条目</summary>
    private void ApplyHighlight()
    {
        foreach (var e in _entries) e.ApplyHighlight(HighlightText);
    }

    [RelayCommand]
    private void ResetLogFilter()
    {
        ShowReceived = true;
        ShowSent = true;
        ShowRelay = true;
        ShowSystem = true;
        FilterText = string.Empty;
        HighlightText = string.Empty;
        AddLog(LogDirection.Info, message: "日志过滤条件已重置");
    }

    /// <summary>暂停 / 恢复日志自动滚动</summary>
    [RelayCommand]
    private void ToggleLogPause()
    {
        IsLogPaused = !IsLogPaused;
        AddLog(LogDirection.Info, message: IsLogPaused ? "日志已暂停滚动（仍在持续记录）" : "日志恢复自动滚动");
        if (!IsLogPaused) LogScrollToEndRequested?.Invoke();
    }

    /// <summary>请求复制一条日志（由窗口层写入剪贴板）</summary>
    public event Action<string>? CopyTextRequested;

    /// <summary>把指定日志行复制到剪贴板</summary>
    public void CopyLogEntry(LogEntry? entry)
    {
        if (entry == null) return;
        CopyTextRequested?.Invoke(entry.ToLine(DisplayHex));
    }

    private void UpdateLogCountText()
        => LogCountText = $"显示 {LogEntries.Count} / 共 {_entries.Count} 条";

    // ---------- 流量统计 ----------

    private void CountTraffic(LogDirection dir, byte[]? data)
    {
        int len = data?.Length ?? 0;
        switch (dir)
        {
            case LogDirection.Received:
                BytesReceived += len;
                PacketsReceived++;
                break;
            case LogDirection.Sent:
                BytesSent += len;
                PacketsSent++;
                break;
            case LogDirection.Relay:
                BytesRelayed += len;
                PacketsRelayed++;
                break;
        }
        UpdateStatsText();
    }

    private void UpdateStatsText()
    {
        string relay = BytesRelayed > 0 ? $" · 转发 {FormatSize(BytesRelayed)}/{PacketsRelayed} 包" : string.Empty;
        StatsText = $"接收 {FormatSize(BytesReceived)}/{PacketsReceived} 包 · 发送 {FormatSize(BytesSent)}/{PacketsSent} 包"
                    + relay
                    + $" · ↓ {FormatSize(ReceiveRate)}/s ↑ {FormatSize(SendRate)}/s";
    }

    /// <summary>字节数格式化为 B / KB / MB / GB</summary>
    private static string FormatSize(double bytes)
    {
        if (bytes < 1024) return $"{bytes:0} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:0.0} KB";
        if (bytes < 1024d * 1024 * 1024) return $"{bytes / 1024 / 1024:0.00} MB";
        return $"{bytes / 1024 / 1024 / 1024:0.00} GB";
    }

    /// <summary>清零流量统计</summary>
    [RelayCommand]
    private void ResetStats()
    {
        BytesReceived = BytesSent = BytesRelayed = 0;
        PacketsReceived = PacketsSent = PacketsRelayed = 0;
        ReceiveRate = SendRate = 0;
        _lastBytesReceived = _lastBytesSent = 0;
        _lastRateTime = DateTime.Now;
        UpdateStatsText();
        AddLog(LogDirection.Info, message: "流量统计已清零");
    }

    /// <summary>启动每秒一次的速率采样</summary>
    private void StartRateTimer()
    {
        _rateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _rateTimer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            double seconds = (now - _lastRateTime).TotalSeconds;
            if (seconds <= 0) return;

            ReceiveRate = (BytesReceived - _lastBytesReceived) / seconds;
            SendRate = (BytesSent - _lastBytesSent) / seconds;
            _lastBytesReceived = BytesReceived;
            _lastBytesSent = BytesSent;
            _lastRateTime = now;
            UpdateStatsText();
        };
        _rateTimer.Start();
    }

    /// <summary>
    /// 将当前日志导出为文本文件（logs/ 目录）。
    /// 导出内容为界面上当前可见的条目，即「所见即所得」；未过滤时等同全部日志。
    /// </summary>
    [RelayCommand]
    private void ExportLog()
    {
        if (LogEntries.Count == 0)
        {
            AddLog(LogDirection.Info, message: "当前没有日志可导出");
            return;
        }

        try
        {
            var sb = new StringBuilder(LogEntries.Count * 64);
            foreach (var e in LogEntries) sb.AppendLine(e.ToLine(DisplayHex));

            string dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"log-export-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AddLog(LogDirection.Info, message: $"日志已导出（{LogEntries.Count} 条）：{path}");
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"日志导出失败：{ex.Message}");
        }
    }

    private static void Ui(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    // ---------- 固定指令 ----------

    public MainWindowViewModel()
    {
        // 指令数量随集合变化同步更新（折叠标题处展示）
        FixedCommands.CollectionChanged += (_, _) => FixedCommandCount = FixedCommands.Count;

        // 启动时加载已保存的固定指令
        try
        {
            foreach (var cmd in _commandStore.Load())
                FixedCommands.Add(cmd);
            FixedCommandCount = FixedCommands.Count;
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"加载固定指令失败：{ex.Message}");
        }

        // 自动应答规则（含总开关）持久化加载
        try
        {
            var cfg = _autoReplyStore.Load();
            AutoReplyEnabled = cfg.Enabled;
            foreach (var r in cfg.Rules) AutoReplyRules.Add(r);
            RefreshAutoReplySummary();
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"加载自动应答规则失败：{ex.Message}");
        }

        // 启动流量速率采样（每秒计算 B/s）
        StartRateTimer();
    }

    /// <summary>新增固定指令：清空输入区，进入新增模式</summary>
    [RelayCommand]
    private void NewFixedCommand()
    {
        CommandName = string.Empty;
        CommandContent = string.Empty;
        CommandIsHex = false;
        IsEditingCommand = false;
        _editingCommand = null;
        SelectedFixedCommand = null;
    }

    /// <summary>编辑当前选中的固定指令：回填输入区，进入编辑模式</summary>
    [RelayCommand(CanExecute = nameof(CanEditFixedCommand))]
    private void EditFixedCommand()
    {
        if (SelectedFixedCommand is not { } cmd) return;
        CommandName = cmd.Name;
        CommandContent = cmd.Content;
        CommandIsHex = cmd.IsHex;
        IsEditingCommand = true;
        _editingCommand = cmd;
    }

    private bool CanEditFixedCommand() => SelectedFixedCommand != null;

    /// <summary>保存固定指令（新增或更新当前编辑的指令）</summary>
    [RelayCommand(CanExecute = nameof(CanSaveFixedCommand))]
    private void SaveFixedCommand()
    {
        string name = CommandName.Trim();
        if (name.Length == 0)
        {
            AddLog(LogDirection.Error, message: "指令名称不能为空");
            return;
        }

        if (IsEditingCommand && _editingCommand is { } editing)
        {
            editing.Name = name;
            editing.Content = CommandContent;
            editing.IsHex = CommandIsHex;
            AddLog(LogDirection.Info, message: $"固定指令已更新：{name}");
        }
        else
        {
            FixedCommands.Add(new FixedCommand
            {
                Name = name,
                Content = CommandContent,
                IsHex = CommandIsHex
            });
            AddLog(LogDirection.Info, message: $"固定指令已保存：{name}");
        }

        PersistCommands();
        NewFixedCommand();
    }

    private bool CanSaveFixedCommand()
        => !string.IsNullOrWhiteSpace(CommandName);

    /// <summary>删除当前选中的固定指令</summary>
    [RelayCommand(CanExecute = nameof(CanEditFixedCommand))]
    private void DeleteFixedCommand()
    {
        if (SelectedFixedCommand is not { } cmd) return;
        FixedCommands.Remove(cmd);
        AddLog(LogDirection.Info, message: $"固定指令已删除：{cmd.Name}");
        PersistCommands();
    }

    /// <summary>上移当前选中指令</summary>
    [RelayCommand(CanExecute = nameof(CanMoveFixedCommandUp))]
    private void MoveFixedCommandUp()
    {
        int idx = FixedCommands.IndexOf(SelectedFixedCommand!);
        if (idx <= 0) return;
        FixedCommands.Move(idx, idx - 1);
        PersistCommands();
    }

    private bool CanMoveFixedCommandUp()
        => SelectedFixedCommand != null && FixedCommands.IndexOf(SelectedFixedCommand) > 0;

    /// <summary>下移当前选中指令</summary>
    [RelayCommand(CanExecute = nameof(CanMoveFixedCommandDown))]
    private void MoveFixedCommandDown()
    {
        int idx = FixedCommands.IndexOf(SelectedFixedCommand!);
        if (idx < 0 || idx >= FixedCommands.Count - 1) return;
        FixedCommands.Move(idx, idx + 1);
        PersistCommands();
    }

    private bool CanMoveFixedCommandDown()
        => SelectedFixedCommand != null && FixedCommands.IndexOf(SelectedFixedCommand) < FixedCommands.Count - 1;

    /// <summary>点击指令：一键发送该指令内容</summary>
    [RelayCommand(CanExecute = nameof(CanSendFixedCommand))]
    private async Task SendFixedCommandAsync(FixedCommand? cmd)
    {
        if (cmd == null) return;
        if (!IsConnected)
        {
            AddLog(LogDirection.Error, message: "未连接，无法发送固定指令");
            return;
        }

        byte[] data;
        try
        {
            data = cmd.IsHex ? HexHelper.Parse(cmd.Content) : Encoding.UTF8.GetBytes(cmd.Content);
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"固定指令「{cmd.Name}」HEX 解析失败：{ex.Message}");
            return;
        }

        if (data.Length == 0)
        {
            AddLog(LogDirection.Info, message: $"固定指令「{cmd.Name}」内容为空，未发送");
            return;
        }

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
                    else if (_tcpServer!.ClientCount > 0)
                    {
                        await _tcpServer.BroadcastAsync(data);
                        AddLog(LogDirection.Sent, $"全部对端 ({_tcpServer.ClientCount})", data);
                    }
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
                    else if (_udp!.KnownRemoteCount > 0)
                    {
                        await _udp.BroadcastAsync(data);
                        AddLog(LogDirection.Sent, $"全部对端 ({_udp.KnownRemoteCount})", data);
                    }
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
            AddLog(LogDirection.Error, message: $"固定指令「{cmd.Name}」发送失败：{ex.Message}");
        }
    }

    /// <summary>将当前指令列表持久化到本地文件</summary>
    private void PersistCommands()
    {
        try
        {
            _commandStore.Save(FixedCommands);
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"保存固定指令失败：{ex.Message}");
        }
    }

    private bool CanSendFixedCommand() => IsConnected && SelectedFixedCommand != null;

    // ---------- 自动应答：规则管理 ----------

    /// <summary>新增规则：清空输入区，进入新增模式</summary>
    [RelayCommand]
    private void NewRule()
    {
        RuleName = string.Empty;
        RuleMatchText = string.Empty;
        RuleMatchIsHex = false;
        RuleMatchMode = 0;
        RuleReplyText = string.Empty;
        RuleReplyIsHex = false;
        RuleEnabled = true;
        IsEditingRule = false;
        _editingRule = null;
        SelectedRule = null;
    }

    /// <summary>编辑当前选中的规则：回填输入区，进入编辑模式</summary>
    [RelayCommand(CanExecute = nameof(CanEditRule))]
    private void EditRule()
    {
        if (SelectedRule is not { } rule) return;
        RuleName = rule.Name;
        RuleMatchText = rule.MatchText;
        RuleMatchIsHex = rule.MatchIsHex;
        RuleMatchMode = rule.MatchMode;
        RuleReplyText = rule.ReplyText;
        RuleReplyIsHex = rule.ReplyIsHex;
        RuleEnabled = rule.Enabled;
        IsEditingRule = true;
        _editingRule = rule;
    }

    private bool CanEditRule() => SelectedRule != null;

    /// <summary>把输入区内容写入规则对象</summary>
    private void ApplyRuleInput(AutoReplyRule rule, string name)
    {
        rule.Name = name;
        rule.MatchText = RuleMatchText;
        rule.MatchIsHex = RuleMatchIsHex;
        rule.MatchMode = RuleMatchMode;
        rule.ReplyText = RuleReplyText;
        rule.ReplyIsHex = RuleReplyIsHex;
        rule.Enabled = RuleEnabled;
    }

    /// <summary>保存规则（新增或更新当前编辑的规则）</summary>
    [RelayCommand(CanExecute = nameof(CanSaveRule))]
    private void SaveRule()
    {
        if (string.IsNullOrWhiteSpace(RuleMatchText))
        {
            AddLog(LogDirection.Error, message: "匹配内容不能为空");
            return;
        }
        if (string.IsNullOrWhiteSpace(RuleReplyText))
        {
            AddLog(LogDirection.Error, message: "回复内容不能为空");
            return;
        }

        string name = RuleName.Trim();

        if (IsEditingRule && _editingRule is { } editing)
        {
            ApplyRuleInput(editing, name.Length == 0 ? editing.Name : name);
            AddLog(LogDirection.Info, message: $"自动应答规则已更新：{editing.Name}");
        }
        else
        {
            var rule = new AutoReplyRule();
            ApplyRuleInput(rule, name.Length == 0 ? $"规则 {AutoReplyRules.Count + 1}" : name);
            AutoReplyRules.Add(rule);
            AddLog(LogDirection.Info, message: $"自动应答规则已保存：{rule.Name}");
        }

        PersistAutoReply();
        RefreshAutoReplySummary();
        NewRule();
    }

    private bool CanSaveRule() => !string.IsNullOrWhiteSpace(RuleMatchText);

    /// <summary>删除当前选中的规则</summary>
    [RelayCommand(CanExecute = nameof(CanEditRule))]
    private void DeleteRule()
    {
        if (SelectedRule is not { } rule) return;
        AutoReplyRules.Remove(rule);
        AddLog(LogDirection.Info, message: $"自动应答规则已删除：{rule.Name}");
        PersistAutoReply();
        NewRule();
    }

    /// <summary>上移当前选中规则（越靠前越先匹配）</summary>
    [RelayCommand(CanExecute = nameof(CanMoveRuleUp))]
    private void MoveRuleUp()
    {
        int idx = AutoReplyRules.IndexOf(SelectedRule!);
        if (idx <= 0) return;
        AutoReplyRules.Move(idx, idx - 1);
        PersistAutoReply();
    }

    private bool CanMoveRuleUp()
        => SelectedRule != null && AutoReplyRules.IndexOf(SelectedRule) > 0;

    /// <summary>下移当前选中规则</summary>
    [RelayCommand(CanExecute = nameof(CanMoveRuleDown))]
    private void MoveRuleDown()
    {
        int idx = AutoReplyRules.IndexOf(SelectedRule!);
        if (idx < 0 || idx >= AutoReplyRules.Count - 1) return;
        AutoReplyRules.Move(idx, idx + 1);
        PersistAutoReply();
    }

    private bool CanMoveRuleDown()
        => SelectedRule != null && AutoReplyRules.IndexOf(SelectedRule) < AutoReplyRules.Count - 1;

    /// <summary>持久化自动应答配置到本地文件</summary>
    private void PersistAutoReply()
    {
        try
        {
            _autoReplyStore.Save(new AutoReplyConfig
            {
                Enabled = AutoReplyEnabled,
                Rules = AutoReplyRules.ToList()
            });
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"保存自动应答规则失败：{ex.Message}");
        }
    }

    private void RefreshAutoReplySummary() => OnPropertyChanged(nameof(AutoReplySummaryText));

    // ---------- 自动应答：匹配与回复 ----------

    /// <summary>
    /// 收到数据后按顺序匹配所有启用的规则，命中即自动回复。
    /// 带简单的频率保护，避免规则造成收发回环时刷爆界面。
    /// </summary>
    private async Task TryAutoReplyAsync(string peer, byte[] data)
    {
        var now = DateTime.Now;
        if (now - _autoReplyWindowStart > TimeSpan.FromSeconds(1))
        {
            _autoReplyWindowStart = now;
            _autoReplyBurst = 0;
        }
        if (++_autoReplyBurst > 100)
        {
            if (_autoReplyBurst == 101)
                AddLog(LogDirection.Error, message: "自动应答触发过于频繁（1 秒内超过 100 次），已临时抑制，请检查规则是否造成收发回环");
            return;
        }

        foreach (var rule in AutoReplyRules.ToList())
        {
            if (!rule.Enabled) continue;
            if (!RuleMatches(rule, data)) continue;

            byte[] reply;
            try
            {
                reply = rule.ReplyIsHex ? HexHelper.Parse(rule.ReplyText) : Encoding.UTF8.GetBytes(rule.ReplyText);
            }
            catch (Exception ex)
            {
                AddLog(LogDirection.Error, message: $"自动应答规则「{rule.Name}」回复内容 HEX 解析失败：{ex.Message}");
                continue;
            }
            if (reply.Length == 0) continue;

            await SendAutoReplyAsync(peer, reply, rule);
        }
    }

    /// <summary>判断收到的数据是否命中规则</summary>
    private static bool RuleMatches(AutoReplyRule rule, byte[] data)
    {
        if (string.IsNullOrEmpty(rule.MatchText)) return false;

        string text, pattern;
        if (rule.MatchIsHex)
        {
            // HEX 匹配：统一去掉空格并转大写，忽略用户书写习惯差异
            text = string.Concat(data.Select(b => b.ToString("X2")));
            pattern = new string(rule.MatchText.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }
        else
        {
            text = Encoding.UTF8.GetString(data);
            pattern = rule.MatchText;
        }
        if (pattern.Length == 0) return false;

        switch (rule.MatchMode)
        {
            case 1: return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            case 2: return text.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
            case 3:
                try { return Regex.IsMatch(text, pattern); }
                catch { return false; }   // 正则非法时视为未命中
            default: return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>按当前连接形态把回复发回去（服务端模式原路回复到该对端）</summary>
    private async Task SendAutoReplyAsync(string peer, byte[] data, AutoReplyRule rule)
    {
        try
        {
            if (IsTcp)
            {
                if (IsServerMode)
                {
                    if (_tcpServer == null) return;
                    await _tcpServer.SendAsync(peer, data);
                }
                else
                {
                    if (_tcpClient == null) return;
                    await _tcpClient.SendAsync(data);
                }
            }
            else
            {
                if (IsServerMode)
                {
                    if (_udp == null) return;
                    await _udp.SendToAsync(data, peer);
                }
                else
                {
                    if (_udp == null) return;
                    await _udp.SendAsync(data);
                }
            }

            AddLog(LogDirection.Sent, peer, data);
            AddLog(LogDirection.Info, message: $"自动应答：命中规则「{rule.Name}」");
        }
        catch (Exception ex)
        {
            AddLog(LogDirection.Error, message: $"自动应答「{rule.Name}」发送失败：{ex.Message}");
        }
    }

    public void Dispose()
    {
        StopAll(log: false);
        StopRelay();
        _rateTimer?.Stop();
        _rateTimer = null;
        _logWriter?.Dispose();
        _logWriter = null;
    }
}
