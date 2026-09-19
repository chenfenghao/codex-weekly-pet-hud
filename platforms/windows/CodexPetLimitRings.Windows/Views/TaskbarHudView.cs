using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextBlock = System.Windows.Controls.TextBlock;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace CodexPetLimitRings.Windows.Views;

internal sealed class TaskbarHudView : System.Windows.Controls.Border
{
    public OverlaySettings? PacingSettings { get; set; } = new();
    private readonly TextBlock _remaining = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.WhiteSmoke };
    private readonly TextBlock _pace = new() { FontSize = 10, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
    private readonly TextBlock _target = new() { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(188, 208, 196)) };
    private readonly TextBlock _radar = new() { Text = "●", FontSize = 9, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
    internal event Action? Clicked;
    internal event Action? SettingsRequested;

    internal TaskbarHudView()
    {
        foreach (var text in new[] { _remaining, _pace, _target, _radar })
            text.FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
        Background = new SolidColorBrush(Color.FromRgb(24, 32, 27));
        CornerRadius = new CornerRadius(5);
        Cursor = System.Windows.Input.Cursors.Hand;
        var grid = new Grid { Width = 178, Height = 30 };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(17) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(13) });
        grid.Children.Add(_remaining); grid.Children.Add(_pace);
        Grid.SetRow(_target, 1); grid.Children.Add(_target);
        Grid.SetRow(_radar, 1); grid.Children.Add(_radar);
        Child = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(6, 2, 6, 2), Child = grid };
        MouseLeftButtonUp += (_, e) => { e.Handled = true; Clicked?.Invoke(); };
        MouseRightButtonUp += (_, e) => { e.Handled = true; SettingsRequested?.Invoke(); };
    }

    internal void Update(UsageSnapshot usage, string radarHeadline, string radarDetail, bool attention)
    {
        var now = DateTimeOffset.Now;
        var pace = WeeklyPacing.Calculate(usage.SecondaryRemaining, usage.SecondaryReset, now, PacingSettings);
        var targets = WeeklyTargets.Calculate(usage.SecondaryReset, now, PacingSettings);
        var fresh = usage.Source is "live" or "manual";
        _remaining.Text = usage.SecondaryRemaining is { } value ? UiText.F("剩余 {0:0}%", value) : UiText.T("剩余 —");
        _pace.Text = UiText.T(usage.Source == "none" ? "同步中" : usage.Source == "stale" ? "待更新" : pace.Status switch
        {
            "待确认重置" => "待重置", "刚刚开始" => "初始期", "检查时间" => "查时间", "等待数据" => "待数据", _ => pace.Status
        });
        _pace.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(fresh ? pace.Color : "#9AADA1")!;
        _target.Text = targets.Compact(fresh);
        _radar.Foreground = attention ? Brushes.Gold : Brushes.DarkSeaGreen;
        ToolTip = $"{_remaining.Text} · {_pace.Text}\n{_target.Text}\n{pace.Prediction}\n{WeeklyPacing.Countdown(usage.SecondaryReset, DateTimeOffset.Now)}\n{radarHeadline}\n{radarDetail}\n{UiText.T("单击查看详情，右键打开设置。")}";
        ToolTip += "\n" + UiText.T("匀速：按自然时间计算此刻应剩；下班：按工作时段计算下班时应留。目标不随实际消耗改变。");
        if (targets.ResetBeforeClose) ToolTip += "\n" + UiText.T("本周期在下班前重置，工作目标截止于重置时刻。");
        AutomationProperties.SetName(this, ToolTip.ToString());
    }
}
