using Avalonia.Controls;

namespace SocketTestTool.Views;

/// <summary>
/// 自动应答规则窗口：直接以主窗口 ViewModel 作为 DataContext，
/// 复用其规则集合与命令（新增 / 编辑 / 删除 / 排序 / 启用开关）。
/// </summary>
public partial class AutoReplyWindow : Window
{
    public AutoReplyWindow()
    {
        InitializeComponent();
    }
}
