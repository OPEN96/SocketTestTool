using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SocketTestTool.Converters;

/// <summary>连接状态 -> 状态指示灯颜色（绿=已连接/监听中，灰=未连接）</summary>
public sealed class ConnectedBrushConverter : IValueConverter
{
    private static readonly IBrush Connected = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly IBrush Disconnected = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Connected : Disconnected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
