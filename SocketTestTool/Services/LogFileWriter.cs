using System.Text;

namespace SocketTestTool.Services;

/// <summary>
/// 文本日志写入器：将格式化后的日志行追加写入 logs/ 目录下的文本文件。
/// 线程安全（内部加锁），写入后立即 Flush，保证异常退出时日志不丢失。
/// </summary>
public sealed class LogFileWriter : IDisposable
{
    private readonly object _sync = new();
    private StreamWriter? _writer;

    private LogFileWriter(string path)
    {
        FilePath = path;
        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public string FilePath { get; }

    /// <summary>在程序运行目录的 logs/ 下创建一个新日志文件</summary>
    public static LogFileWriter Create()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"socket-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        return new LogFileWriter(path);
    }

    public void WriteLine(string line)
    {
        lock (_sync)
        {
            _writer?.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
