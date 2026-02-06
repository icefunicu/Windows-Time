# AGENTS.md

> 本文件由仓库扫描自动生成，默认覆盖旧内容。面向自动化 Agent 与人工协作者。

## 1. 目标与适用范围
- 保持改动可回滚、可验证、可审计。
- 适用于所有协作者：自动化 Agent、开发者、审阅者。
- 高风险或不确定需求先缩小范围，再执行。

## 2. 仓库画像（扫描结果）
- 项目标识：`ScreenTimeWin`
- 包管理器：`未识别`
- 主要语言：C# (203), JSON (34), HTML (9)
- 技术栈：.NET
- 仓库形态：`single-repo`
- 生态证据：
  - .NET: `ScreenTimeWin.sln`, `src/ScreenTimeWin.App/ScreenTimeWin.App.csproj`, `src/ScreenTimeWin.Core/ScreenTimeWin.Core.csproj`
- 关键路径：
  - `README.md`
  - `src`
  - `ScreenTimeWin.sln`
  - `src/ScreenTimeWin.App/ScreenTimeWin.App.csproj`
  - `src/ScreenTimeWin.Core/ScreenTimeWin.Core.csproj`
- CI 流程：未识别
- 部署关注点：
  - 未检测到明确部署目标，发布前请补充部署平台和验收命令。
  - 未检测到 CI 配置，建议补充自动化检查以降低回归风险。

## 3. 协作原则
- 先读后改：先确认边界、影响范围、回滚点。
- 小步提交：单次改动聚焦单一问题，禁止无关重构混入。
- 优先复用：优先使用现有组件、脚本、约定。
- 禁止猜测：结论必须有命令输出或测试结果支撑。

## 4. 实施规范
- 保持命名、目录结构、错误处理风格一致。
- 边界输入做校验（空值、类型、范围、格式）。
- 避免明显性能风险（N+1、无界循环、全量扫描）。
- 日志禁止输出敏感信息，禁止提交密钥和令牌。

## 5. 安全红线
- 禁止执行破坏性操作（清库、覆盖生产配置、强推受保护分支）。
- 禁止执行来源不明脚本或未审计下载命令。
- 涉及权限、支付、用户数据时先写威胁模型与防护措施。

## 6. 质量门禁
- 按顺序执行以下命令并确保成功：
  1. `dotnet build`
  2. `dotnet test`
  3. 或者直接运行冒烟测试：`smoke_test.ps1`
- 命令来源：`.NET`, `PowerShell`

## 7. 提交与评审
- Commit message 使用 Conventional Commits：`feat|fix|refactor|docs|test|chore|ci`。
- 提交说明必须包含 What、Why、How to verify（命令 + 预期结果）。
- 评审优先检查行为回归、边界条件、测试充分性、部署兼容性。

## 8. Agent 执行清单
开始前：
- 明确需求边界、影响文件、回滚方案。
- 标记是否影响构建、测试、部署、外部接口。
修改中：
- 每修完一个问题立即做最小验证，不累计风险。
- 发现异常改动时立即暂停并与用户确认。
结束前：
- 汇总改动文件、验证命令、验证结果。
- 明确说明构建、测试、部署基线是否通过。
- 列出剩余风险和可选后续优化。
