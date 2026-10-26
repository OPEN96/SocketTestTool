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
- **Bidirectional relay (port forwarding)**: listen on a local port and transparently forward data in both directions to a target host, with every relayed packet logged
- **Timed heartbeat**: send a configurable payload at a fixed interval to keep connections alive or monitor liveness
- **Fixed commands (presets)**: save frequently used commands with a name, then send them with one click; saved commands support edit, delete and reorder, persisted locally as JSON
- **ASCII table**: a built-in lookup window covering 0-127 standard ASCII and 128-255 extended (Latin-1), with DEC/HEX/OCT/BIN columns, keyword search and range filters; double-click a row to append it to the send box in the current format
- **Base64 codec**: encode text or files to Base64, decode Base64 back to text, export the result as a binary file, read a local binary file into Base64, and preview Base64 images (including `data:image/...;base64,` URIs)

### Download

Pre-built single-file executables (no .NET runtime required) are available on the [Releases](../../releases) page. Asset filenames include the version tag, e.g. `SocketTestTool-v1.0.0-win-x64.zip`:

| Platform | File |
|----------|------|
| Windows x64 | `SocketTestTool-<version>-win-x64.zip` |
| Linux x64 | `SocketTestTool-<version>-linux-x64.tar.gz` |
| Linux ARM64 | `SocketTestTool-<version>-linux-arm64.tar.gz` |

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
    │   ├── RelayService.cs           # Bidirectional relay / port forwarding
    │   ├── FixedCommandStore.cs       # Fixed-command persistence (JSON)
    │   └── LogFileWriter.cs          # Thread-safe append-only text log writer
    ├── Models/  Helpers/  Converters/
