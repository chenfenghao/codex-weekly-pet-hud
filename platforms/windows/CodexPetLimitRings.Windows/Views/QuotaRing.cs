using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace CodexPetLimitRings.Windows.Views;

// One vector surface for Pet and taskbar modes. No animation timer or bitmap assets.
public sealed class QuotaRing : FrameworkElement
{
    private const double Center = 20;
    private const double Radius = 14;
    private static readonly Brush Surface = HudTheme.Canvas;
    private static readonly Brush Diamond = MakeBrush(156, 160, 176);
    private static readonly Brush Alert = HudTheme.Warning;
    private static readonly Pen Track = MakePen(HudTheme.Track, 1.8);
    private static readonly Pen Progress = MakePen(HudTheme.Accent, 1.8);
    private static readonly Pen StaleProgress = MakePen(MakeBrush(156, 160, 176), 1.8);
    private static readonly Pen BudgetTrack = MakePen(HudTheme.Track, 1.5);
    private static readonly Pen BudgetProgress = MakePen(HudTheme.ChartBlue, 1.5);
    private static readonly Pen EmptyDiamond = MakePen(Diamond, 1);
    private static readonly Pen AlertOutline = MakePen(Surface, 1.5);
    private double? _remaining;
    private bool _fresh;
    private bool _attention;
    private Geometry? _arc;
    private double? _available;
    private Geometry? _budgetArc;

    public void UpdateUsage(double? remaining, bool fresh)
    {
        var value = remaining is { } number && double.IsFinite(number) ? Math.Clamp(number, 0, 100) : (double?)null;
        fresh &= value is not null;
        if (_remaining == value && _fresh == fresh) return;
        _remaining = value;
        _fresh = fresh;
        _arc = CreateArc(value, Radius);
        InvalidateVisual();
    }

    public void UpdateBudget(double? available)
    {
        var value = available is { } number && double.IsFinite(number) ? Math.Clamp(number, 0, 100) : (double?)null;
        if (_available == value) return;
        _available = value;
        _budgetArc = CreateArc(value, 10);
        InvalidateVisual();
    }

    private static Geometry? CreateArc(double? value, double radius)
    {
        if (value is > 0 and < 100)
        {
            var angle = value.Value / 100 * Math.Tau;
            var geometry = new StreamGeometry();
            using (var drawing = geometry.Open())
            {
                drawing.BeginFigure(new Point(Center, Center - radius), false, false);
                drawing.ArcTo(new Point(Center + radius * Math.Sin(angle), Center - radius * Math.Cos(angle)),
                    new Size(radius, radius), 0, value > 50, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            return geometry;
        }
        return null;
    }

    public void SetAttention(bool attention)
    {
        if (_attention == attention) return;
        _attention = attention;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var diameter = Math.Min(HudLayout.RingDiameter, Math.Min(availableSize.Width, availableSize.Height));
        return new Size(diameter, diameter);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        var diameter = Math.Min(ActualWidth, ActualHeight);
        if (diameter <= 0) return;
        drawing.PushTransform(new TranslateTransform((ActualWidth - diameter) / 2, (ActualHeight - diameter) / 2));
        drawing.PushTransform(new ScaleTransform(diameter / 40, diameter / 40));
        var center = new Point(Center, Center);
        drawing.DrawEllipse(Surface, null, center, 19, 19);
        drawing.DrawEllipse(null, Track, center, Radius, Radius);
        var progress = _fresh ? Progress : StaleProgress;
        // A complete ellipse avoids the coincident-endpoint gap at exactly 100%.
        if (_remaining >= 100) drawing.DrawEllipse(null, progress, center, Radius, Radius);
        else if (_arc is not null) drawing.DrawGeometry(null, progress, _arc);
        drawing.DrawEllipse(null, BudgetTrack, center, 10, 10);
        if (_available >= 100) drawing.DrawEllipse(null, BudgetProgress, center, 10, 10);
        else if (_budgetArc is not null) drawing.DrawGeometry(null, BudgetProgress, _budgetArc);
        drawing.PushTransform(new RotateTransform(45, Center, Center));
        drawing.DrawRoundedRectangle(_fresh ? Diamond : null, _fresh ? null : EmptyDiamond,
            new Rect(Center - 3.2, Center - 3.2, 6.4, 6.4), 0.6, 0.6);
        drawing.Pop();
        if (_attention) drawing.DrawEllipse(Alert, AlertOutline, new Point(33, 7), 2.6, 2.6);
        drawing.Pop();
        drawing.Pop();
    }

    private static Brush MakeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen MakePen(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }
}
