using Avalonia.Controls;
using Avalonia.Input.Platform;   // ClipboardExtensions.SetTextAsync 扩展方法
using SocketTestTool.ViewModels;

namespace SocketTestTool.Views;

/// <summary>
/// 协议工具箱窗口：校验码 / 进制转换 / 时间换算。
/// 剪贴板在本层执行，插入发送框通过 <see cref="OnInsert"/> 回调交给主窗口处理。
/// </summary>
public partial class ToolboxWindow : Window
{
    private ToolboxViewModel? _vm;

    /// <summary>由主窗口注入：把内容插入主窗口发送框（文本, 是否按 HEX 处理）</summary>
    public Action<string, bool>? OnInsert { get; set; }

    public ToolboxWindow()
    {
        InitializeComponent();

        var vm = new ToolboxViewModel();
        _vm = vm;
        DataContext = vm;

        vm.CopyRequested += text => _ = CopyTextAsync(text);
        vm.InsertRequested += (text, asHex) => OnInsert?.Invoke(text, asHex);
    }

    private async Task CopyTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var clipboard = Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
                _vm?.ShowStatus($"已复制到剪贴板：{text}");
            }
        }
        catch (Exception ex)
        {
            _vm?.ShowStatus("复制失败：" + ex.Message);
        }
    }
}
