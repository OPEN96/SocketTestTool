using System.Globalization;

namespace SocketTestTool.Helpers;

/// <summary>
/// 数值与进制转换工具：任意进制字符串解析、大小端字节序翻转、
/// IEEE754 浮点与 HEX 互转、BCD 码编解码。全部为纯函数。
/// </summary>
public static class NumberConverter
{
    /// <summary>
    /// 解析数值字符串。支持前缀 0x/0X（十六）、0b/0B（二）、0o/0O（八）与纯十进制，
    /// 允许下划线、空格分隔，支持负号（以 ulong 补码形式返回）。
    /// </summary>
    public static bool TryParse(string? input, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = new(input.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != ',').ToArray());
        if (s.Length == 0) return false;

        bool negative = false;
        if (s[0] is '+' or '-')
        {
            negative = s[0] == '-';
            s = s[1..];
        }
        if (s.Length == 0) return false;

        int basis;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { basis = 16; s = s[2..]; }
        else if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) { basis = 2; s = s[2..]; }
        else if (s.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) { basis = 8; s = s[2..]; }
        else basis = 10;

        if (s.Length == 0) return false;

        ulong parsed;
        try
        {
            parsed = basis switch
            {
                2 => Convert.ToUInt64(s, 2),
                8 => Convert.ToUInt64(s, 8),
                16 => Convert.ToUInt64(s, 16),
                _ => ulong.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture)
            };
        }
        catch
        {
            return false;
        }

        // 负数按 64 位补码表示，便于与 HEX 直接对应
        value = negative ? unchecked((ulong)(-(long)parsed)) : parsed;
        return true;
    }

    /// <summary>按进制格式化：basis 取 2 / 8 / 10 / 16</summary>
    public static string ToBase(ulong value, int basis) => basis switch
    {
        2 => Convert.ToString((long)value, 2),
        8 => Convert.ToString((long)value, 8),
        16 => value.ToString("X"),
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>二进制输出按 4 位分组，便于阅读与组包</summary>
    public static string ToBinaryGrouped(ulong value, int widthBits)
    {
        string raw = Convert.ToString((long)value, 2);
        if (widthBits is 16 or 32 or 64 && raw.Length < widthBits)
            raw = raw.PadLeft(widthBits, '0');

        int groupSize = widthBits is 16 or 32 or 64 ? 8 : 4;
        var parts = new List<string>();
        for (int i = 0; i < raw.Length; i += groupSize)
            parts.Add(raw.Substring(i, Math.Min(groupSize, raw.Length - i)));
        return string.Join(' ', parts);
    }

    /// <summary>
    /// 字节序翻转。hex 为十六进制字符串（允许空格），widthBits 取 16 / 32 / 64。
    /// 例如 "12 34" -> "34 12"，"12 34 56 78" -> "78 56 34 12"。
    /// </summary>
    public static bool TrySwapEndian(string? hex, int widthBits, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string cleaned = new(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
        int expectChars = widthBits / 4;
        if (cleaned.Length == 0 || cleaned.Length % 2 != 0 || cleaned.Length > expectChars)
        {
            result = $"请输入不超过 {expectChars} 位十六进制字符（{widthBits} 位）";
            return false;
        }

        cleaned = cleaned.PadLeft(expectChars, '0');
        byte[] bytes = Convert.FromHexString(cleaned);
        Array.Reverse(bytes);
        result = string.Join(' ', bytes.Select(b => b.ToString("X2")));
        return true;
    }

    /// <summary>十进制小数字符串 -> IEEE754 十六进制（默认小端，即设备常见的低字节在前）</summary>
    public static bool TryFloatToHex(string? text, bool isDouble, bool littleEndian, out string hex)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return false;

        byte[] bytes = isDouble
            ? BitConverter.GetBytes(d)
            : BitConverter.GetBytes((float)d);
        if (BitConverter.IsLittleEndian != littleEndian) Array.Reverse(bytes);
        hex = string.Join(' ', bytes.Select(b => b.ToString("X2")));
        return true;
    }

    /// <summary>IEEE754 十六进制 -> 十进制小数字符串</summary>
    public static bool TryHexToFloat(string? hex, bool isDouble, bool littleEndian, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        byte[] bytes;
        try
        {
            bytes = HexHelper.Parse(hex);
        }
        catch
        {
            return false;
        }

        int expect = isDouble ? 8 : 4;
        if (bytes.Length != expect) return false;

        if (BitConverter.IsLittleEndian != littleEndian) Array.Reverse(bytes);

        if (isDouble)
        {
            double v = BitConverter.ToDouble(bytes, 0);
            text = v.ToString("R", CultureInfo.InvariantCulture);
        }
        else
        {
            float v = BitConverter.ToSingle(bytes, 0);
            text = v.ToString("R", CultureInfo.InvariantCulture);
        }
        return true;
    }

    /// <summary>BCD 编码：数字串（如 "1234"）-> 压缩 BCD 十六进制（"12 34"）</summary>
    public static bool TryBcdEncode(string? digits, out string hex)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(digits)) return false;

        string d = new(digits.Where(char.IsDigit).ToArray());
        if (d.Length == 0) return false;

        if (d.Length % 2 != 0) d = "0" + d; // 奇数位左侧补 0

        var bytes = new byte[d.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)((d[i * 2] - '0') * 16 + (d[i * 2 + 1] - '0'));

        hex = string.Join(' ', bytes.Select(b => b.ToString("X2")));
        return true;
    }

    /// <summary>BCD 解码：压缩 BCD 十六进制 -> 数字串</summary>
    public static bool TryBcdDecode(string? hex, out string digits)
    {
        digits = string.Empty;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        byte[] bytes;
        try
        {
            bytes = HexHelper.Parse(hex);
        }
        catch
        {
            return false;
        }
        if (bytes.Length == 0) return false;

        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            int hi = (b >> 4) & 0xF;
            int lo = b & 0xF;
            if (hi > 9 || lo > 9)
            {
                digits = "包含非 BCD 编码的字节（半字节 > 9）";
                return false;
            }
            sb.Append(hi).Append(lo);
        }

        digits = sb.ToString();
        return true;
    }
}
