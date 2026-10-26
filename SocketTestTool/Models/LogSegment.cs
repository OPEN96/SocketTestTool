namespace SocketTestTool.Models;

/// <summary>
/// 日志正文的一个片段：按高亮关键字切分后的结果。
/// <see cref="IsHit"/> 为 true 表示该片段命中关键字，界面上以黄底显示。
/// </summary>
public sealed class LogSegment
{
    public string Text { get; }

    public bool IsHit { get; }

    public LogSegment(string text, bool isHit)
    {
        Text = text;
        IsHit = isHit;
    }
}
