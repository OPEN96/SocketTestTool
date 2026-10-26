using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocketTestTool.Models;
using SocketTestTool.Services;

namespace SocketTestTool.ViewModels;

/// <summary>ASCII 查询表窗口的视图模型：负责筛选、选中、复制与插入回主窗口</summary>
public partial class AsciiTableViewModel : ObservableObject
{
    private readonly IReadOnlyList<AsciiEntry> _all = AsciiTable.All;

    /// <summary>请求把文本写入剪贴板（由窗口执行，ViewModel 不直接访问剪贴板）</summary>
    public event Action<string>? CopyRequested;

    /// <summary>请求把某个码值插入主窗口发送框（参数是十进制码值）</summary>
    public event Action<int>? InsertRequested;

    /// <summary>筛选后的列表</summary>
    public ObservableCollection<AsciiEntry> Filtered { get; } = new();

    /// <summary>搜索关键字：支持十进制、十六进制、八进制、字符或中文说明</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 范围筛选：0=全部 / 1=控制字符 / 2=可打印字符 / 3=扩展字符(128-255)
    /// </summary>
    [ObservableProperty]
    private int _rangeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSummary))]
    [NotifyCanExecuteChangedFor(nameof(CopyHexCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyDecCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCharCommand))]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    private AsciiEntry? _selected;

    /// <summary>底部状态栏展示的选中摘要</summary>
    public string SelectedSummary => Selected is { } e
        ? $"已选：{e.Dec}  ·  0x{e.Hex}  ·  八进制 {e.Oct}  ·  {e.Desc}"
        : "未选中（双击行或点「插入发送框」把字符追加到主窗口发送内容）";

    /// <summary>当前筛选结果的条数</summary>
    [ObservableProperty]
    private int _resultCount;

    public AsciiTableViewModel() => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnRangeIndexChanged(int value) => ApplyFilter();

    private void ApplyFilter()
    {
        string kw = SearchText.Trim();
        bool hasKw = kw.Length > 0;
        // 十六进制 / 八进制按无前导零比对，输入 "A" 也能命中 "0A"
        string hexKw = kw.ToUpperInvariant().TrimStart('0');
        string octKw = kw.TrimStart('0');

        Filtered.Clear();
        foreach (var e in _all)
        {
            if (!MatchRange(e)) continue;
            if (hasKw && !MatchKeyword(e, kw, hexKw, octKw)) continue;
            Filtered.Add(e);
        }
        ResultCount = Filtered.Count;
    }

    private bool MatchRange(AsciiEntry e) => RangeIndex switch
    {
        1 => e.IsControl && e.Dec < 128,   // 控制字符
        2 => !e.IsControl && e.Dec < 128,  // 可打印字符
        3 => e.Dec >= 128,                 // 扩展字符
        _ => true
    };

    private static bool MatchKeyword(AsciiEntry e, string kw, string hexKw, string octKw)
    {
        var lower = kw.ToLowerInvariant();
        if (e.Dec.ToString().Contains(lower, StringComparison.Ordinal)) return true;
        if (hexKw.Length > 0 && e.Hex.TrimStart('0').Contains(hexKw, StringComparison.Ordinal)) return true;
        if (octKw.Length > 0 && e.Oct.TrimStart('0').Contains(octKw, StringComparison.Ordinal)) return true;
        if (e.Bin.Contains(lower, StringComparison.Ordinal)) return true;
        if (e.Char.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        if (e.Abbr.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        if (e.Desc.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ---------- 操作 ----------

    private bool HasSelection() => Selected != null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyHex() => CopyRequested?.Invoke(Selected!.Hex);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyDec() => CopyRequested?.Invoke(Selected!.Dec.ToString());

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyChar()
    {
        var e = Selected!;
        CopyRequested?.Invoke(e.IsControl ? e.Abbr : e.Char);
    }

    /// <summary>把选中的码值插入主窗口发送框</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Insert() => InsertRequested?.Invoke(Selected!.Dec);

    /// <summary>双击列表行时插入</summary>
    public void InsertEntry(AsciiEntry? entry)
    {
        if (entry == null) return;
        Selected = entry;
        InsertRequested?.Invoke(entry.Dec);
    }

    /// <summary>清空搜索条件</summary>
    [RelayCommand]
    private void Reset()
    {
        SearchText = string.Empty;
        RangeIndex = 0;
    }
}
