using SocketTestTool.Models;

namespace SocketTestTool.Services;

/// <summary>
/// ASCII 码表数据（0-127 标准 ASCII + 128-255 扩展 / Latin-1）。
/// 供调试串口、Socket 报文时快速查询字符与进制对应关系。
/// </summary>
public static class AsciiTable
{
    private static readonly string[] Abbrs =
    {
        "NUL","SOH","STX","ETX","EOT","ENQ","ACK","BEL","BS","HT",
        "LF","VT","FF","CR","SO","SI","DLE","DC1","DC2","DC3",
        "DC4","NAK","SYN","ETB","CAN","EM","SUB","ESC","FS","GS",
        "RS","US","SP","!","\"","#","$","%","&","'",
        "(",")","*","+",",","-",".","/","0","1",
        "2","3","4","5","6","7","8","9",":",";",
        "<","=",">","?","@","A","B","C","D","E",
        "F","G","H","I","J","K","L","M","N","O",
        "P","Q","R","S","T","U","V","W","X","Y",
        "Z","[","\\","]","^","_","`","a","b","c",
        "d","e","f","g","h","i","j","k","l","m",
        "n","o","p","q","r","s","t","u","v","w",
        "x","y","z","{","|","}","~","DEL"
    };

    private static readonly string[] Descs =
    {
        "空字符 / 字符串结束符","标题开始","正文开始","正文结束","传输结束","询问 / 请求","确认应答","响铃","退格","水平制表符",
        "换行","垂直制表符","换页","回车","移出（切换字符集）","移入（切换字符集）","数据链路转义","设备控制 1（XON）","设备控制 2","设备控制 3（XOFF）",
        "设备控制 4","否定应答 / 无响应","同步空闲","传输块结束","取消","介质结束 / 媒质用完","替补 / 替换字符","转义符","文件分隔符","组分隔符",
        "记录分隔符","单元分隔符","空格","感叹号","双引号","井号 / 号码符","美元符","百分号","与符号 / And","单引号 / 撇号",
        "左圆括号","右圆括号","星号 / 乘号","加号","逗号","连字符 / 减号","句号 / 点","斜杠 / 除号","数字 0","数字 1",
        "数字 2","数字 3","数字 4","数字 5","数字 6","数字 7","数字 8","数字 9","冒号","分号",
        "小于号","等号","大于号","问号","AT 符号 / At","大写字母 A","大写字母 B","大写字母 C","大写字母 D","大写字母 E",
        "大写字母 F","大写字母 G","大写字母 H","大写字母 I","大写字母 J","大写字母 K","大写字母 L","大写字母 M","大写字母 N","大写字母 O",
        "大写字母 P","大写字母 Q","大写字母 R","大写字母 S","大写字母 T","大写字母 U","大写字母 V","大写字母 W","大写字母 X","大写字母 Y",
        "大写字母 Z","左方括号","反斜杠","右方括号","脱字符 / 异或","下划线","反引号 / 重音符","小写字母 a","小写字母 b","小写字母 c",
        "小写字母 d","小写字母 e","小写字母 f","小写字母 g","小写字母 h","小写字母 i","小写字母 j","小写字母 k","小写字母 l","小写字母 m",
        "小写字母 n","小写字母 o","小写字母 p","小写字母 q","小写字母 r","小写字母 s","小写字母 t","小写字母 u","小写字母 v","小写字母 w",
        "小写字母 x","小写字母 y","小写字母 z","左花括号","竖线 / 或","右花括号","波浪号","删除"
    };

    /// <summary>常用控制字符的转义写法</summary>
    private static readonly Dictionary<int, string> Escapes = new()
    {
        [0] = @"\0", [7] = @"\a", [8] = @"\b", [9] = @"\t",
        [10] = @"\n", [11] = @"\v", [12] = @"\f", [13] = @"\r",
        [27] = @"\e", [92] = @"\\"
    };

    /// <summary>扩展区（Latin-1）中值得单独标注的字符</summary>
    private static readonly Dictionary<int, string> ExtendedDescs = new()
    {
        [160] = "不换行空格（NBSP）",
        [161] = "倒感叹号", [162] = "分币符号", [163] = "英镑符号", [164] = "通用货币符号",
        [165] = "日元 / 人民币符号", [166] = "断开的竖线", [167] = "节符号", [168] = "分音符",
        [169] = "版权符号 ©", [170] = "阴性序数标记", [171] = "左双角引号 «", [172] = "逻辑非符号",
        [174] = "注册商标 ®", [175] = "长音符", [176] = "度符号 °", [177] = "正负号 ±",
        [178] = "上标 2", [179] = "上标 3", [180] = "锐音符", [181] = "微符号 µ",
        [182] = "段落符号 ¶", [183] = "间隔中点", [184] = "软音符", [185] = "上标 1",
        [186] = "阳性序数标记", [187] = "右双角引号 »", [188] = "四分之一", [189] = "二分之一",
        [190] = "四分之三", [191] = "倒问号", [215] = "乘号 ×", [247] = "除号 ÷"
    };

    private static readonly List<AsciiEntry> _all = Build();

    /// <summary>完整码表（0-255，共 256 条）</summary>
    public static IReadOnlyList<AsciiEntry> All => _all;

    private static List<AsciiEntry> Build()
    {
        var list = new List<AsciiEntry>(256);

        // 0-127 标准 ASCII
        for (int i = 0; i < 128; i++)
        {
            bool isControl = i < 32 || i == 127;
            list.Add(new AsciiEntry
            {
                Dec = i,
                Hex = i.ToString("X2"),
                Oct = Convert.ToString(i, 8).PadLeft(3, '0'),
                Bin = Convert.ToString(i, 2).PadLeft(8, '0'),
                Char = isControl ? string.Empty : ((char)i).ToString(),
                Abbr = Abbrs[i],
                Desc = Descs[i],
                IsControl = isControl,
                Esc = Escapes.TryGetValue(i, out var esc) ? esc : "-"
            });
        }

        // 128-255 扩展区（ISO-8859-1 / Latin-1）
        for (int i = 128; i < 256; i++)
        {
            // 128-159 在 Latin-1 中是 C1 控制字符，不对应可见字形
            bool isControl = i < 160;
            string desc = ExtendedDescs.TryGetValue(i, out var d)
                ? d
                : (isControl ? "扩展控制字符（C1，Latin-1）" : "带音调的扩展拉丁字母（Latin-1）");

            list.Add(new AsciiEntry
            {
                Dec = i,
                Hex = i.ToString("X2"),
                Oct = Convert.ToString(i, 8).PadLeft(3, '0'),
                Bin = Convert.ToString(i, 2).PadLeft(8, '0'),
                Char = isControl ? string.Empty : ((char)i).ToString(),
                Abbr = isControl ? "C1" : ((char)i).ToString(),
                Desc = desc,
                IsControl = isControl,
                Esc = "-"
            });
        }

        return list;
    }
}