```

### Bidirectional Relay (Port Forwarding)

The relay feature lets the tool act as a transparent man-in-the-middle: it listens on a local port, connects to a target host, and forwards data in both directions unchanged. Every relayed packet is logged with direction (A→B / B→A).

**Use cases** — protocol debugging, port mapping, transparent proxying, and observing traffic between a client and server without modifying either side.

**How to use**

1. In the "双向透传 / Bidirectional Relay" panel, fill in the local **listen port**, the **target host** and **target port**.
2. Click "启动透传 / Start relay".
3. Point your client at `127.0.0.1:<listen port>`; the tool connects to the target and starts forwarding.

**Notes**

- Relay is independent of the main connection panel — you can run a relay alongside a TCP/UDP test session, or use it alone.
- Each downstream connection opens its own forwarding session; data is passed through verbatim (no parsing).
- The relay is TCP-only. Forwarded data appears in the log as the "转发 / relay" direction.
- Stopping the relay or closing the window tears down all active sessions.

### Fixed Commands (Presets)

Save frequently used payloads (e.g. "query status", "heartbeat", hex register reads) once, then send them with a single click instead of retyping each time.

**How to use**

1. Click the "固定指令 / Fixed commands" header to expand the panel (it is collapsed by default to keep the main UI compact).
2. Fill in a **name** and **content**, tick **HEX** if the content is hexadecimal, then click **保存 / Save**.
3. Select a command in the list and click **发送 / Send** to send it immediately (same targeting rules as the main send panel: selected client, or broadcast in server mode).
4. Use **编辑 / Edit**, **删除 / Delete**, **上移 / Up**, **下移 / Down** to manage the list.

**Notes**

- Commands are persisted to `data/fixed-commands.json` next to the executable and reloaded on startup.
- A command stores its own ASCII/HEX flag, so each preset is sent in the format you saved it with.
- Sending requires an active connection; otherwise a warning is logged.

### ASCII Table

A read-only reference window for looking up character codes while crafting binary or text payloads.

**How to use**

1. Click the **ASCII 码表 / ASCII table** button at the top-right of the send panel, or press **Ctrl+K**.
2. Type in the search box to filter by decimal, hex, octal, binary, the character itself, its abbreviation (e.g. `CR`) or the Chinese description (e.g. `换行`).
3. Narrow the list with the range combo: all / control chars / printable chars / extended chars.
4. Double-click a row (or select it and click **插入发送框 / Insert**) to append it to the send box — in HEX mode as two hex digits (`41`), in ASCII mode as the character itself (`A`).
5. **复制 HEX / 复制 DEC / 复制字符** copy the selected value to the clipboard.

**Notes**

- The window is non-modal and single-instance: clicking the button again just brings it to the front.
- Control characters show their abbreviation (NUL, LF, CR, DEL …) instead of a glyph, plus the common escape form (`\n`, `\r`, `\t`, `\0`).
- Rows 128-159 are C1 control characters in Latin-1 and have no visible glyph.

### Base64 Codec

A standalone window for Base64 conversion and binary file handling.

**How to use**

1. Click **Base64 工具 / Base64 tool** at the top-right of the send panel, or press **Ctrl+B**.
2. **Encode**: type on the left — the Base64 result appears on the right automatically (or click **编码 →**).
3. **Decode**: paste Base64 on the right — the text appears on the left automatically (or click **← 解码**).
4. **File → Base64**: click **从文件读取** and pick any file; its bytes are converted to Base64.
5. **Base64 → file**: click **导出为二进制文件** to save the current bytes. Images get the correct extension automatically (`image.png`, `image.jpg`, …).
6. **Image preview**: when the decoded bytes are a PNG/JPEG/GIF/BMP/WEBP/ICO/TIFF image it is rendered in the preview area, together with format, byte count and pixel size. Both raw Base64 and `data:image/png;base64,...` URIs work.
7. **复制 / Copy** buttons put either side on the clipboard.

**Notes**

- Text encoding is selectable: UTF-8 (default), ASCII, Latin-1, UTF-16, UTF-32. If decoding produces replacement characters, a warning tells you to switch encoding.
- **Invalid Base64** is reported explicitly: illegal characters, or a length that is not a multiple of 4 (missing `=` padding / truncated).
- **Size limits**: 20 MB per file/Base64 payload (larger input is rejected with a clear message), and image preview is skipped above 8 MB while export still works.
- Turn off **输入时实时转换** to stop live conversion and use the direction buttons manually — useful for very large content.

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
- **双向透传（端口转发）**：监听本地端口，将收到的连接与数据双向原样转发到目标地址，每一条转发数据都记录日志
- **定时心跳**：按固定间隔自动发送可配置内容，用于保活或连通性探测
- **固定指令**：保存常用指令（带名称），点击一键发送；支持编辑、删除、排序管理，本地 JSON 持久化
- **ASCII 码表**：独立查询窗口，覆盖 0-127 标准 ASCII 与 128-255 扩展（Latin-1），提供 DEC/HEX/OCT/BIN 对照、关键字搜索与范围筛选，双击即可按当前格式插入发送框
- **Base64 编解码**：文本/文件与 Base64 互转、导出二进制文件、读取本地文件转 Base64，并自动识别预览 Base64 图片（支持 data URI）

### 下载

无需安装 .NET 运行时的单文件可执行程序见 [Releases](../../releases) 页面，产物文件名包含版本号（tag），如 `SocketTestTool-v1.0.0-win-x64.zip`：

| 平台 | 文件 |
|------|------|
| Windows x64 | `SocketTestTool-<版本号>-win-x64.zip` |
| Linux x64 | `SocketTestTool-<版本号>-linux-x64.tar.gz` |
| Linux ARM64（树莓派等） | `SocketTestTool-<版本号>-linux-arm64.tar.gz` |

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

**定时心跳**：勾选发送区下方的「定时心跳」，填间隔秒数与心跳内容（跟随上方 ASCII/HEX 格式），即可按周期自动发送；服务端模式下未选中客户端时自动广播到全部对端。主连接断开或停止时心跳自动停止。

**固定指令**：在「固定指令」区填「名称」和「内容」，若内容为十六进制则勾选「HEX」，点「保存」即可入库；在列表中选中某条指令后点「发送」一键发送（发送对象规则与主发送区一致：服务端选中则单发、否则广播）。用「编辑」「删除」「上移」「下移」管理指令列表。

### 双向透传（端口转发）

透传功能让工具充当「透明中间人」：监听本地端口，连到目标地址，把两边数据原样双向转发，且每一笔转发都以「转发」方向记录日志。

**功能用途**

- 协议调试 / 抓包观察：不修改客户端与服务端，即可看到两者之间实际交互的数据
- 端口映射 / 透明代理：把对某个本地端口的访问转发到另一台主机的端口
- 中间人测试：在真实链路中间插入观察点，验证数据流

**使用方式**

1. 在「双向透传」区填写本地**监听端口**、**目标 IP/域名** 与 **目标端口**
2. 点击「启动透传」
3. 让客户端连接 `127.0.0.1:<监听端口>`，工具会代为连到目标地址并开始双向转发

**注意事项**

- 透传与上方主连接区相互独立：可与 TCP/UDP 测试会话同时运行，也可单独使用
- 每个下游连接会建立一条独立转发会话，数据原样透传（不做任何解析），互不阻塞
- 透传仅支持 TCP 协议；转发数据在日志中显示为「转发」方向，格式跟随当前 ASCII/HEX 显示设置
- 停止透传或关闭窗口会断开所有进行中的转发会话
- 若透传监听端口已被占用，会在日志区给出错误提示，请更换端口后重试

### 固定指令

把常用的发送内容（如「查询状态」「心跳」「寄存器读取」等）保存一次，之后点击即可一键发送，无需每次重复输入。

**功能用途**

- 高频重复发送：调试协议时反复发送同一命令，保存后一点即发
- 指令库管理：常用指令集中保存，带名称便于识别，支持排序整理

**使用方式**

1. 点击「固定指令」标题行展开面板（默认折叠，避免占用主界面空间）
2. 填写**名称**与**内容**，若内容为十六进制则勾选 **HEX**，点「保存」
3. 在列表中选中某条指令，点「发送」即可立即发送（发送对象规则与主发送区一致：服务端选中客户端则单发、否则广播到全部）
4. 用「编辑」「删除」「上移」「下移」按钮管理指令列表

折叠时标题行会显示已保存的指令条数（如「3 条」），点击标题行任意位置即可展开/收起。

**注意事项**

- 指令持久化保存到程序同级的 `data/fixed-commands.json`，重启后自动加载
- 每条指令单独保存自己的 ASCII/HEX 标记，发送时按保存时的格式解析
- 发送需要当前已建立连接，未连接时会在日志区给出提示

### ASCII 码表

调试文本/二进制报文时快速查询字符与进制对应关系的只读工具，独立窗口，不占用主界面空间。

**功能用途**

- 进制对照：一眼看到某个字符的十进制、十六进制、八进制与二进制表示
- 控制字符辨识：`\r` `\n` `\t` `\0` 等不可见字符的名称、缩写与转义写法
- 快速组包：查到目标字符后直接插入发送框，免去手工换算

**使用方式**

1. 点击发送区右上角的「ASCII 码表」按钮，或按 **Ctrl+K** 打开窗口
2. 在搜索框输入关键字，支持十进制、十六进制、八进制、二进制、字符本体、缩写（如 `CR`）或中文说明（如 `换行`）
3. 用「范围」下拉框收窄：全部 / 控制字符 / 可打印字符 / 扩展字符
4. 双击某一行（或选中后点「插入发送框」）即可追加到发送内容：
   HEX 模式下追加两位十六进制（`41`），ASCII 模式下追加字符本体（`A`）
5. 「复制 HEX」「复制 DEC」「复制字符」把选中值复制到剪贴板

**注意事项**

- 窗口为非模态、单实例：再次点击按钮只会把它提到最前，不会重复打开
- 控制字符不显示字形，改以缩写（NUL / LF / CR / DEL 等）标识，并给出常见转义写法
- 128-159 在 Latin-1 中是 C1 控制字符，没有可见字形
- 打开后搜索框自动聚焦，可直接输入查询；列表内按 Enter 插入、Esc 关闭窗口

### Base64 编解码

独立的 Base64 转换与二进制文件处理窗口，不占用主界面空间。

**功能用途**

- 快速把文本或任意文件转成 Base64，便于在报文中嵌入
- 把收到的 Base64 还原成文本或另存为原始二进制文件
- 直接在工具里预览 Base64 图片（接口返回的 data URI 可直接粘贴查看）

**使用方式**

1. 点击发送区右上角的「Base64 工具」按钮，或按 **Ctrl+B** 打开窗口
2. **编码**：在左侧输入文本，右侧实时生成 Base64（也可点「编码 →」手动执行）
3. **解码**：在右侧粘贴 Base64，左侧实时还原文本（也可点「← 解码」手动执行）
4. **文件 → Base64**：点「从文件读取」选择本地文件，字节内容即转为 Base64
5. **Base64 → 文件**：点「导出为二进制文件」把当前数据另存为文件；若识别为图片会自动给出正确扩展名（`image.png`、`image.jpg` 等）
6. **图片预览**：解码后的数据若为 PNG/JPEG/GIF/BMP/WEBP/ICO/TIFF，会在下方预览区直接显示，并标注格式、字节数与像素尺寸；纯 Base64 与 `data:image/png;base64,...` 形式的 URI 都支持
7. 两侧各有「复制」按钮，一键复制到剪贴板

**注意事项**

- 文字编码可切换：UTF-8（默认）、ASCII、Latin-1、UTF-16、UTF-32；若解码出现替换字符（乱码），会提示更换编码
- **非法 Base64** 会明确提示：包含非 Base64 字符，或长度不是 4 的倍数（缺 `=` 补位／被截断）
- **体积限制**：单文件与单次 Base64 上限 20 MB，超出会明确拒绝并提示拆分；图片预览上限 8 MB，超出时跳过预览但仍可正常导出
- 内容较大时可取消勾选「输入时实时转换」，改用中间的方向按钮手动执行

### 项目结构

```
├── SocketTestTool.slnx               # 解决方案
└── SocketTestTool/
    ├── Program.cs / App.axaml        # 入口与应用定义（Fluent 主题）
    ├── Views/MainWindow.axaml        # 界面：连接配置 / 客户端列表 / 日志区 / 发送区
    ├── Views/AsciiTableWindow.axaml  # ASCII 码查询表（独立窗口，可插入发送框）
    ├── Views/Base64Window.axaml      # Base64 编解码 / 文件转换 / 图片预览（独立窗口）
    ├── Helpers/ImageFormatHelper.cs  # 依魔数识别 PNG/JPEG/GIF/BMP/WEBP/ICO/TIFF
    ├── ViewModels/                   # MVVM（CommunityToolkit.Mvvm 源生成器）
    ├── Services/
    │   ├── TcpServerService.cs       # TCP 监听、客户端管理、单发/广播
    │   ├── TcpClientService.cs       # TCP 客户端，断开事件
    │   ├── UdpService.cs             # UDP 服务端（对端记录）/ 客户端
    │   ├── RelayService.cs           # 双向透传 / 端口转发
    │   ├── FixedCommandStore.cs       # 固定指令持久化（JSON）
    │   ├── AsciiTable.cs              # ASCII 码表数据（0-127 标准 + 128-255 扩展）
    │   └── (Base64 转换逻辑见 ViewModels/Base64ViewModel.cs)
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


