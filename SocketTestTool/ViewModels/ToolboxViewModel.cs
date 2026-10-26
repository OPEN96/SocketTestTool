using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocketTestTool.Helpers;

namespace SocketTestTool.ViewModels;

/// <summary>
/// 协议调试工具箱（校验码 / 进制转换 / 时间换算）的视图模型。
/// 文件与剪贴板等平台能力通过事件交由窗口层处理。
/// </summary>
public partial class ToolboxViewModel : ObservableObject
{
    /// <summary>请求把文本复制到剪贴板</summary>
    public event Action<string>? CopyRequested;

    /// <summary>请求把内容插入主窗口发送框：(文本, 是否按 HEX 处理)</summary>
    public event Action<string, bool>? InsertRequested;

    // ==================== 一、校验码 ====================

    public string[] Algorithms => ChecksumHelper.AlgorithmNames;

    /// <summary>校验输入来源选项：0=HEX，1=文本</summary>
    public string[] ChecksumInputFormats => ["HEX 字节", "文本字符串"];

    /// <summary>输入来源：0=HEX，1=文本</summary>
    [ObservableProperty] private int _checksumInputMode;

    [ObservableProperty] private string _checksumInput = string.Empty;

    [ObservableProperty] private int _algorithmIndex;

    /// <summary>结果字节序：true=低字节在前（常见报文顺序）</summary>
    [ObservableProperty] private bool _resultLittleEndian = true;

    [ObservableProperty] private string _checksumHex = string.Empty;
    [ObservableProperty] private string _checksumDec = string.Empty;
    [ObservableProperty] private string _checksumBin = string.Empty;

    /// <summary>输入 HEX + 校验字节拼成的完整帧</summary>
    [ObservableProperty] private string _checksumFrame = string.Empty;

    [ObservableProperty] private string _checksumError = string.Empty;
    [ObservableProperty] private bool _hasChecksumError;

    // ==================== 二、进制转换 ====================

    public string[] NumWidths => ["8 位", "16 位", "32 位", "64 位"];
    public string[] EndianWidths => ["16 位", "32 位", "64 位"];
    public string[] FloatModes => ["单精度 float32", "双精度 double64"];

    [ObservableProperty] private string _numInput = "255";
    [ObservableProperty] private bool _numSigned;
    [ObservableProperty] private int _numWidthIndex = 2;
    [ObservableProperty] private string _numHex = string.Empty;
    [ObservableProperty] private string _numDec = string.Empty;
    [ObservableProperty] private string _numSignedDec = string.Empty;
    [ObservableProperty] private string _numOct = string.Empty;
    [ObservableProperty] private string _numBin = string.Empty;
    [ObservableProperty] private string _numError = string.Empty;
    [ObservableProperty] private bool _hasNumError;

    [ObservableProperty] private int _endianWidthIndex = 1;
    [ObservableProperty] private string _endianInput = "12 34 56 78";
    [ObservableProperty] private string _endianOutput = string.Empty;
    [ObservableProperty] private string _endianError = string.Empty;
    [ObservableProperty] private bool _hasEndianError;

    [ObservableProperty] private int _floatModeIndex;
    [ObservableProperty] private bool _floatLittleEndian = true;
    [ObservableProperty] private string _floatDec = "3.14";
    [ObservableProperty] private string _floatHex = string.Empty;
    [ObservableProperty] private string _floatError = string.Empty;
    [ObservableProperty] private bool _hasFloatError;

    [ObservableProperty] private string _bcdDigits = "1234";
    [ObservableProperty] private string _bcdHex = string.Empty;
    [ObservableProperty] private string _bcdError = string.Empty;
    [ObservableProperty] private bool _hasBcdError;

    // ==================== 三、时间换算 ====================

    public string[] TimeZones => ["本地时区", "UTC", "自定义 UTC 偏移"];

    /// <summary>是否使用自定义 UTC 偏移（时区选择第 3 项时）</summary>
    public bool IsCustomTimeZone => TimeZoneIndex == 2;

