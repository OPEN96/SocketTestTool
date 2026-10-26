namespace SocketTestTool.Models;

/// <summary>日志方向/类型</summary>
public enum LogDirection
{
    /// <summary>收到的数据</summary>
    Received,
    /// <summary>发出的数据</summary>
    Sent,
    /// <summary>双向透传中转的数据</summary>
    Relay,
    /// <summary>系统/状态信息</summary>
    Info,
    /// <summary>错误</summary>
    Error
}

/// <summary>一条收发日志（保存原始字节，显示时再按 ASCII/HEX 格式化）</summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogDirection Direction { get; init; }
    /// <summary>对端标识，如 127.0.0.1:5000</summary>
    public string Peer { get; init; } = string.Empty;
    /// <summary>原始数据；为 null 时表示这是一条纯文字消息</summary>
    public byte[]? Data { get; init; }
    /// <summary>纯文字消息内容（系统/错误日志）</summary>
    public string? Message { get; init; }
}
