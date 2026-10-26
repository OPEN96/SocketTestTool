using System.Text;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocketTestTool.Helpers;

namespace SocketTestTool.ViewModels;

/// <summary>
/// Base64 编解码工具的视图模型：
/// 文本/文件 → Base64、Base64 → 文本/二进制文件、Base64 图片预览。
/// 文件对话框与剪贴板由窗口（View）执行，ViewModel 只发出请求事件。
/// </summary>
public partial class Base64ViewModel : ObservableObject
{
    /// <summary>单文件处理上限：20 MB，避免一次性读入超大文件导致界面卡死</summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    /// <summary>实时自动转换的文本长度上限（字符数），超出后需手动点按钮执行</summary>
    private const int AutoSyncLimit = 2_000_000;

    /// <summary>图片预览的体积上限：8 MB，超出只提示不解码</summary>
    private const long MaxPreviewBytes = 8L * 1024 * 1024;

    /// <summary>请求写入剪贴板（由窗口执行）</summary>
    public event Action<string>? CopyRequested;

    /// <summary>请求弹出「打开文件」对话框（由窗口执行）</summary>
    public event Action? OpenFileRequested;

    /// <summary>请求弹出「保存文件」对话框（由窗口执行）</summary>
    public event Action? SaveFileRequested;

    private byte[] _currentBytes = Array.Empty<byte>();
    private string _suggestedFileName = "data.bin";
    private bool _syncing;

    // ---------- 输入 / 输出 ----------

    /// <summary>左侧：明文 / 原始文本内容</summary>
    [ObservableProperty]
    private string _plainText = string.Empty;

    /// <summary>右侧：Base64 文本</summary>
    [ObservableProperty]
    private string _base64Text = string.Empty;

    /// <summary>文本编码方式：0=UTF-8 / 1=ASCII / 2=Latin-1 / 3=UTF-16 / 4=UTF-32</summary>
    [ObservableProperty]
    private int _encodingIndex;

    /// <summary>是否随输入实时互转（关闭后改用中间的方向按钮手动执行）</summary>
    [ObservableProperty]
    private bool _autoSync = true;

    // ---------- 预览 / 状态 ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private Bitmap? _preview;

    public bool HasPreview => Preview != null;

    /// <summary>当前数据的描述：类型、字节数、图片尺寸</summary>
    [ObservableProperty]
    private string _dataInfo = "暂无数据";

    /// <summary>操作状态提示</summary>
    [ObservableProperty]
    private string _status = "就绪：左侧输入文本自动编码，或点「从文件读取」导入二进制文件";

    /// <summary>状态是否为错误（决定提示是否标红）</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>另存为文件时建议的文件名（按识别出的类型给出扩展名）</summary>
    public string SuggestedFileName => _suggestedFileName;

    private Encoding CurrentEncoding => EncodingIndex switch
    {
        1 => Encoding.ASCII,
        2 => Encoding.Latin1,
        3 => Encoding.Unicode,
        4 => Encoding.UTF32,
        _ => Encoding.UTF8
    };

    private string EncodingName => EncodingIndex switch
    {
        1 => "ASCII", 2 => "Latin-1", 3 => "UTF-16", 4 => "UTF-32", _ => "UTF-8"
    };

    // ---------- 实时联动 ----------

    partial void OnPlainTextChanged(string value)
    {
        if (_syncing || !AutoSync) return;
        EncodeCore(silent: true);
    }

    partial void OnBase64TextChanged(string value)
    {
        if (_syncing || !AutoSync) return;
        if (value.Length > AutoSyncLimit)
        {
            SetError($"内容过大（{value.Length:N0} 字符），已暂停实时解码，请点「← 解码」手动执行");
            return;
        }
        DecodeCore(silent: true);
    }

    partial void OnEncodingIndexChanged(int value)
    {
        // 切换编码后用已有的字节重新解释文本，便于对比乱码
        if (_currentBytes.Length > 0)
        {
            _syncing = true;
            try { PlainText = CurrentEncoding.GetString(_currentBytes); }
            finally { _syncing = false; }
            SetOk($"已按 {EncodingName} 重新解释文本（{_currentBytes.Length} 字节）");
        }
    }

