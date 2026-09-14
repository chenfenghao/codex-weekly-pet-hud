using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CodexPetLimitRings.Windows.Interop;
using InputMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CodexPetLimitRings.Windows.Views;

public partial class PotionWindow : Window
{

    private readonly string _accessibleLabel;
    private double _appliedScale = double.NaN;
    private bool _pointerPressed;
    private bool _pointerMoved;
    private ScreenPointer _pressPointer;
    public event Action? PotionClicked;
    internal event Action<ScreenPointer>? PointerPressed;
    internal event Action<ScreenPointer>? PointerMoved;
    internal event Action<ScreenPointer>? PointerReleased;
    internal event Action? PointerCancelled;

    public PotionWindow(string label, System.Windows.Media.Color dark, System.Windows.Media.Color mid, System.Windows.Media.Color bright, System.Windows.Media.Color surface)
    {
        InitializeComponent();
        _accessibleLabel = "周额度节奏伴侣";
        AutomationProperties.SetName(this, UiText.T(_accessibleLabel));
        AutomationProperties.SetName(Root, UiText.T(_accessibleLabel));
        Root.IsHitTestVisible = true;
        SourceInitialized += (_, _) =>
        {
            NativeMethods.ConfigurePotionInput(this, directPotionClicksEnabled: true);
        };
        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += OnLostMouseCapture;
    }

    public void UpdateUsage(double? remaining, long? resetAt, string source)
    {
        var pace = WeeklyPacing.Calculate(remaining, resetAt, DateTimeOffset.Now);
        var fresh = source is "live" or "manual";
        PaceText.Text = UiText.T(source == "none" ? "同步中" : source == "stale" ? "待更新" : pace.Status switch
        {
            "待确认重置" => "待重置", "刚刚开始" => "初始期", "检查时间" => "查时间", "等待数据" => "待数据", _ => pace.Status
        });
        RemainingText.Text = remaining is { } value ? UiText.F("剩余 {0:0.#}%", value) : UiText.T("剩余 —");
        var ideal = fresh && pace.TimePercent is { } time && pace.DailyBudget is not null ? $"{100 - time:0}%" : "—";
        var daily = fresh && pace.DailyBudget is { } budget ? $"≤{Math.Floor(budget * 10) / 10:0.#}%" : "—";
        IdealText.Text = UiText.F("应剩 {0} · 建议 {1}/天", ideal, daily);
        var countdown = WeeklyPacing.Countdown(resetAt, DateTimeOffset.Now);
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(fresh ? pace.Color : "#9AADA1")!;
        StatusDot.Fill = brush; PaceText.Foreground = brush;
        Capsule.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, brush.Color.R, brush.Color.G, brush.Color.B));
        var sourceText = UiText.T(source == "manual" ? "粘贴记录 · 不自动覆盖" : source == "live" ? "自动更新" : "等待最新数据");
        Root.ToolTip = UiText.F("周额度剩余 {0:0.#}% · {1}\n匀速使用此刻应剩 {2}；每日建议 {3}\n速率 {4:0.00}× · {5}\n{6}\n{7}", remaining, UiText.T(pace.Status), ideal, daily, pace.Ratio, countdown, pace.Prediction, sourceText);
        AutomationProperties.SetName(this, $"{UiText.T(_accessibleLabel)}: {Root.ToolTip}");
        AutomationProperties.SetName(Root, $"{UiText.T(_accessibleLabel)}: {Root.ToolTip}");
    }

    public void ApplyScale(double scale)
    {
        if (double.IsFinite(_appliedScale) && Math.Abs(_appliedScale - scale) < 0.0001) return;
        _appliedScale = scale;
        Width = 190 * scale; Height = 72 * scale;
    }

    public void UpdateResetSignal(string headline, string detail, bool attention)
    {
        ResetSignalText.Text = headline;
        ResetSignalText.Foreground = attention ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.DarkSeaGreen;
        ResetSignalText.ToolTip = detail;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Left) return;
        eventArgs.Handled = true;
        _pointerPressed = true;
        _pointerMoved = false;
        _pressPointer = ToScreenPointer(eventArgs);
        Mouse.Capture(this, CaptureMode.Element);
        PointerPressed?.Invoke(_pressPointer);
    }

    private void OnMouseMove(object sender, InputMouseEventArgs eventArgs)
    {
        if (!_pointerPressed) return;
        eventArgs.Handled = true;
        var pointer = ToScreenPointer(eventArgs);
        _pointerMoved |= UnifiedDragPolicy.IsDrag(_pressPointer, pointer);
        PointerMoved?.Invoke(pointer);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!_pointerPressed || eventArgs.ChangedButton != MouseButton.Left) return;
        eventArgs.Handled = true;
        var pointer = ToScreenPointer(eventArgs);
        var clicked = !_pointerMoved;
        _pointerPressed = false;
        PointerReleased?.Invoke(pointer);
        if (IsMouseCaptured) Mouse.Capture(null);
        if (clicked) PotionClicked?.Invoke();
    }

    private void OnLostMouseCapture(object sender, InputMouseEventArgs eventArgs)
    {
        if (!_pointerPressed) return;
        _pointerPressed = false;
        PointerCancelled?.Invoke();
    }

    private ScreenPointer ToScreenPointer(InputMouseEventArgs eventArgs)
    {
        var point = PointToScreen(eventArgs.GetPosition(this));
        return new ScreenPointer(point.X, point.Y);
    }
}
