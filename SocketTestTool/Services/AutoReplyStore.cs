using System.Text.Json;
using SocketTestTool.Models;

namespace SocketTestTool.Services;

/// <summary>
/// 自动应答规则的本地持久化存储。
/// 数据以 JSON 形式保存在程序目录下的 data/auto-reply.json。
/// </summary>
public sealed class AutoReplyStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();

    public AutoReplyStore(string? filePath = null)
    {
        if (filePath == null)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            filePath = Path.Combine(dir, "auto-reply.json");
        }
        _filePath = filePath;
    }

    /// <summary>读取配置；文件不存在或损坏时返回空配置。</summary>
    public AutoReplyConfig Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return new AutoReplyConfig();

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AutoReplyConfig>(json, Options) ?? new AutoReplyConfig();
            }
            catch
            {
                return new AutoReplyConfig();
            }
        }
    }

    /// <summary>保存配置到本地文件。</summary>
    public void Save(AutoReplyConfig config)
    {
        lock (_lock)
        {
            string dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(config, Options);
            File.WriteAllText(_filePath, json);
        }
    }
}
