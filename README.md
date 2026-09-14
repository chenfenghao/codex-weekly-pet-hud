# Codex Weekly Pet HUD · 周额度节奏挂件

放在 **Codex Pet 上方**的轻量 Windows 状态条：看剩余额度、判断使用快慢，并接收社区重置信号提醒。

[下载 Windows x64 便携版](https://github.com/chenfenghao/codex-weekly-pet-hud/releases/latest) · [使用与构建](platforms/windows/README.md) · [更新记录](CHANGELOG.md) · [MIT 许可证](LICENSE)

<p>
  <img src="docs/images/capsule.png" alt="剩余额度、当前节奏、应剩比例、每日建议和重置雷达" width="285">
  <img src="docs/images/radar-alert.png" alt="新重置信号高亮第三行" width="285">
  <img width="331" height="283" alt="image" src="https://github.com/user-attachments/assets/b05a28ce-4096-4125-9ea2-27dc9cf1b601" />

</p>

*以上为程序渲染的示例数据，不是实时账户用量或实时公告。宠物由 Codex 提供，本版不另行打包 Codex Pet 角色文件。*

## 功能

- **三行状态条**：默认约 190 × 72 DIP，可缩放；显示剩余、节奏、匀速应剩、每日建议和重置雷达。
- **自动读取周额度**：默认每 5 分钟更新，设置中可调为 1–60 分钟，也可立即刷新。
- **跟随真实 Pet**：从宠物或状态条拖动；支持位置、偏移和间距调整，屏幕边缘自动避让。
- **不挡系统托盘**：托盘面板和受支持的系统菜单展开时，状态条与透明拖拽层暂时隐藏，关闭后恢复；不再循环抢到其他置顶窗口前面。
- **中文粘贴导入**：识别「重置时间 + 剩余百分比」，也支持已用百分比和相对倒计时。
- **使用节奏与预测**：比较时间流逝和额度消耗，显示偏慢、稳健、偏快或超速，并估算耗尽时间。
- **社区重置雷达**：新强信号、重置公告和明确的储备重置发放公告可高亮并触发 Windows 通知。
- **本地保存**：保存设置、手动记录、最近用量及通知去重状态。便携版不注册开机启动。

## 下载与启动

1. 在 [Releases](https://github.com/chenfenghao/codex-weekly-pet-hud/releases/latest) 下载 `Codex-Weekly-Pet-HUD-v1.0.0-Windows-x64.zip`。
2. **完整解压**到固定目录，保留 EXE 旁边的 DLL 文件。
3. 双击 `CodexWeeklyPetHud.exe`，然后在 Codex 中打开 Pet。
4. 单击状态条查看详情，点「设置」调整间隔、位置、大小和提醒；系统托盘菜单也能进入设置或退出。

系统要求：Windows 10/11 x64、已登录且支持 Pet 的 Codex 桌面端。便携包自带 .NET 8 运行时，无需另装 .NET。本版本未做代码签名，Windows 可能显示未知发布者提示，请确认下载来源和校验值。

升级时先通过托盘退出旧版，再解压新版；设置保存在用户数据目录，不因替换程序文件丢失。卸载便携版时退出并删除解压目录即可。

```powershell
Get-FileHash .\Codex-Weekly-Pet-HUD-v1.0.0-Windows-x64.zip -Algorithm SHA256
```

将结果与同一 Release 的 `SHA256SUMS.txt` 对照。

## 看懂「剩余」与「应剩」

| 项目 | 含义 |
| --- | --- |
| 剩余 | Codex 返回的周额度剩余比例，或最近一次粘贴记录 |
| 应剩 | 按 7 天均匀使用，此刻理想的剩余比例；是计算值 |
| 建议 ≤ x%/天 | 将当前剩余额度按距离重置的天数均摊 |
| 速率 R | 本周期已用比例 ÷ 本周期已过时间比例 |

| 速率 | 显示 |
| --- | --- |
| R < 0.85 | 偏慢，余量充足 |
| 0.85 ≤ R ≤ 1.0 | 稳健 |
| 1.0 < R ≤ 1.3 | 偏快 |
| R > 1.3 | 超速 |

例如一周已过 40%、已用 60%，则 R = 1.5，显示「超速」；实际剩余 40%，匀速应剩 60%。预测以本周期平均消耗速率不变为前提，不是最近一分钟的瞬时速率。

本版按固定 **7 天窗口**计算。逐笔恢复的滚动额度只能近似估算；重置时间过期时提示待确认，不会擅自把额度清零或补满。

## 粘贴导入

在详情中点击「剪贴板」，或在输入框粘贴：

```text
重置时间：2026年9月19日 16:12
剩余 80%
```

识别为已用 20%。其他支持的格式包括：

```text
2026-09-19 16:12
已用 20%
```

```text
剩余 80% 3天5小时
```

```text
已用 25% 6d 07:37:30
```

绝对时间按本机时区解释，需处于未来 7 天内；请把示例日期换成实际重置时间。手动导入切换到手动模式；点击「自动读取额度」并成功读取后切回自动模式。Ctrl+V 只在本应用输入框中生效，不安装全局键盘监听。

## 重置雷达与通知

使用 [codex-reset.com](https://codex-reset.com/zh/) 的公开接口：

- `https://codex-reset.com/api/forecast`
- `https://codex-reset.com/api/feed`

雷达与额度读取共用可调间隔，默认 5 分钟。**只要挂件进程在运行，Pet 隐藏后仍继续检查雷达**；Pet 隐藏时暂停账户用量读取。退出程序后两者均停止。

首次成功读取只建立历史基线，不补发旧公告。新信号显示在第三行，并可触发 Windows 系统托盘通知。点击通知打开详情，可继续查看来源。同一事件的同一状态不重复通知，待核实升级为已确认时可再次提醒。普通动态、弱暗示及纯模型概率变化不会触发提醒。

网络失败或上游数据过期时显示「连接延迟」，保留最近记录并暂停新信号通知。可分别关闭雷达或系统通知；通知能否弹出还取决于 Windows 的通知设置和勿扰状态。

**社区公告不等于个人账户已到账。** 雷达不会修改账户剩余值，也不会兑换重置次数；个人额度继续独立读取。

## 数据与隐私

| 数据或接口 | 用途 |
| --- | --- |
| `%USERPROFILE%\.codex\.codex-global-state.json` | 定位可见 Pet；支持 `CODEX_HOME` |
| Codex 数据目录的 `auth.json` | 在内存中读取现有登录凭据，向 ChatGPT 用量接口发请求；不记录 token |
| `https://chatgpt.com/backend-api/wham/usage` | 读取周额度；是可能变化的客户端内部接口 |
| codex-reset.com 的两个公开接口 | 社区信号；独立客户端，不发送 OpenAI 凭据或账户用量 |
| `%LOCALAPPDATA%\CodexWeeklyPetHud` | 设置、最近用量、手动记录、提醒去重和诊断日志 |

发布包不包含用户配置、Codex 登录文件、个人用量缓存或本机日志。日志会记录用量摘要和错误类型，分享前请检查个人信息，不要上传 `auth.json`。

公开接口访问失败时，可使用本机已安装 Python 的标准 `urllib` 请求作为兼容方式，沿用系统网络配置。程序不会自动安装 Python；没有 Python 且直接访问失败时，雷达会显示连接延迟。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 和 PowerShell。当前发布目标为 Windows x64。

```powershell
git clone https://github.com/chenfenghao/codex-weekly-pet-hud.git
cd codex-weekly-pet-hud
pwsh -File scripts/release/package-weekly.ps1 -Tag v1.0.0
```

产物在 `dist`，包含便携 ZIP 与 SHA-256 校验文件。打包脚本只读取构建输出和项目文档，不读取用户数据目录。也可直接构建：

```powershell
dotnet publish platforms/windows/CodexPetLimitRings.Windows/CodexPetLimitRings.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/windows-x64
```

测试命令与源码安装见 [Windows 开发说明](platforms/windows/README.md)。

## 常见问题

**启动后看不到挂件？** 检查系统托盘，再打开 Codex Pet。Pet 关闭、系统托盘面板展开或无法匹配宠物窗口时会隐藏。不要同时运行两个版本。

**额度一直是「待更新」？** 确认 Codex 已登录，点击「自动读取额度」重试；仍失败可粘贴导入。客户端接口或登录存储方式变更可能需要更新程序。

**雷达连接延迟？** 检查网站、系统代理和已安装的 Python。接口受限或数据过期时，程序不会把失败当成「没有信号」。

**为什么不直接用 HTML？** 本版需要定位真实 Pet、桌面置顶、鼠标转发和本地登录读取，因此采用原生 WPF。单个网页无法完成这些桌面集成能力。

**支持 macOS / ARM64 吗？** 此分支仅发布和验证 Windows x64。保留的上游其他平台源码供参考，未同步这套周额度状态条和重置雷达功能。

## 来源与许可

基于 [himomohi/codex-pet-hud](https://github.com/himomohi/codex-pet-hud) 改造，保留上游 Git 历史与 MIT 版权声明。主要改造为中文周额度状态条、节奏计算、粘贴输入、可调刷新、重置雷达及托盘重叠修复。

项目遵循 [MIT License](LICENSE)。本项目与 OpenAI、Codex Reset 均无隶属关系。第三方服务内容与角色素材的权利归各自权利人所有。
