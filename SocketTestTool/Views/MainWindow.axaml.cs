using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;   // ClipboardExtensions.SetTextAsync 扩展方法
using Avalonia.Threading;
using SocketTestTool.Models;
using SocketTestTool.ViewModels;

namespace SocketTestTool.Views;

public partial class MainWindow : Window
{
    private AsciiTableWindow? _asciiWindow;
    private Base64Window? _base64Window;
    private ToolboxWindow? _toolboxWindow;
    private AutoReplyWindow? _autoReplyWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        if (DataContext is MainWindowViewModel vm)
        {
            // 入口按钮 / 快捷键 -> 打开或前置工具窗口
            vm.OpenAsciiTableRequested += ShowOrActivateAsciiTable;
            vm.OpenBase64ToolRequested += ShowOrActivateBase64Window;
            vm.OpenToolboxRequested += ShowOrActivateToolbox;
            vm.OpenAutoReplyRequested += ShowOrActivateAutoReply;

            // 日志列表：新条目到达且未暂停时滚动到底部；复制请求交由剪贴板
            vm.LogScrollToEndRequested += ScrollLogToEnd;
            vm.CopyTextRequested += text => _ = CopyTextAsync(text);

            // 双击日志行复制到剪贴板
            if (LogList is { } list)
                list.DoubleTapped += OnLogDoubleTapped;
        }
    }

    /// <summary>打开 ASCII 码表窗口；已打开则激活并提到最前</summary>
    private void ShowOrActivateAsciiTable()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_asciiWindow is { } existing)
        {
            if (!existing.IsVisible) existing.Show(this);
            existing.Activate();
            return;
        }

        var win = new AsciiTableWindow
        {
            // 选中码值后按发送区当前格式追加到发送内容
            OnInsert = vm.AppendByteToSendText
        };
        win.Closed += (_, _) => _asciiWindow = null;
        _asciiWindow = win;
        win.Show(this);
    }

    /// <summary>打开 Base64 工具窗口；已打开则激活并提到最前</summary>
    private void ShowOrActivateBase64Window()
    {
        if (_base64Window is { } existing)
        {
            if (!existing.IsVisible) existing.Show(this);
            existing.Activate();
            return;
        }

        var win = new Base64Window();
        win.Closed += (_, _) => _base64Window = null;
        _base64Window = win;
        win.Show(this);
    }

    /// <summary>打开协议工具箱窗口；已打开则激活并提到最前</summary>
    private void ShowOrActivateToolbox()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_toolboxWindow is { } existing)
        {
            if (!existing.IsVisible) existing.Show(this);
            existing.Activate();
            return;
        }

        var win = new ToolboxWindow
        {
            // 工具箱的计算结果插入主窗口发送框（文本, 是否按 HEX 处理）
            OnInsert = vm.AppendToSendText
        };
        win.Closed += (_, _) => _toolboxWindow = null;
        _toolboxWindow = win;
        win.Show(this);
    }

    /// <summary>打开自动应答规则窗口；已打开则激活并提到最前</summary>
    private void ShowOrActivateAutoReply()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_autoReplyWindow is { } existing)
        {
            if (!existing.IsVisible) existing.Show(this);
            existing.Activate();
            return;
        }

        var win = new AutoReplyWindow { DataContext = vm };
        win.Closed += (_, _) => _autoReplyWindow = null;
        _autoReplyWindow = win;
        win.Show(this);
    }

    /// <summary>日志自动滚动到底部</summary>
    private void ScrollLogToEnd()
    {
        if (LogList is { } list && list.ItemCount > 0)
            Dispatcher.UIThread.Post(() => list.ScrollIntoView(list.Items[^1]!), DispatcherPriority.Background);
    }

    /// <summary>双击日志行：复制该行到剪贴板</summary>
    private void OnLogDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && LogList?.SelectedItem is LogEntry entry)
            vm.CopyLogEntry(entry);
    }

    /// <summary>把文本写入系统剪贴板</summary>
    private async Task CopyTextAsync(string text)
    {
        try
        {
            var clipboard = Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
        }
        catch
        {
            // 剪贴板不可用时静默忽略
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        try { _asciiWindow?.Close(); } catch { /* 子窗口已关闭时忽略 */ }
        try { _base64Window?.Close(); } catch { /* 子窗口已关闭时忽略 */ }
        try { _toolboxWindow?.Close(); } catch { /* 子窗口已关闭时忽略 */ }
        try { _autoReplyWindow?.Close(); } catch { /* 子窗口已关闭时忽略 */ }
        _asciiWindow = null;
        _base64Window = null;
        _toolboxWindow = null;
        _autoReplyWindow = null;
        (DataContext as MainWindowViewModel)?.Dispose();
        base.OnClosing(e);
    }
}
