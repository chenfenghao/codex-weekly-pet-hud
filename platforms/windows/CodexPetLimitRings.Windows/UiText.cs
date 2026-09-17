using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace CodexPetLimitRings.Windows;

public sealed class UiText : INotifyPropertyChanged
{
    public static UiText Instance { get; } = new();
    public string Language { get; private set; } = "zh-CN";
    public static bool IsEnglish => Instance.Language == "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "zh-CN");
    public event PropertyChangedEventHandler? PropertyChanged;
    public static void SetLanguage(string language)
    {
        var normalized = language == "en" ? "en" : "zh-CN";
        if (Instance.Language == normalized) return;
        Instance.Language = normalized;
        Instance.PropertyChanged?.Invoke(Instance, new(nameof(Language)));
    }
    public static string T(string text) => IsEnglish && English.TryGetValue(text, out var value) ? value : text;
    public static string F(string text, params object?[] args) => string.Format(Culture, T(text), args);
    public static string Date(DateTime date) => date.ToString(IsEnglish ? "MMM d, ddd HH:mm" : "M月d日 ddd HH:mm", Culture);
    internal static IReadOnlyDictionary<string, string> Translations => English;
    private static readonly Dictionary<string, string> English = new()
    {
        ["显示模式"] = "Display mode",
        ["跟随 Codex 宠物"] = "Follow Codex Pet",
        ["任务栏（无需打开宠物）"] = "Taskbar (Pet not required)",
        ["在主屏幕任务栏空白处显示两行额度。单击查看详情，右键打开设置。"] = "Two quota lines in free space on the primary taskbar. Click for details; right-click for Settings.",
        ["向左移动（避开应用图标）"] = "Move left (avoid app icons)",
        ["默认在通知区域左侧；不会挤开任务栏图标。如有重叠，请调整位置或切回宠物模式。"] = "Starts left of the notification area; does not push app icons aside. If they overlap, adjust the position or switch to Pet mode.",
        ["任务栏正在连接"] = "Connecting to taskbar",
        ["任务栏布局暂不支持，请使用宠物模式"] = "Unsupported taskbar; use Pet mode",
        ["任务栏空间不足，请使用宠物模式"] = "Taskbar too small; use Pet mode",
        ["任务栏显示"] = "Taskbar display",
        ["单击查看详情，右键打开设置。"] = "Click for details; right-click for Settings.",
        ["剩余 {0:0}%"] = "Left {0:0}%",
        ["周额度 · 挂件设置"] = "Weekly quota · Settings",
        ["恢复默认"] = "Reset defaults",
        ["Codex 周额度"] = "Codex weekly quota",
        ["修改后立即生效，自动保存"] = "Changes apply instantly and are saved",
        ["语言 / Language"] = "Language / 语言",
        ["自动更新"] = "Auto refresh",
        ["读取额度的间隔"] = "Refresh interval",
        ["1–60 分钟可调，默认 5 分钟。倒计时持续更新；点击「自动读取额度」可立即刷新。"] = "1–60 minutes; default 5. The countdown stays live. Use Refresh quota for an immediate update.",
        ["监控 codex-reset.com 重置信号"] = "Monitor reset signals from codex-reset.com",
        ["新信号通过 Windows 通知提醒"] = "Send Windows notifications for new signals",
        ["沿用上方间隔；宠物隐藏时仍检查信号。首次连接只记录历史，同一事件不重复提醒。"] = "Uses the interval above, even when the Pet is hidden. First sync records history silently; repeated states are not notified again.",
        ["位置"] = "Position",
        ["状态条跟随 Codex 宠物，靠近屏幕边缘时自动换侧。"] = "Follows your Pet and moves aside at screen edges.",
        ["相对宠物的位置"] = "Placement relative to Pet",
        ["左侧"] = "Left", ["右侧"] = "Right", ["上方"] = "Above", ["下方"] = "Below",
        ["水平偏移"] = "Horizontal offset", ["垂直偏移"] = "Vertical offset",
        ["归位"] = "Center", ["向左 5 像素"] = "Move left 5 px", ["向右 5 像素"] = "Move right 5 px",
        ["向上 5 像素"] = "Move up 5 px", ["向下 5 像素"] = "Move down 5 px",
        ["大小与间距"] = "Size and spacing", ["默认 190 × 72，可继续缩放。"] = "Default: 190 × 72. Adjust the scale below.",
        ["状态条大小"] = "HUD scale", ["与宠物的间距"] = "Gap from Pet", ["每 {0:0} 分钟"] = "Every {0:0} min",
        ["周额度 · 节奏伴侣"] = "Weekly quota · Pace companion", ["设置"] = "Settings", ["本周节奏"] = "Weekly pace",
        ["剩余可用额度"] = "Quota remaining", ["额度已用"] = "Quota used", ["时间已过"] = "Time elapsed",
        ["使用速率"] = "Usage pace", ["后续每日上限"] = "Daily budget", ["≤ {0:0.#}% /天"] = "≤ {0:0.#}% /day",
        ["距离重置  "] = "Resets in  ", ["本机时区"] = "Local time zone", ["时间无效"] = "Invalid time",
        ["查看来源 ↗"] = "View source ↗", ["检查信号"] = "Check signals", ["检查中…"] = "Checking…",
        ["社区公告与信号，不代表你的账户额度已重置。"] = "Community signals do not confirm a reset for your account.",
        ["📋 剪贴板"] = "📋 Clipboard", ["或在下方 Ctrl+V"] = "Or paste below with Ctrl+V",
        ["识别并保存"] = "Import & save", ["自动读取额度"] = "Refresh quota", ["读取中…"] = "Refreshing…",
        ["重置时间：2026年9月19日 16:12\n剩余 80%"] = "Reset time: 2026-09-19 16:12\nRemaining 80%",
        ["应剩＝100%−本周时间进度；建议每日上限＝剩余额度按剩余天数均摊。点击上方「设置」调整自动读取间隔，默认 5 分钟；手动粘贴后暂停自动覆盖。"] = "Target = 100% minus elapsed week. Daily budget divides your remaining quota by days left. Settings adjusts the 5-minute refresh interval. Manual imports pause automatic quota updates.",
        ["粘贴记录 · {0:M/d HH:mm}"] = "Manual import · {0:MMM d HH:mm}", ["自动读取 · {0:M/d HH:mm}"] = "Auto refresh · {0:MMM d HH:mm}",
        ["更新延迟 · 显示上次记录"] = "Update delayed · Showing last record",
        ["等待录入 · 打开 Codex Pet 后挂件随宠物显示"] = "Waiting for data · Open Codex Pet to show the HUD",
        ["已保存：剩余 {0:0.#}%，已用 {1:0.#}%。"] = "Saved: {0:0.#}% remaining, {1:0.#}% used.",
        ["剪贴板暂时不可用，请在输入框按 Ctrl+V。"] = "Clipboard unavailable. Paste into the input box with Ctrl+V.",
        ["等待数据"] = "No data", ["检查时间"] = "Check time", ["待确认重置"] = "Verify reset", ["已耗尽"] = "Empty",
        ["刚刚开始"] = "Starting", ["偏慢"] = "Slow", ["稳健"] = "On track", ["偏快"] = "Fast", ["超速"] = "Too fast",
        ["同步中"] = "Syncing", ["待更新"] = "Stale", ["待重置"] = "Verify", ["初始期"] = "Starting", ["查时间"] = "Check time", ["待数据"] = "No data",
        ["粘贴周额度与重置时间，开始记录。"] = "Paste your weekly quota and reset time to get started.",
        ["重置时间无效。"] = "Invalid reset time.", ["重置时间已到，请重新粘贴最新额度。"] = "Reset time reached. Refresh or import the latest quota.",
        ["重置时间超过 7 天，暂停预测。"] = "Reset is more than 7 days away. Prediction paused.",
        ["额度已耗尽，等待重置后更新。"] = "Quota exhausted. Refresh after the reset.",
        ["周期刚开始，满 1 分钟后估算使用速率。"] = "The cycle just started. Pace estimates begin after one minute.",
        ["按当前平均速度，可用到重置日。"] = "At your average pace, quota should last until reset.",
        ["预计 {0} 耗尽\n比重置提前 {1:0.#} 小时"] = "Estimated exhaustion: {0}\n{1:0.#} hours before reset",
        ["时间已到，请更新"] = "Reset time reached; refresh quota", ["{0}天 {1:00}:{2:00}:{3:00}"] = "{0}d {1:00}:{2:00}:{3:00}", ["无效时间"] = "Invalid time",
        ["请只粘贴周额度与重置时间。"] = "Paste only your weekly quota and reset time.",
        ["未识别到百分比，例如：剩余 80%。"] = "No percentage found. Try: Remaining 80%.",
        ["百分比必须在 0–100 之间。"] = "Percentage must be between 0 and 100.",
        ["存在多个不同额度，请只复制周额度那一段。"] = "Conflicting percentages. Copy only the weekly quota section.",
        ["日期或时间无效，请检查后重试。"] = "Invalid date or time. Check and try again.",
        ["倒计时无效。"] = "Invalid countdown.",
        ["请同时粘贴重置时间，例如：2026年9月19日 16:12。"] = "Include a reset time, such as 2026-09-19 16:12.",
        ["重置时间应在未来 7 天内。"] = "Reset time must be within the next 7 days.",
        ["周额度节奏伴侣"] = "Weekly quota pace companion", ["剩余 {0:0.#}%"] = "Left {0:0.#}%", ["剩余 —"] = "Left —",
        ["应剩 {0} · 建议 {1}/天"] = "Target {0} · {1}/day",
        ["应剩 — · 建议 — /天"] = "Target — · — /day",
        ["粘贴记录 · 不自动覆盖"] = "Manual import · Auto refresh paused", ["等待最新数据"] = "Waiting for fresh data",
        ["周额度剩余 {0:0.#}% · {1}\n匀速使用此刻应剩 {2}；每日建议 {3}\n速率 {4:0.00}× · {5}\n{6}\n{7}"] = "Weekly quota left: {0:0.#}% · {1}\nTarget remaining: {2}; daily budget: {3}\nPace {4:0.00}× · {5}\n{6}\n{7}",
        ["重置雷达 · 已关闭"] = "Reset radar · Off", ["重置雷达 · 连接延迟"] = "Reset radar · Delayed", ["重置雷达 · 连接中"] = "Reset radar · Connecting",
        ["雷达 · 新重置信号"] = "Radar · New reset signal", ["雷达 · 新储备重置公告"] = "Radar · New banked reset", ["雷达 · 新重置公告"] = "Radar · New reset notice",
        ["雷达 · 有重置信号"] = "Radar · Reset signal active", ["重置雷达 · 暂无新信号"] = "Reset radar · No new signal",
        ["等待 codex-reset.com 公开 API 数据。"] = "Waiting for public API data from codex-reset.com.",
        ["最近检查 {0:M/d HH:mm} · 每 {1} 分钟\n上次全局重置公告：{2}"] = "Checked {0:MMM d HH:mm} · Every {1} min\nLast global reset notice: {2}",
        ["暂无记录"] = "No record", ["\n连接或数据延迟，保留上次记录，暂停新信号提醒。"] = "\nConnection or data delayed. Keeping the last record; new signal alerts paused.",
        ["新的重置信号"] = "New reset signal", ["新的储备重置公告"] = "New banked reset announcement", ["社区已确认重置公告"] = "Community-confirmed reset",
        ["新的重置公告（待核实）"] = "New reset announcement (unverified)",
        ["发现 {0} 条新的重置动态"] = "{0} new reset updates",
        ["codex-reset.com · {0:M/d HH:mm}\n点击查看。社区公告不代表你的账户已到账。"] = "codex-reset.com · {0:MMM d HH:mm}\nClick to view. This does not confirm a reset for your account.",
        ["查看周额度详情"] = "Weekly quota details", ["立即刷新"] = "Refresh now", ["挂件设置（更新间隔 / 外观）…"] = "Settings (refresh / appearance)…",
        ["重置挂件位置"] = "Reset HUD position", ["退出"] = "Exit",
        ["Codex 周额度 · 请打开 Codex 宠物"] = "Codex weekly quota · Open Codex Pet",
        [" · 更新延迟"] = " · Stale", ["Codex 周额度 · 剩余 {0}{1}"] = "Codex weekly quota · {0} left{1}",
    };
}

// Binding keeps static XAML labels live when the saved language changes.
public sealed class TrExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new System.Windows.Data.Binding(nameof(UiText.Language))
    {
        Source = UiText.Instance, Converter = TranslationConverter.Instance, ConverterParameter = text
    }.ProvideValue(serviceProvider);
    private sealed class TranslationConverter : IValueConverter
    {
        public static TranslationConverter Instance { get; } = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => UiText.T((string)parameter);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
