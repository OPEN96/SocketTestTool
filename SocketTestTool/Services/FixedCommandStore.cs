using System.Text.Json;
using SocketTestTool.Models;

namespace SocketTestTool.Services;

/// <summary>
/// 固定指令的本地持久化存储。
/// 数据以 JSON 形式保存在程序目录下的 data/fixed-commands.json，
/// 通过 <see cref="Load"/> / <see cref="Save"/> 读写，内部加锁保证线程安全。
/// </summary>
public sealed class FixedCommandStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();

    public FixedCommandStore(string? filePath = null)
    {
        if (filePath == null)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            filePath = Path.Combine(dir, "fixed-commands.json");
        }
        _filePath = filePath;
    }

    /// <summary>读取全部固定指令；文件不存在或损坏时返回空列表。</summary>
    public List<FixedCommand> Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return new List<FixedCommand>();

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<FixedCommand>>(json, Options) ?? new List<FixedCommand>();
            }
            catch
            {
                // 文件损坏时返回空列表，避免程序启动失败
                return new List<FixedCommand>();
            }
        }
    }

    /// <summary>保存全部固定指令到本地文件。</summary>
    public void Save(IEnumerable<FixedCommand> commands)
    {
        lock (_lock)
        {
            string dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(commands, Options);
            File.WriteAllText(_filePath, json);
        }
    }
}
