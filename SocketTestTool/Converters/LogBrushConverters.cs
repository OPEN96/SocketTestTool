using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SocketTestTool.Models;

namespace SocketTestTool.Converters;

/// <summary>日志方向 -> 方向文字颜色（深色日志区上使用的高亮色）</summary>
public sealed class LogDirectionBrushConverter : IValueConverter
{
    private static readonly IBrush Received = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
    private static readonly IBrush Sent = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly IBrush Relay = new SolidColorBrush(Color.FromRgb(0xBA, 0x68, 0xC8));
    private static readonly IBrush Error = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly IBrush Info = new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LogDirection dir
            ? dir switch
            {
                LogDirection.Received => Received,
                LogDirection.Sent => Sent,
                LogDirection.Relay => Relay,
                LogDirection.Error => Error,
                _ => Info
            }
            : Info;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>命中高亮 -> 背景色（命中为醒目的黄色底）</summary>
public sealed class HighlightBackgroundConverter : IValueConverter
{
    private static readonly IBrush Hit = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
    private static readonly IBrush Normal = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Hit : Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>命中高亮 -> 文字颜色（黄底上改用深色文字保证可读）</summary>
public sealed class HighlightForegroundConverter : IValueConverter
{
    private static readonly IBrush Hit = new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x30));
    private static readonly IBrush Normal = new SolidColorBrush(Color.FromRgb(0xD7, 0xDD, 0xE8));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Hit : Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
