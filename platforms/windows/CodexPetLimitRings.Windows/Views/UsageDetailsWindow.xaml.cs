using System.Windows;
using System.Windows.Media;
using System.Globalization;

namespace CodexPetLimitRings.Windows.Views;

public partial class UsageDetailsWindow : Window
{
    public OverlaySettings? PacingSettings { get; set; } = new();
    private bool _allowClose;
    private string? _lastLanguage;
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? RadarRefreshRequested;
    public event Action<UsageSnapshot>? UsageImported;
    public UsageDetailsWindow()
    {
        InitializeComponent();
        System.Windows.DataObject.AddPastingHandler(PasteInput, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText))
            {
                var text = e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) as string ?? "";
                e.CancelCommand(); PasteInput.Text = text; Import(text);
            }
        });
    }
    public void Update(UsageSnapshot usage, bool refreshing)
    {
        var pace = WeeklyPacing.Calculate(usage.SecondaryRemaining, usage.SecondaryReset, DateTimeOffset.Now, PacingSettings);
        var targets = WeeklyTargets.Calculate(usage.SecondaryReset, DateTimeOffset.Now, PacingSettings);
        var fresh = usage.Source is "live" or "manual";
        EvenTargetText.Text = QuotaTargets.Percent(fresh ? targets.EvenRemaining : null);
        CloseTargetText.Text = QuotaTargets.Percent(fresh ? targets.CloseRemaining : null);
        CloseTargetLabel.Text = UiText.T(targets.DayOff ? "休息日应剩" : "下班应剩");
        WorkDeadlineText.Text = targets.Deadline is { } deadline
            ? UiText.F("工作目标截止：{0}", UiText.Date(deadline.LocalDateTime))
            : UiText.T("设置有效作息和重置时间后显示下班目标。");
        if (targets.ResetBeforeClose) WorkDeadlineText.Text += "\n" + UiText.T("本周期在下班前重置，工作目标截止于重置时刻。");
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(pace.Color)!;
        RemainingText.Text = usage.SecondaryRemaining is { } remaining ? $"{remaining:0.#}%" : "—%";
        StatusText.Text = UiText.T(pace.Status); StatusText.Foreground = brush; UsedBar.Foreground = brush;
        UsedText.Text = usage.SecondaryUsed is { } used ? $"{used:0.#}%" : "—"; UsedBar.Value = usage.SecondaryUsed ?? 0;
        TimeText.Text = pace.TimePercent is { } time ? $"{time:0.0}%" : "—"; TimeBar.Value = pace.TimePercent ?? 0;
        RatioText.Text = pace.Ratio is { } ratio ? $"{ratio:0.00} ×" : "—";
        var work = PacingSettings?.WorkHoursEnabled == true;
        TimeLabel.Text = UiText.T(work ? "计划工作时间已过" : "时间已过");
        BudgetLabel.Text = UiText.T(work ? "今日剩余可用" : "后续每日上限");
        BudgetText.Text = pace.DailyBudget is { } budget ? UiText.F(work ? "≤ {0:0.#}% 今日" : "≤ {0:0.#}% /天", Math.Floor(budget * 10) / 10) : "—";
        PacingExplanation.Text = UiText.T("匀速应剩＝剩余自然时间÷7天；下班应剩＝下班后剩余工作时长÷本周期全部工作时长。两者都是计划参考值，不随实际余额改变。");
        PredictionText.Text = pace.Prediction; PredictionText.Foreground = brush;
        CountdownText.Text = UiText.T("距离重置  ") + WeeklyPacing.Countdown(usage.SecondaryReset, DateTimeOffset.Now);
        try { ResetText.Text = usage.SecondaryReset is { } reset ? DateTimeOffset.FromUnixTimeSeconds(reset).LocalDateTime.ToString(UiText.IsEnglish ? "yyyy MMM d, ddd HH:mm" : "yyyy年M月d日 ddd HH:mm", UiText.Culture) : UiText.T("本机时区"); }
        catch (ArgumentOutOfRangeException) { ResetText.Text = UiText.T("时间无效"); }
        SourceText.Text = usage.Source switch { "manual" => UiText.F("粘贴记录 · {0:M/d HH:mm}", usage.ReadAt.LocalDateTime), "live" => UiText.F("自动读取 · {0:M/d HH:mm}", usage.ReadAt.LocalDateTime), "stale" => UiText.T("更新延迟 · 显示上次记录"), _ => UiText.T("等待录入 · 打开 Codex Pet 后挂件随宠物显示") };
        RefreshButton.IsEnabled = !refreshing; RefreshButton.Content = UiText.T(refreshing ? "读取中…" : "自动读取额度");
        PasteInput.ToolTip = UiText.T("重置时间：2026年9月19日 16:12\n剩余 80%");
    }
    private void Import(string text)
    {
        try
        {
            var usage = WeeklyPasteParser.Parse(text, DateTimeOffset.Now);
            UsageImported?.Invoke(usage); Update(usage, false);
            MessageText.Foreground = System.Windows.Media.Brushes.LightGreen;
            MessageText.Text = UiText.F("已保存：剩余 {0:0.#}%，已用 {1:0.#}%。", usage.SecondaryRemaining, usage.SecondaryUsed);
        }
        catch (FormatException e) { MessageText.Foreground = System.Windows.Media.Brushes.LightCoral; MessageText.Text = e.Message; }
    }
    public void UpdateResetSignal(string headline, string detail, bool attention, bool refreshing)
    {
        RadarHeadline.Text = headline; RadarDetail.Text = detail;
        RadarHeadline.Foreground = attention ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.DarkSeaGreen;
        RadarRefresh.IsEnabled = !refreshing; RadarRefresh.Content = UiText.T(refreshing ? "检查中…" : "检查信号");
    }
    private void RadarRefresh_OnClick(object sender, RoutedEventArgs e) => RadarRefreshRequested?.Invoke();
    private void RadarSource_OnClick(object sender, RoutedEventArgs e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://codex-reset.com/zh/") { UseShellExecute = true });
    private void Paste_OnClick(object sender, RoutedEventArgs e)
    {
        try { PasteInput.Text = System.Windows.Clipboard.GetText(); Import(PasteInput.Text); }
        catch (System.Runtime.InteropServices.ExternalException) { MessageText.Text = UiText.T("剪贴板暂时不可用，请在输入框按 Ctrl+V。"); }
    }
    private void Import_OnClick(object sender, RoutedEventArgs e) => Import(PasteInput.Text);
    private void RefreshButton_OnClick(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();
    private void Settings_OnClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    public void ClosePermanently() { _allowClose = true; Close(); }
    public void ApplyLanguage()
    {
        if (_lastLanguage == UiText.Instance.Language) return;
        _lastLanguage = UiText.Instance.Language;
        MessageText.Text = "";
        Width = UiText.IsEnglish ? 410 : 370;
    }
    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } }
}
