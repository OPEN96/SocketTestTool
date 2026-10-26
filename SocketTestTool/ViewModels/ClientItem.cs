using CommunityToolkit.Mvvm.ComponentModel;

namespace SocketTestTool.ViewModels;

/// <summary>已连接客户端列表项（TCP 服务端 / UDP 服务端模式下使用）</summary>
public partial class ClientItem : ObservableObject
{
    public ClientItem(string endPoint)
    {
        EndPoint = endPoint;
        ConnectedAt = DateTime.Now;
    }

    /// <summary>对端地址，如 127.0.0.1:5000</summary>
    public string EndPoint { get; }

    /// <summary>接入（或首次发现）时间</summary>
    public DateTime ConnectedAt { get; }

    public string ConnectedAtText => $"接入于 {ConnectedAt:HH:mm:ss}";
}
