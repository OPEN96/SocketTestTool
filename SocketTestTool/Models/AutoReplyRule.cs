namespace SocketTestTool.Models;

/// <summary>自动应答匹配方式</summary>
public enum AutoReplyMatchMode
{
    /// <summary>包含匹配串</summary>
    Contains = 0,
    /// <summary>完全相等</summary>
    Equals = 1,
    /// <summary>以匹配串开头</summary>
    StartsWith = 2,
    /// <summary>正则表达式</summary>
    Regex = 3
}

/// <summary>
/// 一条自动应答规则：收到匹配内容时自动回复预设数据。
/// 用于模拟从机 / 服务端，免去人工点击发送。
/// </summary>
public sealed class AutoReplyRule
{
    /// <summary>规则名称（仅用于识别）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>匹配内容</summary>
    public string MatchText { get; set; } = string.Empty;

    /// <summary>匹配内容是否按 HEX 解析（比较时忽略空格与大小写）</summary>
    public bool MatchIsHex { get; set; }

    /// <summary>匹配方式，取值见 <see cref="AutoReplyMatchMode"/></summary>
    public int MatchMode { get; set; }

    /// <summary>命中后回复的内容</summary>
    public string ReplyText { get; set; } = string.Empty;

    /// <summary>回复内容是否按 HEX 解析</summary>
    public bool ReplyIsHex { get; set; }

    /// <summary>是否启用该规则</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>匹配方式的显示文本</summary>
    public string MatchModeText => MatchMode switch
    {
        1 => "相等",
        2 => "前缀",
        3 => "正则",
        _ => "包含"
    };
}
