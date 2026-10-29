# Windows 开发与使用

**简体中文** · [English](README.en.md)

本分支是 .NET 8 / WPF 托盘应用，主界面为真实 Codex Pet 旁或任务栏内的周额度圆环。操作说明见[主 README](../../README.md)。

## 构建与源码安装

需要 .NET 8 SDK、Windows x64 和 PowerShell。以下命令从仓库根目录执行：

```powershell
dotnet build platforms/windows/CodexPetLimitRings.Windows/CodexPetLimitRings.Windows.csproj -c Release
pwsh -File scripts/release/package-weekly.ps1 -Tag v1.6.3
```

可传入 `-DotnetPath C:\path\to\dotnet.exe` 使用自定义 SDK 路径。打包产物在 `dist`。

源码安装到 `%LOCALAPPDATA%\Programs\CodexWeeklyPetHud`：

```powershell
.\install.ps1
# 只有显式指定此参数才注册开机启动：
.\install.ps1 -AutoStart
.\uninstall.ps1
# 可选：同时删除本项目的数据目录
.\uninstall.ps1 -Data
```

安装需要 SDK，下载的便携版不需要。安装前请退出其他目录运行的挂件。

## 检查

先用主 README 的 publish 命令生成 `artifacts/windows-x64`：

```powershell
dotnet run --project platforms/windows/tests/LayoutTests/LayoutTests.csproj -c Release
$exe = (Resolve-Path artifacts/windows-x64/CodexWeeklyPetHud.exe).Path
Start-Process $exe -ArgumentList '--pace-self-test artifacts/qa/pace' -Wait
Start-Process $exe -ArgumentList '--reset-self-test artifacts/qa/reset' -Wait
Start-Process $exe -ArgumentList '--overlay-order-self-test artifacts/qa/overlay.txt' -Wait
Start-Process $exe -ArgumentList '--input-relay-self-test artifacts/qa/drag.json' -Wait
Start-Process $exe -ArgumentList '--language-self-test artifacts/qa/language' -Wait
```

自测参数在单实例检查前处理，不加载个人用量。窗口顺序和鼠标转发测试会短暂创建测试窗口，需要交互式 Windows 桌面，运行时不要操作这些窗口。

v1.0.0 本机验证覆盖：1120 个布局案例、174 个节奏/粘贴/界面检查、22 个重置信号策略检查、15 个窗口顺序与点击命中检查，以及拖拽消息转发。GitHub CI 运行布局和重置信号检查并编译便携包；原生桌面交互仍需本机验证。

## 代码位置

详情使用三个页签，刷新按钮固定在内容滚动区之外；雷达和更多指标可以展开。设置按常用、作息、外观分组，任务栏模式隐藏宠物外观页签，切换回来保留原设置。窗口默认大小分别为 560 × 600 和 520 × 620。`--language-self-test` 会遍历中英文的每个页签，生成 `compact-details-*` 和 `compact-settings-*` 截图，并检查刷新入口可见及设置保存行为。

`HudTheme.cs` 统一灰白底 #EFF1F5、蓝灰文字 #4C4F69 和紫色强调 #8839EF，XAML、图表和挂件共用冻结画刷。按钮与提示框样式位于 `App.xaml`。浅色界面的节奏、警告与错误文字使用对应的深色画刷，保留原有状态判断和图表图例。

中英文文案集中在 `UiText.cs`，静态 XAML 使用 `{local:Tr '中文原文'}` 绑定，格式化提示使用 `UiText.F`。内部状态和通知事件标识不随语言变化。`language` 设置保存为 `zh-CN` 或 `en`，旧配置默认中文。「恢复默认」保留语言选择。

| 文件 | 职责 |
| --- | --- |
| `CodexPetLimitRings.Windows/WeeklyPacing.cs` | 节奏算法、预测、粘贴解析 |
| `CodexPetLimitRings.Windows/MainController.cs` | 定时读取、Pet 跟随、托盘避让 |
| `CodexPetLimitRings.Windows/Services/UsageService.cs` | 个人用量读取 |
| `CodexPetLimitRings.Windows/Services/ResetSignalService.cs` | 公共信号解析、时效验证和去重 |
| `CodexPetLimitRings.Windows/Interop/NativeMethods.cs` | Win32 定位、输入转发、窗口层级 |
| `CodexPetLimitRings.Windows/Views/` | 状态条、详情与设置 |

Codex 的状态格式、窗口结构和用量接口可能变化。当前仅验证 Windows x64；其他平台源码保留自上游。系统托盘避让识别常见 Windows 10/11 托盘浮层、原生菜单和部分系统面板，其他第三方浮层依赖正常窗口顺序。

日志在 `%LOCALAPPDATA%\CodexWeeklyPetHud\Logs\runtime.log`。勿提交个人配置、日志、`auth.json` 或 token。自定义 Codex 数据目录使用 `CODEX_HOME`。

## 任务栏模式（v1.2.0）

`displayMode` 为 `pet`（默认，兼容旧配置）或 `taskbar`；`taskbarOffset` 为任务栏内向左移动的 DIP 距离，独立于宠物偏移。`TaskbarHudHost` 创建属于本进程的 `HwndSource` 分层子窗口并挂到主任务栏，按父窗口 DPI 计算位置，不向 Explorer 注入代码、不缩小任务列表或修改系统设置。模式切换销毁旧的任务栏子窗口、隐藏宠物输入代理，并保留数据、刷新间隔和语言。

Windows 11 的 XAML/DirectComposition 任务栏可能遮盖普通 WPF 子窗口的绘制表面，即使 `IsWindowVisible` 和点击命中均正常。设置 `HwndSourceParameters.UsesPerPixelTransparency = true` 使 WPF 创建并更新分层子窗口；不要改用旧的 `UsesPerPixelOpacity`，后者对 `WS_CHILD` 不生效。`app.manifest` 同时声明 Windows 10/11 的 `supportedOS`，否则分层子窗口创建可能失败。不能只设置原生 `WS_EX_LAYERED` 而不切换 WPF 的绘制方式。

在交互式 Windows 桌面运行 `CodexWeeklyPetHud.exe --taskbar-self-test <输出目录>`，检查真实父窗口、非置顶样式、点击命中、通知区域避让、位置调整、隐藏/重建、中英文渲染和设置切换。测试使用示例数据，不读取账户额度。实际屏幕截图同时匹配 WPF 参考图的深色额度弧线和浅色底，验证空白浅色/深色任务栏都不能误判通过；失败时也保留 `taskbar-live.*.png`。

两种模式共用 `QuotaBadge` 与 `QuotaRing`，不增加动画计时器。挂件内只有双层圆环与两行数字：外圈 / 上行为剩余，内圈 / 下行为下班可用。宠物挂件默认 100 × 40 DIP，任务栏约 90 × 36 DIP。负余量用负数与警示色表达，内圈不反向绕圈；延迟数据不显示可用预算。`--pace-self-test` 检查这些语义及 20–72 DIP / 100%–200% 缩放，并输出 `ring-preview.png`。

本次在 Windows 11 26H2（26300.9711、100% 缩放、主屏横向任务栏）复现旧版窗口可点击但文字不显示，修复后通过 38 项原生检查。其他 Windows 11 构建、缩放、Explorer 重启和自动隐藏行为仍需在各目标桌面补充验证；Windows 10 未在本次复测。主任务栏竖向布局或无法定位通知区域时仅保留托盘入口。
