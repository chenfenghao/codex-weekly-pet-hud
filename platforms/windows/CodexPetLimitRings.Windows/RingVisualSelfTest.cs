using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexPetLimitRings.Windows.Views;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace CodexPetLimitRings.Windows;

internal static class RingVisualSelfTest
{
    internal static void Run(Action<bool, string> check, string directory)
    {
        RenderPreview(directory);
        var empty = RenderRing(0);
        var full = RenderRing(100);
        var quarter = RenderRing(25);
        var threeQuarters = RenderRing(75);
        foreach (var angle in new[] { 45, 135, 225, 315 })
        {
            check(!ArcInkAt(empty, angle), "Zero quota leaves only the light track");
            check(ArcInkAt(full, angle), "Full quota paints the entire ring");
            check(ArcInkAt(quarter, angle) == (angle < 90), "Quarter quota runs clockwise from twelve o'clock");
            check(ArcInkAt(threeQuarters, angle) == (angle < 270), "Three-quarter quota preserves the correct gap");
        }
        check(ArcInkAt(full, 0), "Full ring has no seam at twelve o'clock");
        var unknown = RenderRing(null);
        check(Pixels(RenderRing(double.NaN)).SequenceEqual(Pixels(unknown)), "NaN quota renders as unknown");
        check(Pixels(RenderRing(double.PositiveInfinity)).SequenceEqual(Pixels(unknown)), "Infinite quota renders as unknown");
        check(Pixels(RenderRing(-5)).SequenceEqual(Pixels(empty)), "Negative quota is clamped to empty");
        check(Pixels(RenderRing(105)).SequenceEqual(Pixels(full)), "Overfull quota is clamped to full");
        check(PixelAt(unknown, 20, 20).R > 235 && PixelAt(full, 20, 20).R < 220, "Hollow unknown diamond differs from valid quota");
        var stale = RenderRing(80, false);
        check(!ArcInkAt(stale, 45) && PixelAt(stale, 20, 20).R > 235, "Stale quota has a muted arc and hollow diamond");
        var alert = RenderRing(80, true, true);
        check(PixelAt(alert, 33, 7).B < 80 && PixelAt(alert, 33, 7).R > 150, "Radar alert uses an amber dot");
        foreach (var size in new[] { 20d, 26, 36, 40, 72 })
        foreach (var dpi in new[] { 1d, 1.25, 1.5, 2 })
        {
            var bitmap = RenderRing(80, true, false, size, dpi);
            check(bitmap.PixelWidth == bitmap.PixelHeight && PixelAt(bitmap, 0, 0).A == 0, "Scaled ring stays square with transparent corners");
            var visible = PixelAt(bitmap, 20, 20).A == 255 && ArcInkAt(bitmap, 45);
            if (!visible)
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, "ring-scale-failure.png")); encoder.Save(stream);
            }
            check(visible, $"Scaled diamond and quota arc remain visible: size={size}, DPI={dpi}, center={PixelAt(bitmap, 20, 20)}, arc={PixelAt(bitmap, 29.9, 10.1)}");
        }
        var ring = new QuotaRing();
        ring.UpdateUsage(80, true); ring.SetAttention(true); ring.SetAttention(false);
        check(Pixels(Render(ring, 40, 40, 2)).SequenceEqual(Pixels(RenderRing(80))), "Clearing radar attention removes its dot without changing quota");
        var badge = new QuotaBadge();
        foreach (var sample in new[] { (100d, true, "100%"), (99.9d, true, "99.9%"), (99.9d, false, "≈99.9%"), (0d, true, "0%"), (double.NaN, true, "—%") })
        {
            badge.UpdateUsage(sample.Item1, sample.Item2);
            check(badge.RemainingText == sample.Item3, "Always-visible percentage distinguishes valid, stale and unavailable quota");
        }
        var targets = new QuotaTargets(50, 33.3, DateTimeOffset.Now.AddHours(2), false, false);
        badge.UpdateUsage(80, true, targets);
        check(badge.BudgetText == "46.7%", "Badge shows closing-target headroom alongside quota");
        badge.UpdateUsage(5, true, targets);
        check(badge.BudgetText == "-28.3%", "Below-target quota uses deficit text instead of positive allowance");
        badge.UpdateUsage(80, false, targets);
        check(badge.BudgetText == "—%", "Stale data does not advertise a usable budget");
        badge.UpdateUsage(80, true, targets with { DayOff = true });
        check(badge.BudgetText == "46.7%", "Days off retain a daily reference instead of a shift label");
        ring.UpdateUsage(80, true);
        foreach (var amount in new[] { 0d, 50, 100, -5 })
        {
            ring.UpdateBudget(amount);
            var rendered = Render(ring, 40, 40, 2);
            foreach (var angle in new[] { 45d, 135, 225, 315 })
                check(BudgetInkAt(rendered, angle) == (amount > 0 && angle < amount / 100 * 360), "Inner ring represents closing headroom and never wraps negative allowance");
        }
    }

    private static RenderTargetBitmap RenderRing(double? value, bool fresh = true, bool attention = false, double size = 40, double dpi = 2)
    {
        var ring = new QuotaRing();
        ring.UpdateUsage(value, fresh); ring.SetAttention(attention);
        return Render(ring, size, size, dpi);
    }

    private static RenderTargetBitmap Render(FrameworkElement view, double width, double height, double dpi)
    {
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Round(width * dpi), (int)Math.Round(height * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
        bitmap.Render(view);
        return bitmap;
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var data = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0);
        return data;
    }

    private static Color PixelAt(BitmapSource bitmap, double x, double y)
    {
        var px = Math.Clamp((int)Math.Round(x / 40 * bitmap.PixelWidth), 0, bitmap.PixelWidth - 1);
        var py = Math.Clamp((int)Math.Round(y / 40 * bitmap.PixelHeight), 0, bitmap.PixelHeight - 1);
        var data = new byte[4]; bitmap.CopyPixels(new Int32Rect(px, py, 1, 1), data, 4, 0);
        return Color.FromArgb(data[3], data[2], data[1], data[0]);
    }

    private static bool ArcInkAt(BitmapSource bitmap, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        // At fractional DPI the thin stroke straddles pixels. Inspect its immediate
        // pixel neighborhood; the tested sectors stay forty-five degrees apart.
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            var point = PixelAt(bitmap, 20 + 14 * Math.Sin(angle) + dx * 40d / bitmap.PixelWidth,
                20 - 14 * Math.Cos(angle) + dy * 40d / bitmap.PixelHeight);
            if (point.A > 240 && point.R > point.G + 25 && point.B > point.R + 25) return true;
        }
        return false;
    }

    private static bool BudgetInkAt(BitmapSource bitmap, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        var point = PixelAt(bitmap, 20 + 10 * Math.Sin(angle), 20 - 10 * Math.Cos(angle));
        return point.A > 240 && point.R < 150 && point.G < 190 && point.B > 200;
    }

    private static void RenderPreview(string directory)
    {
        var canvas = new Canvas { Width = 840, Height = 264, Background = HudTheme.Canvas };
        void Put(UIElement child, double x, double y) { Canvas.SetLeft(child, x); Canvas.SetTop(child, y); canvas.Children.Add(child); }
        void Label(string text, double x, double y, double fontSize = 12, System.Windows.Media.Brush? brush = null) =>
            Put(new TextBlock { Text = text, FontSize = fontSize, Foreground = brush ?? HudTheme.Muted, FontFamily = new FontFamily("Microsoft YaHei UI") }, x, y);
        Label("圆环与额度", 24, 18, 20, HudTheme.Text);
        Label("外圈 / 上方：剩余 · 内圈 / 下方：下班可用 · 示例数据", 24, 52);
        Label("宠物旁", 24, 108); Label("100 × 40", 24, 128, 10);
        Put(new Border { Width = 792, Height = 50, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(37, 39, 42)) }, 24, 190);
        Label("任务栏", 40, 199, 12, Brushes.LightGray); Label("90 × 36", 40, 218, 10, Brushes.LightGray);
        var samples = new[] { (100d, true, false, "满额"), (80d, true, false, "常规"), (35.5d, true, false, "保留小数"), (5d, true, false, "剩余较少"), (99.9d, false, false, "数据延迟"), (80d, true, true, "新信号") };
        for (var i = 0; i < samples.Length; i++)
        {
            var (value, fresh, attention, label) = samples[i]; var x = 130 + i * 112;
            var target = new QuotaTargets(50, 33.3, DateTimeOffset.Now.AddHours(2), false, false);
            var pet = new QuotaBadge { Width = 100, Height = 40 }; pet.UpdateUsage(value, fresh, target); pet.SetAttention(attention); Put(pet, x, 98);
            Label(label, x + 22, 146, 11);
            var taskbar = new QuotaBadge { Width = 90, Height = 36 }; taskbar.UpdateUsage(value, fresh, target); taskbar.SetAttention(attention); Put(taskbar, x + 5, 197);
        }
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Render(canvas, 840, 264, 2)));
        using var stream = File.Create(Path.Combine(directory, "ring-preview.png")); encoder.Save(stream);
    }
}
