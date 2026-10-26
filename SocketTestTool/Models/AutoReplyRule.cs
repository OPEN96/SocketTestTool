using System.ComponentModel;
using System.Runtime.CompilerServices;

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
/// 实现 INotifyPropertyChanged，保证编辑保存后列表立即刷新、启用开关即时生效。
/// </summary>
public sealed class AutoReplyRule : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _matchText = string.Empty;
    private bool _matchIsHex;
    private int _matchMode;
    private string _replyText = string.Empty;
    private bool _replyIsHex;
    private bool _enabled = true;

    /// <summary>规则名称（仅用于识别）</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>匹配内容</summary>
    public string MatchText
    {
        get => _matchText;
        set => Set(ref _matchText, value);
    }

    /// <summary>匹配内容是否按 HEX 解析（比较时忽略空格与大小写）</summary>
    public bool MatchIsHex
    {
        get => _matchIsHex;
        set => Set(ref _matchIsHex, value);
    }

    /// <summary>匹配方式，取值见 <see cref="AutoReplyMatchMode"/></summary>
    public int MatchMode
    {
        get => _matchMode;
        set
        {
            if (Set(ref _matchMode, value))
                OnPropertyChanged(nameof(MatchModeText));
        }
    }

    /// <summary>命中后回复的内容</summary>
    public string ReplyText
    {
        get => _replyText;
        set => Set(ref _replyText, value);
    }

    /// <summary>回复内容是否按 HEX 解析</summary>
    public bool ReplyIsHex
    {
        get => _replyIsHex;
        set => Set(ref _replyIsHex, value);
    }

    /// <summary>是否启用该规则</summary>
    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    /// <summary>匹配方式的显示文本</summary>
    public string MatchModeText => MatchMode switch
    {
        1 => "相等",
        2 => "前缀",
        3 => "正则",
        _ => "包含"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
