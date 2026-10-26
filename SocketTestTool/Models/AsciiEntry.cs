namespace SocketTestTool.Models;

/// <summary>ASCII 码表中的一行记录</summary>
public class AsciiEntry
{
    /// <summary>十进制</summary>
    public int Dec { get; init; }

    /// <summary>十六进制（两位大写，如 0A）</summary>
    public string Hex { get; init; } = string.Empty;

    /// <summary>八进制（三位，如 012）</summary>
    public string Oct { get; init; } = string.Empty;

    /// <summary>二进制（8 位，如 00001010）</summary>
    public string Bin { get; init; } = string.Empty;

    /// <summary>字符本体（控制字符为空字符串）</summary>
    public string Char { get; init; } = string.Empty;

    /// <summary>标准缩写（NUL / LF / CR / SP / DEL 等，可打印字符为其自身）</summary>
    public string Abbr { get; init; } = string.Empty;

    /// <summary>中文说明</summary>
    public string Desc { get; init; } = string.Empty;

    /// <summary>是否为控制字符（不可见）</summary>
    public bool IsControl { get; init; }

    /// <summary>常见的转义写法（如 \n \r \t \0），无可显示为 '-'</summary>
    public string Esc { get; init; } = "-";

    /// <summary>列表中字符列的显示文本（控制字符显示缩写，便于辨识）</summary>
    public string CharDisplay => IsControl ? string.Empty : Char;

    /// <summary>控制字符行用等宽字体展示缩写</summary>
    public bool ShowAbbrAsChar => IsControl;
}
