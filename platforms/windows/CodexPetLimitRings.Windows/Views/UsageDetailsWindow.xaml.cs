using System.Windows;
using System.Windows.Media;
using System.Globalization;

namespace CodexPetLimitRings.Windows.Views;

public partial class UsageDetailsWindow : Window
{
    private bool _allowClose;
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
        var pace = WeeklyPacing.Calculate(usage.SecondaryRemaining, usage.SecondaryReset, DateTimeOffset.Now);
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(pace.Color)!;
        RemainingText.Text = usage.SecondaryRemaining is { } remaining ? $"{remaining:0.#}%" : "—%";
        StatusText.Text = pace.Status; StatusText.Foreground = brush; UsedBar.Foreground = brush;
        UsedText.Text = usage.SecondaryUsed is { } used ? $"{used:0.#}%" : "—"; UsedBar.Value = usage.SecondaryUsed ?? 0;
        TimeText.Text = pace.TimePercent is { } time ? $"{time:0.0}%" : "—"; TimeBar.Value = pace.TimePercent ?? 0;
        RatioText.Text = pace.Ratio is { } ratio ? $"{ratio:0.00} ×" : "—";
        BudgetText.Text = pace.DailyBudget is { } budget ? $"≤ {Math.Floor(budget * 10) / 10:0.#}% /天" : "—";
        PredictionText.Text = pace.Prediction; PredictionText.Foreground = brush;
        CountdownText.Text = "距离重置  " + WeeklyPacing.Countdown(usage.SecondaryReset, DateTimeOffset.Now);
        try { ResetText.Text = usage.SecondaryReset is { } reset ? DateTimeOffset.FromUnixTimeSeconds(reset).LocalDateTime.ToString("yyyy年M月d日 ddd HH:mm", CultureInfo.GetCultureInfo("zh-CN")) : "本机时区"; }
        catch (ArgumentOutOfRangeException) { ResetText.Text = "时间无效"; }
        SourceText.Text = usage.Source switch { "manual" => $"粘贴记录 · {usage.ReadAt.LocalDateTime:M/d HH:mm}", "live" => $"自动读取 · {usage.ReadAt.LocalDateTime:M/d HH:mm}", "stale" => "更新延迟 · 显示上次记录", _ => "等待录入 · 打开 Codex Pet 后挂件随宠物显示" };
        RefreshButton.IsEnabled = !refreshing; RefreshButton.Content = refreshing ? "读取中…" : "自动读取额度";
    }
    private void Import(string text)
    {
        try
        {
            var usage = WeeklyPasteParser.Parse(text, DateTimeOffset.Now);
            UsageImported?.Invoke(usage); Update(usage, false);
            MessageText.Foreground = System.Windows.Media.Brushes.LightGreen;
            MessageText.Text = $"已保存：剩余 {usage.SecondaryRemaining:0.#}%，已用 {usage.SecondaryUsed:0.#}%。";
        }
        catch (FormatException e) { MessageText.Foreground = System.Windows.Media.Brushes.LightCoral; MessageText.Text = e.Message; }
    }
    public void UpdateResetSignal(string headline, string detail, bool attention, bool refreshing)
    {
        RadarHeadline.Text = headline; RadarDetail.Text = detail;
        RadarHeadline.Foreground = attention ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.DarkSeaGreen;
        RadarRefresh.IsEnabled = !refreshing; RadarRefresh.Content = refreshing ? "检查中…" : "检查信号";
    }
    private void RadarRefresh_OnClick(object sender, RoutedEventArgs e) => RadarRefreshRequested?.Invoke();
    private void RadarSource_OnClick(object sender, RoutedEventArgs e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://codex-reset.com/zh/") { UseShellExecute = true });
    private void Paste_OnClick(object sender, RoutedEventArgs e)
    {
        try { PasteInput.Text = System.Windows.Clipboard.GetText(); Import(PasteInput.Text); }
        catch (System.Runtime.InteropServices.ExternalException) { MessageText.Text = "剪贴板暂时不可用，请在输入框按 Ctrl+V。"; }
    }
    private void Import_OnClick(object sender, RoutedEventArgs e) => Import(PasteInput.Text);
    private void RefreshButton_OnClick(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();
    private void Settings_OnClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    public void ClosePermanently() { _allowClose = true; Close(); }
    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } }
}
