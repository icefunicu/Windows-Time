# ScreenTimeWin STAR 项目亮点

## 项目概览

| 项目 | 说明 |
|------|------|
| **项目名称** | ScreenTimeWin - Windows 屏幕时间管理应用 |
| **技术栈** | .NET 8 / WPF / MVVM / SQLite / EF Core / LiveCharts2 |
| **角色/职责** | 全栈开发（前端UI + 后端服务 + 本地监控） |
| **项目周期** | 2026年2月 |
| **关键指标** | 6个视图组件、本地实时监控、每2秒刷新、一键启动 |

---

## STAR 条目

### 条目 1：本地实时应用监控系统

- **S（背景/场景）**：用户需要无需安装后台服务即可追踪应用使用时长，传统方案依赖独立后台进程，安装部署复杂。

- **T（任务/目标）**：实现轻量级本地监控服务，在App内部实时追踪所有运行中的应用，无需额外后台进程。

- **A（行动/方案）**：
  - 基于 Windows API（`EnumWindows`、`GetForegroundWindow`）实现窗口枚举
  - 创建 `LocalAppMonitorService` 单例服务，每2秒扫描一次系统窗口
  - 实现进程过滤机制，自动排除 explorer、dwm 等系统进程
  - 通过事件驱动（`AppsUpdated`）通知UI实时刷新
  - 自动提取应用图标并识别应用分类

- **R（结果/影响）**：
  - 双击 `start.bat` 即可一键启动，无需安装后台服务
  - 实时显示当前运行应用和使用时长
  - 界面每2秒自动刷新，用户体验流畅

- **证据索引**：
  - `src/ScreenTimeWin.App/Services/LocalAppMonitorService.cs`
  - `src/ScreenTimeWin.App/App.xaml.cs` (第64行启动监控)
  - `src/ScreenTimeWin.App/ViewModels/ViewModels.cs` (订阅事件刷新UI)

---

### 条目 2：现代化 WPF MVVM 架构设计

- **S（背景/场景）**：需要构建可维护、可扩展的桌面应用，同时满足现代UI设计要求。

- **T（任务/目标）**：采用 MVVM 架构模式，实现视图与逻辑分离，支持依赖注入和单元测试。

- **A（行动/方案）**：
  - 使用 CommunityToolkit.Mvvm 简化 MVVM 实现
  - 通过 `Microsoft.Extensions.DependencyInjection` 管理服务生命周期
  - 创建6个独立视图组件（Dashboard/AppUsage/Limits/Focus/WeeklyReport/TimeLimitDialog）
  - 实现多种值转换器（`BooleanToVisibility`、`PercentToWidth`、`FirstChar`等）
  - 使用 LiveCharts2 实现可视化图表（面积图、饼图、柱状图）

- **R（结果/影响）**：
  - 代码结构清晰，视图与业务逻辑完全分离
  - 支持热插拔视图切换，导航流畅
  - 3个单元测试全部通过，构建0错误

- **证据索引**：
  - `src/ScreenTimeWin.App/ViewModels/` (7个ViewModel)
  - `src/ScreenTimeWin.App/Views/` (多个XAML视图)
  - `src/ScreenTimeWin.App/Converters.cs`

---

### 条目 3：IPC 进程间通信架构

- **S（背景/场景）**：需要支持 UI 与后台服务之间的数据交互，实现数据持久化和策略执行。

- **T（任务/目标）**：设计基于 Named Pipes 的 IPC 通信机制，支持请求/响应模式。

- **A（行动/方案）**：
  - 创建 `ScreenTimeWin.IPC` 独立项目封装通信逻辑
  - 定义标准化 DTO 模型（`TodaySummaryResponse`、`LimitRuleDto` 等）
  - 实现 `IpcServer`/`IpcClient` 双向通信
  - 支持多种操作（获取摘要、设置限制、专注模式等）

- **R（结果/影响）**：
  - UI 与后台服务完全解耦
  - 支持独立部署和运行
  - 通信协议可扩展

- **证据索引**：
  - `src/ScreenTimeWin.IPC/` (IPC项目)
  - `src/ScreenTimeWin.IPC/Models/Dtos.cs`
  - `src/ScreenTimeWin.Service/IpcServer.cs`

---

## 技术难题与解决方案摘要

### 难题 1：无后台服务的实时监控

- **难题**：用户希望无需安装 Windows Service 即可追踪应用使用
- **解决方案**：在 WPF App 内创建 `LocalAppMonitorService`，使用 Timer 定期调用 Windows API
- **影响/收益**：简化部署，一键启动即用
- **证据索引**：`src/ScreenTimeWin.App/Services/LocalAppMonitorService.cs`

### 难题 2：XAML 命名空间和转换器管理

- **难题**：复杂的数据绑定需要多种转换器，且需要在 XAML 中正确引用
- **解决方案**：统一在 `App.xaml` 中定义全局资源，创建专用 `Converters.cs`
- **影响/收益**：减少重复代码，提高可维护性
- **证据索引**：`src/ScreenTimeWin.App/App.xaml`、`src/ScreenTimeWin.App/Converters.cs`

### 难题 3：图表数据实时更新

- **难题**：LiveCharts2 饼图和面积图需要实时反映监控数据变化
- **解决方案**：订阅 `AppsUpdated` 事件，使用 Dispatcher 在 UI 线程更新 Series 数据
- **影响/收益**：图表流畅刷新，无卡顿
- **证据索引**：`src/ScreenTimeWin.App/ViewModels/ViewModels.cs` (`OnMonitorDataUpdated`)

---

## 项目亮点摘要

| 亮点 | 影响/收益 | 证据索引 |
|------|----------|----------|
| **一键启动** | 双击 start.bat 即可运行，零配置 | `start.bat` |
| **实时监控** | 每2秒扫描，前台/后台应用均追踪 | `LocalAppMonitorService.cs` |
| **分类自动识别** | 根据进程名自动分类（开发/办公/社交/娱乐） | `LocalAppMonitorService.DetermineCategory()` |
| **图标自动提取** | 从 exe 文件提取应用图标显示 | `LocalAppMonitorService.ExtractIconBase64()` |
| **现代化UI** | 深色/浅色主题、渐变面积图、环形饼图 | `Views/*.xaml` |
| **模块化架构** | 5个独立项目，职责清晰 | `ScreenTimeWin.sln` |

---

## 待确认问题

- [ ] 性能指标：长时间运行的内存占用和CPU使用率
- [ ] 用户规模：目标用户群体和使用场景
- [ ] 数据持久化：是否需要历史数据存储到数据库
