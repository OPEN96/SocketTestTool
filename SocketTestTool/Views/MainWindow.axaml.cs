using Avalonia.Controls;
using Avalonia.Threading;
using SocketTestTool.ViewModels;

namespace SocketTestTool.Views;

public partial class MainWindow : Window
{
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
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.Dispose();
        base.OnClosing(e);
    }
}
