# 项目上下文 (GEMINI.md)

> 本文件包含 ScreenTimeWin 项目的关键信息，用于辅助 AI Agent 理解项目上下文。

## 1. 项目概述

**ScreenTimeWin** 是一个基于 Windows 10/11 的屏幕时间管理应用，类似于 Apple 的 "屏幕使用时间"。旨在帮助用户追踪应用使用情况、设置每日限额、并通过专注模式提高效率。

### 主要功能
- **仪表盘**: 实时统计应用使用时长，展示分类饼图和趋势图。
- **限制规则**: 设置每日应用使用限额和宵禁模式。
- **专注模式**: 番茄钟功能，支持应用白名单。
- **周报**: 每周使用统计和趋势对比。
- **数据隐私**: 数据存储在本地 SQLite 数据库中。

## 2. 构建与运行

### 常用命令

| 动作 | 命令 | 说明 |
| :--- | :--- | :--- |
| **启动 (开发)** | `dev.bat` | 编译并运行 WPF 客户端 |
| **启动 (CLI)** | `dotnet run --project src/ScreenTimeWin.App` | 等同于 `dev.bat` |
| **冒烟测试** | `.\smoke_test.ps1` | 执行构建和单元测试 |
| **构建** | `dotnet build` | 编译整个解决方案 |
| **测试** | `dotnet test` | 运行所有单元测试 |

### 环境要求
- Windows 10 (x64) 或 Windows 11
- .NET 8 SDK

## 3. 技术栈与架构

### 核心技术
- **框架**: .NET 8 (WPF)
- **UI 库**: CommunityToolkit.Mvvm, LiveCharts2 (SkiaSharp)
- **数据库**: Entity Framework Core + SQLite
- **日志**: Serilog
- **其他**: Hardcodet.NotifyIcon (系统托盘), Microsoft.Extensions.Hosting (依赖注入)

### 目录结构 (`src/`)

- **ScreenTimeWin.App**: 主 WPF 应用程序。包含 UI (Views), 逻辑 (ViewModels), 和本地监控服务。
- **ScreenTimeWin.Service**: 后台服务（用于持久化任务，目前主要逻辑在 App 中）。
- **ScreenTimeWin.Core**: 核心领域模型 (Entities) 和接口定义。
- **ScreenTimeWin.Data**: 数据访问层 (DbContext, Repositories)。
- **ScreenTimeWin.IPC**: 进程间通信模块。
- **ScreenTimeWin.Tests**: 单元测试项目。

## 4. 开发规范

- **语言**: C# (最新版本特性)
- **UI 模式**: MVVM (Model-View-ViewModel)。
- **依赖注入**: 广泛使用构造函数注入。
- **异步编程**: 优先使用 `async/await`，避免阻塞 UI 线程。
- **资源管理**: 确保非托管资源（如 Windows 句柄）正确释放。
- **注释**: 关键逻辑和复杂算法需添加中文注释。

## 5. 关键文件/类说明

- **`src/ScreenTimeWin.App/App.xaml.cs`**: 应用程序入口点，配置依赖注入容器 (IServiceProvider)。
- **`src/ScreenTimeWin.App/Services/LocalAppMonitorService.cs`**: 核心监控逻辑，定期扫描前台窗口并统计时间。
- **`src/ScreenTimeWin.App/ViewModels/DashboardViewModel.cs`**: 仪表盘逻辑，负责聚合数据供图表显示。
- **`src/ScreenTimeWin.Data/ScreenTimeDbContext.cs`**: 数据库上下文，定义数据表结构。

## 6. 注意事项

- **权限**: 涉及进程终止的功能可能需要管理员权限。
- **本地数据**: 数据库文件位于 `%LocalAppData%\ScreenTimeWin\ScreenTimeWin.db`。
- **系统进程**: 开发监控逻辑时，注意过滤 Windows 系统进程 (Explorer, DWM 等)。
