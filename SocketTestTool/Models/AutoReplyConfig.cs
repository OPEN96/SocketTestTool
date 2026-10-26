namespace SocketTestTool.Models;

/// <summary>自动应答的持久化结构：总开关 + 规则列表</summary>
public sealed class AutoReplyConfig
{
    /// <summary>是否启用自动应答</summary>
    public bool Enabled { get; set; }

    /// <summary>规则列表（按顺序匹配）</summary>
    public List<AutoReplyRule> Rules { get; set; } = new();
}
