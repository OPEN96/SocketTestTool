using Avalonia.Controls;
using Avalonia.Threading;
using SocketTestTool.ViewModels;

namespace SocketTestTool.Views;

public partial class MainWindow : Window
{
    private AsciiTableWindow? _asciiWindow;
    private Base64Window? _base64Window;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        if (DataContext is MainWindowViewModel vm)
        {
            // 日志更新时自动滚动到底部
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.LogText))
                    Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd(), DispatcherPriority.Background);
            };

            // 入口按钮 / 快捷键 -> 打开或前置工具窗口
            vm.OpenAsciiTableRequested += ShowOrActivateAsciiTable;
            vm.OpenBase64ToolRequested += ShowOrActivateBase64Window;
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

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        try { _asciiWindow?.Close(); } catch { /* 子窗口已关闭时忽略 */ }
        try { _base64Window?.Close(); } catch { /* 子窗口已关闭时忽略 */ }
        _asciiWindow = null;
        _base64Window = null;
        (DataContext as MainWindowViewModel)?.Dispose();
        base.OnClosing(e);
    }
}
