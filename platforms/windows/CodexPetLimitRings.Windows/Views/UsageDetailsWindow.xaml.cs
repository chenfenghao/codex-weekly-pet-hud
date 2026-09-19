using System.Windows;
using System.Windows.Media;
using System.Globalization;

namespace CodexPetLimitRings.Windows.Views;

public partial class UsageDetailsWindow : Window
{
    public OverlaySettings? PacingSettings { get; set; }
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
        PacingExplanation.Text = UiText.T(work ? "按本机时区和工作时段计算应剩，下班与休息日暂停推进。今日建议仅包含今天午夜前剩余工作时段；临时加班消耗仍会计入。修改作息会重新计算整个周期。" : "应剩＝100%−本周时间进度；建议每日上限＝剩余额度按剩余天数均摊。点击上方「设置」调整自动读取间隔，默认 5 分钟；手动粘贴后暂停自动覆盖。");
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
