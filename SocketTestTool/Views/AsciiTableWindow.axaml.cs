using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;   // ClipboardExtensions.SetTextAsync 扩展方法
using Avalonia.Threading;
using SocketTestTool.Models;
using SocketTestTool.ViewModels;

namespace SocketTestTool.Views;

/// <summary>
/// ASCII 码查询表窗口。只读查询工具：可搜索、筛选、复制，
/// 并把选中的码值按主窗口当前格式追加到发送框（由主窗口注入 OnInsert 回调）。
/// </summary>
public partial class AsciiTableWindow : Window
{
    /// <summary>主窗口注入的插入回调，参数为十进制码值</summary>
    public Action<int>? OnInsert { get; set; }

    private AsciiTableViewModel? _vm;

    public AsciiTableWindow()
    {
        InitializeComponent();

        var vm = new AsciiTableViewModel();
        _vm = vm;
        DataContext = vm;

        vm.CopyRequested += text => _ = CopyTextAsync(text);
        vm.InsertRequested += code => OnInsert?.Invoke(code);

        // 打开后自动聚焦搜索框，可直接输入查询
        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Loaded);
    }

    private async Task CopyTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var clipboard = Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
        }
        catch
        {
            // 剪贴板不可用时静默忽略，不影响查询
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TableList.SelectedItem is AsciiEntry entry)
            _vm?.InsertEntry(entry);
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TableList.SelectedItem is AsciiEntry entry)
        {
            _vm?.InsertEntry(entry);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
