using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;   // ClipboardExtensions.SetTextAsync 扩展方法
using Avalonia.Platform.Storage;
using SocketTestTool.ViewModels;

namespace SocketTestTool.Views;

/// <summary>
/// Base64 编解码工具窗口。文件对话框与剪贴板在本层执行，
/// 业务转换逻辑全部位于 <see cref="Base64ViewModel"/>。
/// </summary>
public partial class Base64Window : Window
{
    private Base64ViewModel? _vm;

    public Base64Window()
    {
        InitializeComponent();

        var vm = new Base64ViewModel();
        _vm = vm;
        DataContext = vm;

        vm.CopyRequested += text => _ = CopyTextAsync(text);
        vm.OpenFileRequested += () => _ = OpenFileAsync();
        vm.SaveFileRequested += () => _ = SaveFileAsync();
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
                _vm?.ShowMessage($"已复制到剪贴板（{text.Length:N0} 个字符）");
            }
        }
        catch (Exception ex)
        {
            _vm?.ShowError("复制失败：" + ex.Message);
        }
    }

    /// <summary>选择本地文件（二进制）并转换为 Base64</summary>
    private async Task OpenFileAsync()
    {
        if (_vm is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要转换为 Base64 的文件",
            AllowMultiple = false
        });

        if (files is null || files.Count == 0) return;
        var file = files[0];

        try
        {
            byte[] data;
            string? localPath = file.TryGetLocalPath();

            if (localPath != null)
            {
                var info = new FileInfo(localPath);
                if (info.Length > Base64ViewModel.MaxFileBytes)
                {
                    _vm.ShowError($"文件过大：{info.Length / 1024 / 1024} MB，超出单次处理上限 {Base64ViewModel.MaxFileBytes / 1024 / 1024} MB，请拆分后再试");
                    return;
                }
                data = await File.ReadAllBytesAsync(localPath);
            }
            else
            {
                await using var src = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await src.CopyToAsync(ms);
                if (ms.Length > Base64ViewModel.MaxFileBytes)
                {
                    _vm.ShowError($"文件过大：{ms.Length / 1024 / 1024} MB，超出单次处理上限 {Base64ViewModel.MaxFileBytes / 1024 / 1024} MB，请拆分后再试");
                    return;
                }
                data = ms.ToArray();
            }

            if (data.Length == 0)
            {
                _vm.ShowError("文件内容为空，无需转换");
                return;
            }

            _vm.LoadFile(data, file.Name);
        }
        catch (Exception ex)
        {
            _vm.ShowError("读取文件失败：" + ex.Message);
        }
    }

    /// <summary>把当前数据（解码结果）导出为二进制文件</summary>
    private async Task SaveFileAsync()
    {
        if (_vm is null) return;

        byte[] bytes = _vm.GetCurrentBytes();
        if (bytes.Length == 0)
        {
            _vm.ShowError("当前没有可导出的数据：请先输入文本并编码、导入文件，或在右侧填入 Base64");
            return;
        }

        string suggested = _vm.SuggestedFileName;
        string ext = Path.GetExtension(suggested).TrimStart('.');

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出为二进制文件",
            SuggestedFileName = suggested,
            DefaultExtension = string.IsNullOrEmpty(ext) ? "bin" : ext,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
            }
        });

        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(bytes);
            _vm.ShowMessage($"已导出 {bytes.Length:N0} 字节到：{file.Name}");
        }
        catch (Exception ex)
        {
            _vm.ShowError("导出文件失败：" + ex.Message);
        }
    }
}