    // ---------- 编码 / 解码 ----------

    /// <summary>文本 → Base64</summary>
    [RelayCommand]
    private void Encode() => EncodeCore(silent: false);

    /// <summary>Base64 → 文本</summary>
    [RelayCommand]
    private void Decode() => DecodeCore(silent: false);

    private void EncodeCore(bool silent)
    {
        try
        {
            byte[] bytes = CurrentEncoding.GetBytes(PlainText);
            _currentBytes = bytes;

            _syncing = true;
            try { Base64Text = Convert.ToBase64String(bytes); }
            finally { _syncing = false; }

            _suggestedFileName = "data.bin";
            UpdatePreview(bytes);
            SetOk($"已编码：{bytes.Length:N0} 字节 → {Base64Text.Length:N0} 个 Base64 字符（{EncodingName}）");
        }
        catch (Exception ex)
        {
            SetError("编码失败：" + ex.Message);
        }
    }

    private void DecodeCore(bool silent)
    {
        string raw = StripBase64(Base64Text);
        if (raw.Length == 0)
        {
            Preview = null;
            DataInfo = "暂无数据";
            if (!silent) SetOk("请输入 Base64 内容后再解码");
            return;
        }

        if (raw.Length % 4 != 0)
        {
            SetError($"Base64 长度非法：{raw.Length:N0} 个字符，不是 4 的倍数（可能缺少 = 补位或被截断）");
            return;
        }
        if (raw.Length > MaxFileBytes / 3 * 4)
        {
            SetError($"内容过大：约 {raw.Length / 4 * 3 / 1024 / 1024} MB，超出单次处理上限 20 MB");
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            SetError("Base64 格式非法：包含非 Base64 字符（仅允许 A-Z a-z 0-9 + / 与 = 补位）");
            return;
        }
        catch (Exception ex)
        {
            SetError("解码失败：" + ex.Message);
            return;
        }

        _currentBytes = bytes;
        _syncing = true;
        try { PlainText = CurrentEncoding.GetString(bytes); }
        finally { _syncing = false; }

        UpdatePreview(bytes);

        // 编码不匹配的提示：解码后出现大量替换字符 U+FFFD
        int replaced = PlainText.Count(c => c == '\uFFFD');
        if (replaced > 0)
        {
            SetError($"已解码 {bytes.Length:N0} 字节，但用 {EncodingName} 解释出现 {replaced} 个乱码字符，可尝试切换其他文字编码");
        }
        else
        {
            SetOk($"已解码：{bytes.Length:N0} 字节 → {PlainText.Length:N0} 个字符（{EncodingName}）");
        }
    }

    /// <summary>
    /// 清理 Base64 输入：去掉空白与换行，并支持 data URI（data:image/png;base64,xxx）形式。
    /// </summary>
    private string StripBase64(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string s = input.Trim();
        int comma = s.IndexOf(',');
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            _suggestedFileName = GuessFileName(s[..comma]);   // 如 data:image/png;base64
            s = s[(comma + 1)..];
        }

        return new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    private static string GuessFileName(string meta)
    {
        string mime = meta;
        int semi = meta.IndexOf(';');
        if (semi > 5) mime = meta[5..semi];                  // 去掉 "data:" 前缀
        return mime.ToLowerInvariant() switch
        {
            "image/png" => "image.png",
            "image/jpeg" => "image.jpg",
            "image/gif" => "image.gif",
            "image/bmp" => "image.bmp",
            "image/webp" => "image.webp",
            "image/x-icon" => "image.ico",
            "image/tiff" => "image.tiff",
            "application/pdf" => "document.pdf",
            "application/zip" => "archive.zip",
            "text/plain" => "text.txt",
            "application/json" => "data.json",
            _ => "data.bin"
        };
    }

    // ---------- 预览 ----------

