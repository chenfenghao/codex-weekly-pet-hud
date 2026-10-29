using System.Windows.Automation;

namespace CodexPetLimitRings.Windows.Views;

internal sealed class TaskbarHudView : System.Windows.Controls.Border
{
    public OverlaySettings? PacingSettings { get; set; } = new();
    private readonly QuotaBadge _ring = new() { IsHitTestVisible = false };
    internal event Action? Clicked;
    internal event Action? SettingsRequested;

    internal TaskbarHudView()
    {
        Background = System.Windows.Media.Brushes.Transparent;
        Cursor = System.Windows.Input.Cursors.Hand;
        Child = _ring;
        MouseLeftButtonUp += (_, e) => { e.Handled = true; Clicked?.Invoke(); };
        MouseRightButtonUp += (_, e) => { e.Handled = true; SettingsRequested?.Invoke(); };
    }

    internal void Update(UsageSnapshot usage, string radarHeadline, string radarDetail, bool attention)
    {
        var now = DateTimeOffset.Now;
        var pace = WeeklyPacing.Calculate(usage.SecondaryRemaining, usage.SecondaryReset, now, PacingSettings);
        var targets = WeeklyTargets.Calculate(usage.SecondaryReset, now, PacingSettings);
        var fresh = usage.Source is "live" or "manual";
        var remainingText = usage.SecondaryRemaining is { } value ? UiText.F("剩余 {0:0.#}%", value) : UiText.T("剩余 —");
        var paceText = UiText.T(usage.Source == "none" ? "同步中" : usage.Source == "stale" ? "待更新" : pace.Status switch
        {
            "待确认重置" => "待重置", "刚刚开始" => "初始期", "检查时间" => "查时间", "等待数据" => "待数据", _ => pace.Status
        });
        var budgetText = QuotaVisualModel.BudgetText(fresh ? usage.SecondaryRemaining : null, targets);
        _ring.UpdateUsage(usage.SecondaryRemaining, fresh, targets);
        _ring.SetAttention(attention);
        ToolTip = $"{remainingText} · {paceText}\n{budgetText}\n{targets.Compact(fresh)}\n{pace.Prediction}\n{WeeklyPacing.Countdown(usage.SecondaryReset, now)}\n{radarHeadline}\n{radarDetail}\n{UiText.T("单击查看详情，右键打开设置。")}";
        ToolTip += "\n" + UiText.T("匀速：按自然时间计算此刻应剩；下班：按工作时段计算下班时应留。目标不随实际消耗改变。");
        ToolTip += "\n" + UiText.T("外圈 / 上方：剩余；内圈 / 下方：下班可用。负数表示低于目标，≈ 表示上次记录。");
        if (targets.ResetBeforeClose) ToolTip += "\n" + UiText.T("本周期在下班前重置，工作目标截止于重置时刻。");
        AutomationProperties.SetName(this, ToolTip.ToString());
    }
}
