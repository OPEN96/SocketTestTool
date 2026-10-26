using System.Collections.ObjectModel;
using System.Text;
using SocketTestTool.Helpers;

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

/// <summary>
/// 一条收发日志（保存原始字节，显示时再按 ASCII/HEX 格式化）。
/// 界面以列表项渲染：时间 / 方向 / 对端 / 正文（正文可按关键字高亮切分）。
/// </summary>
public sealed class LogEntry
{
    private string _keyword = string.Empty;

    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogDirection Direction { get; init; }

    /// <summary>对端标识，如 127.0.0.1:5000</summary>
    public string Peer { get; init; } = string.Empty;

    /// <summary>原始数据；为 null 时表示这是一条纯文字消息</summary>
    public byte[]? Data { get; init; }

    /// <summary>纯文字消息内容（系统/错误日志）</summary>
    public string? Message { get; init; }

    /// <summary>HH:mm:ss.fff 形式的时间</summary>
    public string TimeText => Timestamp.ToString("HH:mm:ss.fff");

    /// <summary>方向文字（接收 / 发送 / 转发 / 错误 / 系统）</summary>
    public string DirectionText { get; private set; } = string.Empty;

    /// <summary>按当前显示格式渲染后的正文（过滤与高亮都基于它）</summary>
    public string ContentText { get; private set; } = string.Empty;

    /// <summary>正文按高亮关键字切分后的片段</summary>
    public ObservableCollection<LogSegment> Segments { get; } = new();

    /// <summary>是否命中高亮关键字</summary>
    public bool HasHighlight { get; private set; }

    /// <summary>按指定显示格式刷新方向与正文（切换 ASCII/HEX 时调用）</summary>
    public void Refresh(bool displayHex)
    {
        DirectionText = Direction switch
        {
            LogDirection.Received => "接收",
            LogDirection.Sent => "发送",
            LogDirection.Relay => "转发",
            LogDirection.Error => "错误",
            _ => "系统"
        };

        string text;
        if (Data is { } d)
        {
            text = displayHex ? HexHelper.ToHex(d) : Encoding.UTF8.GetString(d);
            // 列表项按单行展示，换行替换为可见转义
            text = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
        else
        {
            text = Message ?? string.Empty;
        }

        ContentText = text;
        ApplyHighlight(_keyword);
    }

    /// <summary>按关键字重建高亮片段；关键字为空时整行作为一个片段</summary>
    public void ApplyHighlight(string? keyword)
    {
        _keyword = keyword ?? string.Empty;
        Segments.Clear();
        HasHighlight = false;

        if (ContentText.Length == 0)
        {
            Segments.Add(new LogSegment(string.Empty, false));
            return;
        }

        if (_keyword.Length == 0)
        {
            Segments.Add(new LogSegment(ContentText, false));
            return;
        }

        int index = 0;
        int hits = 0;
        while (hits < 200)
        {
            int pos = ContentText.IndexOf(_keyword, index, StringComparison.OrdinalIgnoreCase);
            if (pos < 0) break;

            if (pos > index) Segments.Add(new LogSegment(ContentText[index..pos], false));
            Segments.Add(new LogSegment(ContentText.Substring(pos, _keyword.Length), true));
            index = pos + _keyword.Length;
            hits++;
        }

        if (index < ContentText.Length) Segments.Add(new LogSegment(ContentText[index..], false));
        HasHighlight = hits > 0;
    }

    /// <summary>导出为一行文本（与原日志格式一致）</summary>
    public string ToLine(bool displayHex)
    {
        if (Data is { } d)
        {
            string payload = displayHex ? HexHelper.ToHex(d) : Encoding.UTF8.GetString(d);
            return $"[{TimeText}] [{DirectionText}] [{Peer}] {payload}";
        }
        return $"[{TimeText}] [{DirectionText}] {Message}";
    }
}
