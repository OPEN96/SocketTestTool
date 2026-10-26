# Contributing / 贡献指南

[English](#english) | [中文](#中文)

---

## English

Thanks for your interest in contributing to SocketTestTool!

### Getting Started

1. Fork this repository and clone your fork.
2. Install **.NET SDK 10.0+**.
3. Build and run:
   ```bash
   dotnet restore
   dotnet run --project SocketTestTool
   ```

### Making Changes

- Create a feature branch: `git checkout -b feature/your-feature`.
- Follow the existing code style (MVVM with CommunityToolkit.Mvvm; async I/O; UI updates via `Dispatcher.UIThread`).
- Keep public APIs documented with XML comments.
- Make sure `dotnet build -c Release` succeeds with **0 warnings / 0 errors** before submitting.

### Submitting a Pull Request

1. Push your branch and open a PR against `main`.
2. Fill in the PR template — describe what and why.
3. CI must pass (Windows + Linux builds).
4. One logical change per PR keeps review fast.

### Reporting Bugs / Requesting Features

Use the [Issue templates](../../issues/new/choose) and include your platform, version, and reproduction steps. Logs from the app's built-in log export are very helpful.

---

## 中文

感谢你对 SocketTestTool 的关注与贡献！

### 开始

1. Fork 本仓库并克隆到本地。
2. 安装 **.NET SDK 10.0 或更高版本**。
3. 编译运行：
   ```bash
   dotnet restore
   dotnet run --project SocketTestTool
   ```

### 代码规范

- 新建特性分支：`git checkout -b feature/你的特性`。
- 遵循现有代码风格（MVVM + CommunityToolkit.Mvvm；网络 I/O 全部异步；UI 更新经 `Dispatcher.UIThread`）。
- 公共 API 请编写 XML 注释。
- 提交前确保 `dotnet build -c Release` **0 警告 0 错误**。

### 提交 Pull Request

1. 推送分支并向 `main` 发起 PR。
2. 按 PR 模板填写改动内容与原因。
3. CI（Windows + Linux 编译）必须通过。
4. 一个 PR 只做一件事，便于快速评审。

### 报告问题 / 提需求

请使用 [Issue 模板](../../issues/new/choose)，注明平台、版本与复现步骤；附上应用内「导出日志」的内容会非常有帮助。