    [ObservableProperty] private string _unixInput = string.Empty;
    [ObservableProperty] private string _dateInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomTimeZone))]
    private int _timeZoneIndex;
    [ObservableProperty] private string _utcOffsetHours = "8";

    [ObservableProperty] private string _timeDateText = string.Empty;
    [ObservableProperty] private string _timeUtcText = string.Empty;
    [ObservableProperty] private string _timeWeekText = string.Empty;
    [ObservableProperty] private string _timeUnix = string.Empty;
    [ObservableProperty] private string _timeUnixMs = string.Empty;
    [ObservableProperty] private string _timeIso = string.Empty;
    [ObservableProperty] private string _timeHexBigEndian = string.Empty;
    [ObservableProperty] private string _timeHexLittleEndian = string.Empty;
    [ObservableProperty] private string _timeError = string.Empty;
    [ObservableProperty] private bool _hasTimeError;

    // 防止两个输入框互相触发重算
    private bool _syncing;

    /// <summary>底部状态提示</summary>
    [ObservableProperty]
    private string _statusText = "提示：插入发送框会把结果追加到主窗口发送内容末尾；HEX 类结果在发送区为 ASCII 模式时会自动切换为 HEX";

    public void ShowStatus(string text) => StatusText = text;

    public ToolboxViewModel()
    {
        ComputeChecksum();
        ConvertNumber();
        SwapEndian();
        ConvertFloat();
        EncodeBcd();
        UseNow();
    }

    // ---------- 校验码 ----------

    partial void OnChecksumInputChanged(string value) => ComputeChecksum();
    partial void OnChecksumInputModeChanged(int value) => ComputeChecksum();
    partial void OnAlgorithmIndexChanged(int value) => ComputeChecksum();
    partial void OnResultLittleEndianChanged(bool value) => ComputeChecksum();

    private void ComputeChecksum()
    {
        HasChecksumError = false;
        ChecksumError = string.Empty;

        byte[] data;
        string inputHex;

        if (ChecksumInputMode == 0)
        {
            try
            {
                data = HexHelper.Parse(ChecksumInput);
            }
            catch (Exception ex)
            {
                HasChecksumError = true;
                ChecksumError = ex.Message;
                ChecksumHex = ChecksumDec = ChecksumBin = ChecksumFrame = string.Empty;
                return;
            }
            inputHex = string.Join(' ', data.Select(b => b.ToString("X2")));
        }
        else
        {
            if (string.IsNullOrEmpty(ChecksumInput))
            {
                ChecksumHex = ChecksumDec = ChecksumBin = ChecksumFrame = string.Empty;
                return;
            }
            data = Encoding.UTF8.GetBytes(ChecksumInput);
            inputHex = string.Join(' ', data.Select(b => b.ToString("X2")));
        }

        var alg = (ChecksumAlgorithm)AlgorithmIndex;
        byte[] result = ChecksumHelper.ComputeBytes(alg, data, ResultLittleEndian);

        ChecksumHex = string.Join(' ', result.Select(b => b.ToString("X2")));
        ulong value = ChecksumHelper.ComputeValue(alg, data);
        ChecksumDec = value.ToString(CultureInfo.InvariantCulture);
        ChecksumBin = string.Join(' ', result.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        ChecksumFrame = data.Length == 0
            ? ChecksumHex
            : (inputHex.Length == 0 ? ChecksumHex : inputHex + " " + ChecksumHex);
    }

    // ---------- 进制转换 ----------

    partial void OnNumInputChanged(string value) => ConvertNumber();
    partial void OnNumSignedChanged(bool value) => ConvertNumber();
    partial void OnNumWidthIndexChanged(int value) => ConvertNumber();

    private void ConvertNumber()
    {
        HasNumError = false;
        NumError = string.Empty;

        if (string.IsNullOrWhiteSpace(NumInput))
        {
            NumHex = NumDec = NumSignedDec = NumOct = NumBin = string.Empty;
            return;
        }

        if (!NumberConverter.TryParse(NumInput, out ulong v))
        {
            HasNumError = true;
            NumError = "无法解析，请输入十进制（255）、0x 十六进制（0xFF）、0b 二进制（0b1111）或 0o 八进制（0o377）";
            NumHex = NumDec = NumSignedDec = NumOct = NumBin = string.Empty;
            return;
        }

        int widthBits = NumWidthIndex switch { 0 => 8, 1 => 16, 2 => 32, _ => 64 };
        ulong mask = widthBits == 64 ? ulong.MaxValue : (1UL << widthBits) - 1;
        ulong masked = v & mask;

        NumHex = "0x" + masked.ToString("X" + (widthBits / 4));
        NumDec = masked.ToString(CultureInfo.InvariantCulture);
        NumOct = "0o" + Convert.ToString((long)masked, 8);
        NumBin = "0b" + NumberConverter.ToBinaryGrouped(masked, widthBits);
        NumSignedDec = ((long)masked).ToString(CultureInfo.InvariantCulture);
    }

    partial void OnEndianInputChanged(string value) => SwapEndian();
    partial void OnEndianWidthIndexChanged(int value) => SwapEndian();

    [RelayCommand]
    private void SwapEndian()
    {
        HasEndianError = false;
        EndianError = string.Empty;

        int widthBits = EndianWidthIndex switch { 0 => 16, 1 => 32, _ => 64 };

        if (string.IsNullOrWhiteSpace(EndianInput))
        {
            EndianOutput = string.Empty;
            return;
        }

        bool ok = NumberConverter.TrySwapEndian(EndianInput, widthBits, out string result);
        if (!ok)
        {
            HasEndianError = true;
            EndianError = result;
            EndianOutput = string.Empty;
            return;
        }
        EndianOutput = result;
    }

    partial void OnFloatDecChanged(string value) => ConvertFloat();
    partial void OnFloatModeIndexChanged(int value) => ConvertFloat();
    partial void OnFloatLittleEndianChanged(bool value) => ConvertFloat();

    private void ConvertFloat()
    {
        HasFloatError = false;
        FloatError = string.Empty;

        if (string.IsNullOrWhiteSpace(FloatDec))
        {
            FloatHex = string.Empty;
            return;
        }

        if (!NumberConverter.TryFloatToHex(FloatDec, FloatModeIndex == 1, FloatLittleEndian, out string hex))
        {
            HasFloatError = true;
            FloatError = "无法解析为浮点数，请输入如 3.14、-0.5、1e-3";
            FloatHex = string.Empty;
            return;
        }

        _syncing = true;
        FloatHex = hex;
        _syncing = false;
    }

    partial void OnFloatHexChanged(string value) => ParseFloatHex();

    /// <summary>反向：HEX -> 浮点（用户直接编辑 HEX 时触发）</summary>
    private void ParseFloatHex()
    {
        if (_syncing) return;
        HasFloatError = false;
        FloatError = string.Empty;

        if (string.IsNullOrWhiteSpace(FloatHex))
        {
            FloatDec = string.Empty;
            return;
        }

        if (!NumberConverter.TryHexToFloat(FloatHex, FloatModeIndex == 1, FloatLittleEndian, out string dec))
        {
            HasFloatError = true;
            FloatError = FloatModeIndex == 1
                ? "请输入 8 字节十六进制（如 00 00 00 00 00 00 F0 3F）"
                : "请输入 4 字节十六进制（如 C3 F5 48 40）";
            return;
        }

        _syncing = true;
        FloatDec = dec;
        _syncing = false;
    }

    partial void OnBcdDigitsChanged(string value) => EncodeBcd();

    private void EncodeBcd()
    {
        HasBcdError = false;
        BcdError = string.Empty;

        if (string.IsNullOrWhiteSpace(BcdDigits))
        {
            BcdHex = string.Empty;
            return;
        }

        if (!NumberConverter.TryBcdEncode(BcdDigits, out string hex))
        {
            HasBcdError = true;
            BcdError = "请输入纯数字，如 1234";
            BcdHex = string.Empty;
            return;
        }

        _syncing = true;
        BcdHex = hex;
        _syncing = false;
    }

    partial void OnBcdHexChanged(string value) => DecodeBcd();

    private void DecodeBcd()
    {
        if (_syncing) return;
        HasBcdError = false;
        BcdError = string.Empty;

        if (string.IsNullOrWhiteSpace(BcdHex)) return;

        if (!NumberConverter.TryBcdDecode(BcdHex, out string digits))
        {
            HasBcdError = true;
            BcdError = digits; // 失败时 digits 携带原因
            return;
        }

        _syncing = true;
        BcdDigits = digits;
        _syncing = false;
    }

    // ---------- 时间换算 ----------

    partial void OnUnixInputChanged(string value) { if (!_syncing) RecalcFromUnix(); }
    partial void OnDateInputChanged(string value) { if (!_syncing) RecalcFromDate(); }
    partial void OnTimeZoneIndexChanged(int value) { if (!_syncing) RecalcKeepSource(); }
    partial void OnUtcOffsetHoursChanged(string value) { if (!_syncing) RecalcKeepSource(); }

    /// <summary>填入当前时间（秒级）</summary>
    [RelayCommand]
    private void UseNow()
    {
        var now = DateTimeOffset.Now;
        _syncing = true;
        UnixInput = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        _syncing = false;
        RecalcFromUnix();
    }

    /// <summary>时区/偏移变化时：以现有输入为源重新计算</summary>
    private void RecalcKeepSource()
    {
        if (!string.IsNullOrWhiteSpace(UnixInput)) RecalcFromUnix();
        else if (!string.IsNullOrWhiteSpace(DateInput)) RecalcFromDate();
    }

    private void RecalcFromUnix()
    {
        HasTimeError = false;
        TimeError = string.Empty;

        string s = (UnixInput ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            ClearTimeOutputs();
            return;
        }

        if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long raw))
        {
            HasTimeError = true;
            TimeError = "时间戳只能是整数（秒或毫秒）";
            ClearTimeOutputs();
            return;
        }

        // 位数较多时按毫秒处理（13 位毫秒在 2001-2286 年之间）
        bool isMs = Math.Abs(raw) > 9_999_999_999L;
        DateTimeOffset dto = isMs
            ? DateTimeOffset.FromUnixTimeMilliseconds(raw)
            : DateTimeOffset.FromUnixTimeSeconds(raw);

        FillTimeOutputs(dto);
    }

    private void RecalcFromDate()
    {
        HasTimeError = false;
        TimeError = string.Empty;

        string s = (DateInput ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            ClearTimeOutputs();
            return;
        }

        string[] formats =
        [
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd",
            "yyyyMMddHHmmss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ"
        ];

        if (!DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
            && !DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            HasTimeError = true;
            TimeError = "日期格式无法识别，请使用 2026-10-27 00:47:59 这样的格式";
            ClearTimeOutputs();
            return;
        }

        TimeSpan offset = GetOffset(parsed);
        var dto = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), offset);
        FillTimeOutputs(dto);
    }

    /// <summary>按所选时区计算 UTC 偏移</summary>
    private TimeSpan GetOffset(DateTime localPart) => TimeZoneIndex switch
    {
        1 => TimeSpan.Zero,
        2 => TimeSpan.FromHours(ParseOffsetHours()),
        _ => TimeZoneInfo.Local.GetUtcOffset(DateTime.SpecifyKind(localPart, DateTimeKind.Local))
    };

    private double ParseOffsetHours()
        => double.TryParse(UtcOffsetHours, NumberStyles.Float, CultureInfo.InvariantCulture, out double h)
            ? Math.Clamp(h, -14, 14)
            : 0;

    private void FillTimeOutputs(DateTimeOffset dto)
    {
        TimeSpan offset = TimeZoneIndex switch
        {
            1 => TimeSpan.Zero,
            2 => TimeSpan.FromHours(ParseOffsetHours()),
            _ => TimeZoneInfo.Local.GetUtcOffset(dto.UtcDateTime)
        };
        DateTimeOffset shown = dto.ToOffset(offset);

        _syncing = true;
        UnixInput = dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        DateInput = shown.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        _syncing = false;

        TimeDateText = shown.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        TimeUtcText = dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
        TimeWeekText = "星期" + "日一二三四五六"[(int)shown.DayOfWeek];
        TimeUnix = dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        TimeUnixMs = dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        TimeIso = dto.ToString("o", CultureInfo.InvariantCulture);

        uint sec = (uint)dto.ToUnixTimeSeconds();
        byte[] be = [(byte)(sec >> 24), (byte)(sec >> 16), (byte)(sec >> 8), (byte)sec];
        byte[] le = be.Reverse().ToArray();
        TimeHexBigEndian = string.Join(' ', be.Select(b => b.ToString("X2")));
        TimeHexLittleEndian = string.Join(' ', le.Select(b => b.ToString("X2")));
    }

    private void ClearTimeOutputs()
    {
        TimeDateText = TimeUtcText = TimeWeekText = TimeUnix = TimeUnixMs =
            TimeIso = TimeHexBigEndian = TimeHexLittleEndian = string.Empty;
    }

    // ---------- 通用命令 ----------

    [RelayCommand]
    private void Copy(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        CopyRequested?.Invoke(text);
    }

    /// <summary>把内容插入主窗口发送框（按 HEX 处理，必要时自动切换发送格式）</summary>
    [RelayCommand]
    private void InsertHex(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        InsertRequested?.Invoke(text, true);
    }

    /// <summary>把内容插入主窗口发送框（按文本处理）</summary>
    [RelayCommand]
    private void InsertText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        InsertRequested?.Invoke(text, false);
    }
}
