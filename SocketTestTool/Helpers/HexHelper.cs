namespace SocketTestTool.Helpers;

/// <summary>ASCII / HEX 数据转换工具</summary>
public static class HexHelper
{
    /// <summary>
    /// 解析用户输入的 HEX 字符串（允许空格、换行分隔，大小写均可）。
    /// 例如 "48 65 6C 6C 6F" 或 "48656C6C6F"。
    /// </summary>
    public static byte[] Parse(string input)
    {
        var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (cleaned.Length == 0)
            return Array.Empty<byte>();
        if (cleaned.Length % 2 != 0)
            throw new FormatException("HEX 字符数量必须为偶数（可用空格分隔，如：48 65 6C 6C 6F）");
        return Convert.FromHexString(cleaned);
    }

    /// <summary>格式化为带空格的 HEX 字符串，如 "48 65 6C 6C 6F"</summary>
    public static string ToHex(byte[] data)
        => data.Length == 0 ? string.Empty : string.Join(' ', data.Select(b => b.ToString("X2")));
}
