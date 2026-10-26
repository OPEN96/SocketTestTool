# SocketTestTool

> 跨平台 Socket 网络调试工具 · Cross-platform Socket testing tool


[![CI](https://github.com/OPEN96/SocketTestTool/actions/workflows/ci.yml/badge.svg)](https://github.com/OPEN96/SocketTestTool/actions/workflows/ci.yml)
[![Release](https://github.com/OPEN96/SocketTestTool/actions/workflows/release.yml/badge.svg)](https://github.com/OPEN96/SocketTestTool/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.0-8B44AC)](https://avaloniaui.net/)

[English](#english) | [中文](#中文)



---

## English

A cross-platform socket testing/debugging tool built with **.NET 10 + Avalonia 12**, running on Windows and Linux. Supports TCP/UDP, server/client modes, ASCII/HEX display, auto-reconnect and real-time timestamped logging.

### Features

- **Server & Client modes** — listen on any port, or connect to any host
- **TCP & UDP** protocols
- **Server mode**: connected client list, send to a selected client or broadcast to all (UDP peers are recorded on first packet)
- **Client mode**: TCP auto-reconnect with configurable retry (3 s interval)
- **ASCII / HEX** formats for both sending and display
- **Real-time log** with millisecond timestamps, direction and peer address
- **File logging**: stream logs to `logs/socket-log-<timestamp>.txt`, or export the current log view with one click

### Download

Pre-built single-file executables (no .NET runtime required) are available on the [Releases](../../releases) page:

| Platform | File |
|----------|------|
| Windows x64 | `SocketTestTool-win-x64.zip` |
| Linux x64 | `SocketTestTool-linux-x64.tar.gz` |
| Linux ARM64 | `SocketTestTool-linux-arm64.tar.gz` |

### Build from Source

Requires **.NET SDK 10.0+**:

```bash
git clone https://github.com/OPEN96/SocketTestTool.git
cd SocketTestTool
dotnet run --project SocketTestTool
```

Publish a self-contained single-file executable:

```bash
# Windows
dotnet publish SocketTestTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
# Linux
dotnet publish SocketTestTool -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

### Project Structure

```
├── SocketTestTool.slnx               # Solution
└── SocketTestTool/
    ├── Program.cs / App.axaml        # Entry point & app definition (Fluent theme)
    ├── Views/MainWindow.axaml        # UI: connection / client list / log / send panels
    ├── ViewModels/                   # MVVM (CommunityToolkit.Mvvm source generators)
    ├── Services/
    │   ├── TcpServerService.cs       # TCP listener, client registry, unicast/broadcast
    │   ├── TcpClientService.cs       # TCP client with disconnect events
    │   ├── UdpService.cs             # UDP server (peer tracking) / client
    │   └── LogFileWriter.cs          # Thread-safe append-only text log writer
    ├── Models/  Helpers/  Converters/
```

### CI/CD

- **ci.yml** — build verification on Windows + Linux for every push/PR to `main`
- **release.yml** — pushing a `v*` tag builds all platforms and publishes to GitHub Releases automatically



---

## 中文

基于 **.NET 10 + Avalonia 12** 的跨平台 Socket 网络调试工具，支持 Windows / Linux。涵盖 TCP/UDP、服务端/客户端、ASCII/HEX、断线重连与实时时间戳日志。

### 功能特性

- **服务端 / 客户端** 双模式切换
- 支持 **TCP / UDP** 两种协议
- **服务端模式**：监听指定端口，显示已连接客户端列表，支持单发选中客户端或广播全部（UDP 在收到首包后记录对端）
- **客户端模式**：TCP 断线自动重连（3 秒间隔，可开关）
- **ASCII / HEX** 发送与显示格式切换
- **实时日志**：毫秒级时间戳、收发方向、对端地址，自动滚动、一键清空
- **文本日志记录**：勾选「记录到文件」实时写入 `logs/socket-log-<时间戳>.txt`；「导出日志」一键保存当前日志

### 下载

无需安装 .NET 运行时的单文件可执行程序见 [Releases](../../releases) 页面：

| 平台 | 文件 |
|------|------|
| Windows x64 | `SocketTestTool-win-x64.zip` |
| Linux x64 | `SocketTestTool-linux-x64.tar.gz` |
| Linux ARM64（树莓派等） | `SocketTestTool-linux-arm64.tar.gz` |

### 从源码构建

需要 **.NET SDK 10.0 或更高版本**：

```bash
git clone https://github.com/OPEN96/SocketTestTool.git
cd SocketTestTool
dotnet run --project SocketTestTool
```

发布为免安装的单文件可执行程序：

```bash
# Windows
dotnet publish SocketTestTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
# Linux
dotnet publish SocketTestTool -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

### 使用说明

**作为服务端**：选择「服务端」与协议 → 填端口（地址默认 `0.0.0.0`）→ 「启动监听」→ 左侧列表选中客户端可单发，「发送至全部」广播。

**作为客户端**：选择「客户端」→ 填目标地址与端口 → 「连接」。TCP 模式可勾选「断线自动重连」。

**单机自测**：开两个窗口，一个监听 9000 端口，另一个连接 `127.0.0.1:9000` 即可互发。

**HEX 格式**：发送时按 `48 65 6C 6C 6F` 或 `48656C6C6F` 输入（空格可选）；日志区切到 HEX 后历史数据会重新以 HEX 呈现。

### 项目结构

```
├── SocketTestTool.slnx               # 解决方案
└── SocketTestTool/
    ├── Program.cs / App.axaml        # 入口与应用定义（Fluent 主题）
    ├── Views/MainWindow.axaml        # 界面：连接配置 / 客户端列表 / 日志区 / 发送区
    ├── ViewModels/                   # MVVM（CommunityToolkit.Mvvm 源生成器）
    ├── Services/
    │   ├── TcpServerService.cs       # TCP 监听、客户端管理、单发/广播
    │   ├── TcpClientService.cs       # TCP 客户端，断开事件
    │   ├── UdpService.cs             # UDP 服务端（对端记录）/ 客户端
    │   └── LogFileWriter.cs          # 线程安全的文本日志写入器
    ├── Models/  Helpers/  Converters/
```

### CI/CD

- **ci.yml**：推送/PR 到 `main` 时在 Windows + Linux 上编译验证
- **release.yml**：推送 `v*` tag 自动构建全部平台单文件并发布到 GitHub Releases

```bash
git tag v1.0.0
git push origin v1.0.0   # 触发自动发布
```


