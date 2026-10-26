namespace SocketTestTool.Models;

/// <summary>一条固定指令（可保存、编辑、排序、一键发送）</summary>
public sealed class FixedCommand
{
    /// <summary>指令名称（用于列表展示）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>指令内容（ASCII 模式下为文本；HEX 模式下为十六进制字符串）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>内容是否按 HEX 解析发送</summary>
    public bool IsHex { get; set; }
}
