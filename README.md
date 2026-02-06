# ScreenTimeWin

Windows 10/11 屏幕时间管理应用 (.NET 8 + WPF + SQLite)。
类似 Apple Screen Time，提供应用使用统计、每日限额、专注模式等功能。

## 🚀 快速启动

### 一键启动
双击 `start.bat` 即可运行应用。首次运行会自动编译项目。

### 开发模式
```bash
dev.bat
# 或
dotnet run --project src/ScreenTimeWin.App

# 运行冒烟测试 (构建 + 单元测试)
smoke_test.ps1
```

---

## ✨ 主要功能

### 1. 仪表盘 (Dashboard)
- **实时统计**: 自动追踪当前运行的应用，实时显示使用时长
- **App切换计数**: 统计应用切换次数
- **Top 应用**: 列出使用时间最长的应用
- **分类饼图**: 按类别（开发/办公/社交/娱乐）展示时间分布
- **每小时图表**: 可视化全天使用趋势

### 2. 应用使用详情 (App Usage)
- **应用列表**: 显示今日使用时长 vs 7日平均
- **详情弹窗**: 查看应用的最近会话和热门标题
- **快捷操作**: 设置每日限额、专注时拦截、始终允许

### 3. 限制规则 (Limits)
- **每日限额**: 设置应用每日最大使用分钟数
- **宵禁模式**: 设置禁止使用的时间段（如 22:00 - 06:00）
- **执行动作**: 仅通知 / 阻止启动 / 强制关闭

### 4. 专注模式 (Focus Mode)
- **番茄钟**: 自定义专注时长（默认 25 分钟）
- **倒计时进度**: 圆形进度条显示剩余时间
- **白名单**: 仅允许选定应用运行
- **勿扰开关**: 专注期间屏蔽通知

### 5. 周报 (Weekly Report)
- **周使用统计**: 查看本周总使用时长和每日分布
- **趋势对比**: 与上周对比变化百分比
- **分类细分**: 各类应用时间占比条形图

### 6. 系统设置
- **🎨 主题切换**: 深色/浅色模式
- **🌍 多语言**: 中文/英文
- **🛡️ PIN保护**: 设置安全码保护设置

---

## 🏗️ 技术架构

```
ScreenTimeWin/
├── src/
│   ├── ScreenTimeWin.App/        # WPF客户端 (MVVM + LiveCharts2)
│   │   ├── Services/
│   │   │   └── LocalAppMonitorService.cs  # 本地窗口监控服务
│   │   ├── ViewModels/           # 视图模型
│   │   └── Views/                # 视图
│   ├── ScreenTimeWin.Service/    # 后台服务（可选，用于持久化）
│   ├── ScreenTimeWin.Core/       # 领域实体
│   ├── ScreenTimeWin.Data/       # EF Core + SQLite
│   └── ScreenTimeWin.IPC/        # 进程间通信
├── start.bat                     # 一键启动
├── dev.bat                       # 开发模式启动
└── install.ps1                   # 服务安装脚本
```

### 核心组件

| 组件 | 说明 |
|------|------|
| **LocalAppMonitorService** | 本地实时窗口监控，每2秒扫描系统窗口 |
| **DashboardViewModel** | 仪表盘数据绑定，订阅监控事件自动刷新 |
| **NativeHelper** | Windows API封装，获取窗口/进程信息 |

### 监控原理

1. 应用启动时自动开始监控（`MonitorService.Start(2000)`）
2. 每2秒调用 `EnumWindows` API 枚举所有可见窗口
3. 追踪每个进程的总使用时长和前台时长
4. 自动过滤系统进程（explorer、dwm等）
5. 根据进程名自动识别应用分类
6. 通过事件通知UI实时刷新

---

## 📋 前置要求

- Windows 10 (x64) 或 Windows 11
- .NET 8 SDK 或 Runtime

---

## ⚠️ 注意事项

1. **权限**: "强制关闭"功能需要管理员权限
2. **数据存储**: 数据库位于 `%LocalAppData%\ScreenTimeWin\ScreenTimeWin.db`
3. **系统进程**: 自动过滤 explorer、dwm、csrss 等系统进程

---

## 📄 许可证

MIT License