    /// <summary>识别数据类型并更新图片预览与信息</summary>
    private void UpdatePreview(byte[] bytes)
    {
        string? kind = ImageFormatHelper.Detect(bytes);

        if (kind != null)
        {
            if (bytes.Length > MaxPreviewBytes)
            {
                Preview = null;
                DataInfo = $"{kind} 图片 · {bytes.Length:N0} 字节 · 超过 8 MB，已跳过预览（仍可正常导出）";
            }
            else
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new Bitmap(ms);
                    Preview = bmp;
                    DataInfo = $"{kind} 图片 · {bytes.Length:N0} 字节 · {bmp.PixelSize.Width}×{bmp.PixelSize.Height}";
                    if (_suggestedFileName == "data.bin")
                        _suggestedFileName = "image." + kind.ToLowerInvariant();
                    return;
                }
                catch (Exception ex)
                {
                    Preview = null;
                    DataInfo = $"疑似 {kind} 图片但解码失败：{ex.Message}";
                }
            }
            return;
        }

        Preview = null;
        DataInfo = bytes.Length == 0
            ? "暂无数据"
            : $"二进制数据 · {bytes.Length:N0} 字节{TryTextPreview(bytes)}";
    }

    /// <summary>对疑似文本的二进制给出开头片段，便于快速确认内容</summary>
    private string TryTextPreview(byte[] bytes)
    {
        int sample = Math.Min(bytes.Length, 64);
        int printable = bytes.Take(sample)
                             .Count(b => b is >= 0x20 and < 0x7F || b is 0x0A or 0x0D or 0x09);
        if (printable < sample * 0.9) return string.Empty;

        int len = Math.Min(bytes.Length, 40);
        string head = CurrentEncoding.GetString(bytes, 0, len).Replace("\r", " ").Replace("\n", " ");
        return $" · 开头：{head}…";
    }

    // ---------- 文件 ----------

    /// <summary>由窗口在选定文件后调用：把文件内容转为 Base64 并尝试预览</summary>
    public void LoadFile(byte[] data, string fileName)
    {
        _currentBytes = data;
        _suggestedFileName = fileName;

        _syncing = true;
        try
        {
            Base64Text = Convert.ToBase64String(data);
            try { PlainText = CurrentEncoding.GetString(data); }
            catch { PlainText = string.Empty; }
        }
        finally { _syncing = false; }

        UpdatePreview(data);
        SetOk($"已读取文件：{fileName}（{data.Length:N0} 字节 → {Base64Text.Length:N0} 个 Base64 字符）");
    }

    /// <summary>当前 Base64 对应的原始字节（供另存为文件使用）</summary>
    public byte[] GetCurrentBytes()
    {
        if (_currentBytes.Length > 0) return _currentBytes;
        try { return Convert.FromBase64String(StripBase64(Base64Text)); }
        catch { return Array.Empty<byte>(); }
    }

    /// <summary>由窗口在保存失败、文件过大等场景调用，给出明确错误提示</summary>
    public void ShowError(string message) => SetError(message);

    /// <summary>由窗口在文件读写成功等场景调用，给出成功提示</summary>
    public void ShowMessage(string message) => SetOk(message);

    // ---------- 状态 ----------

    private void SetOk(string message)
    {
        Status = message;
        HasError = false;
    }

    private void SetError(string message)
    {
        Status = message;
        HasError = true;
    }

    // ---------- 命令 ----------

    [RelayCommand]
    private void CopyBase64() => CopyRequested?.Invoke(Base64Text);

    [RelayCommand]
    private void CopyText() => CopyRequested?.Invoke(PlainText);

    [RelayCommand]
    private void OpenFile() => OpenFileRequested?.Invoke();

    [RelayCommand]
    private void SaveFile() => SaveFileRequested?.Invoke();

    [RelayCommand]
    private void Clear()
    {
        _syncing = true;
        try
        {
            PlainText = string.Empty;
            Base64Text = string.Empty;
        }
        finally { _syncing = false; }

        _currentBytes = Array.Empty<byte>();
        _suggestedFileName = "data.bin";
        Preview = null;
        DataInfo = "暂无数据";
        SetOk("已清空");
    }
}
