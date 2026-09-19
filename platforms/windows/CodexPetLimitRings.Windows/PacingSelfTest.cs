using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexPetLimitRings.Windows.Views;

namespace CodexPetLimitRings.Windows;

internal static class PacingSelfTest
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var assertions = 0;
        void Check(bool passed, string name) { if (!passed) throw new InvalidOperationException(name); assertions++; }
        WorkPacingSelfTest.Run(Check);
        var now = new DateTimeOffset(2026, 9, 13, 8, 0, 0, TimeSpan.FromHours(8));
        var parsed = WeeklyPasteParser.Parse("重置时间：2026年9月19日 16:12\n剩余 80%", now);
        Check(parsed.SecondaryUsed == 20 && parsed.SecondaryRemaining == 80, "Remaining percent conversion");
        var localReset = DateTimeOffset.FromUnixTimeSeconds(parsed.SecondaryReset!.Value).LocalDateTime;
        Check(localReset.Year == 2026 && localReset.Month == 9 && localReset.Day == 19 && localReset.Hour == 16 && localReset.Minute == 12, "Chinese reset date and time");
        foreach (var text in new[] { "剩余 80%\n2026-09-19 16:12", "80% remaining\n2026-09-19 16:12", "已用 20%\n2026-09-19 16:12" })
            Check(WeeklyPasteParser.Parse(text, now).SecondaryUsed == 20, "Equivalent input: " + text);
        Check(WeeklyPasteParser.Parse("已用 25% 3天5小时", now).SecondaryReset == now.AddDays(3).AddHours(5).ToUnixTimeSeconds(), "Relative time");
        Check(WeeklyPasteParser.Parse("已用 25% 6d 07:37:30", now).SecondaryReset == now.AddDays(6).AddHours(7).AddMinutes(37).AddSeconds(30).ToUnixTimeSeconds(), "Countdown");
        foreach (var invalid in new[] { "剩余 120% 3天", "已用 20% 剩余 60% 3天", "剩余 80% 2026年9月31日 16:12", "剩余 80% 8天", "剩余 80% 2026年9月12日 16:12" })
        {
            var rejected = false;
            try { WeeklyPasteParser.Parse(invalid, now); } catch (FormatException) { rejected = true; }
            Check(rejected, "Invalid input rejected");
        }
        var reset = now.AddDays(3.5).ToUnixTimeSeconds();
        Check(WeeklyPacing.Calculate(75, reset, now).Status == "偏慢", "Slow pace");
        Check(WeeklyPacing.Calculate(57.5, reset, now).Status == "稳健", "Slow/steady threshold");
        var aboveDefault = HudLayout.CalculateCapsule(new PetAnchor(800, 400, 112, 121, null, 0, 0, 1920, 1040), new OverlaySettings());
        Check(aboveDefault.PotionWidth == 190 && aboveDefault.PotionHeight == 72 && aboveDefault.Y + aboveDefault.PotionHeight < 400, "Small badge above by default");
        Check(new OverlaySettings().AutoReadUsage, "Auto read on by default");
        var intervalSettings = new OverlaySettings();
        Check(intervalSettings.RefreshInterval == TimeSpan.FromMinutes(5), "Five minute refresh default");
        Check(TimeSpan.FromSeconds(30) < intervalSettings.RefreshInterval, "Thirty seconds no longer triggers refresh");
        intervalSettings.RefreshMinutes = 15;
        Check(intervalSettings.RefreshInterval == TimeSpan.FromMinutes(15), "Custom refresh takes effect");
        intervalSettings.RefreshMinutes = 0; intervalSettings.Normalize();
        Check(intervalSettings.RefreshMinutes == 1, "Minimum refresh interval");
        intervalSettings.RefreshMinutes = 100; intervalSettings.Normalize();
        Check(intervalSettings.RefreshMinutes == 60, "Maximum refresh interval");
        Check(WeeklyPacing.Calculate(75, reset, now).TimePercent == 50, "Expected remaining is 50 percent at midweek");
        Check(WeeklyPacing.Calculate(50, reset, now).Status == "稳健", "R=1 boundary");
        Check(WeeklyPacing.Calculate(35, reset, now).Status == "偏快", "R=1.3 boundary");
        Check(WeeklyPacing.Calculate(34, reset, now).Status == "超速", "Overspeed boundary");
        Check(WeeklyPacing.Calculate(60, now.AddDays(5).ToUnixTimeSeconds(), now).DailyBudget == 12, "Daily budget");
        Check(WeeklyPacing.Calculate(10, now.AddHours(1).ToUnixTimeSeconds(), now).DailyBudget == 10, "Partial day budget");
        Check(WeeklyPacing.Calculate(80, now.ToUnixTimeSeconds(), now).Status == "待确认重置", "Expired data");
        Check(WeeklyPacing.Calculate(100, now.AddDays(7).ToUnixTimeSeconds(), now).Ratio is null, "Zero elapsed");
        Check(WeeklyPacing.Calculate(0, reset, now).Status == "已耗尽", "Exhausted");
        foreach (var x in new[] { -1900, -800, 0, 5, 800, 1803 })
        foreach (var y in new[] { 1, 400, 918 })
        foreach (var alignment in new[] { "left", "right", "above", "below" })
        {
            var a = new PetAnchor(x, y, 112, 121, null, x < 0 ? -1920 : 0, 0, 1920, 1040);
            var p = HudLayout.CalculateCapsule(a, new OverlaySettings { Scale = 1, Alignment = alignment });
            Check(p.SecondaryX >= a.WorkX && p.SecondaryX + p.PotionWidth <= a.WorkRight && p.Y >= a.WorkY && p.Y + p.PotionHeight <= a.WorkBottom, "Capsule on screen");
            Check(p.SecondaryX + p.PotionWidth <= a.X || p.SecondaryX >= a.Right || p.Y + p.PotionHeight <= a.Y || p.Y >= a.Y + a.Height, "Pet stays unobstructed");
        }
        var capsule = new PotionWindow("周", Colors.Transparent, Colors.Transparent, Colors.Transparent, Colors.Transparent);
        capsule.ApplyScale(1);
        capsule.UpdateUsage(80, parsed.SecondaryReset, "manual");
        capsule.UpdateResetSignal("重置雷达 · 暂无新信号", "公开 API 已连接", false);
        Render((FrameworkElement)capsule.Content, 190, 72, Path.Combine(directory, "capsule.png"));
        capsule.UpdateResetSignal("雷达 · 新重置信号", "社区公告待核实", true);
        Render((FrameworkElement)capsule.Content, 190, 72, Path.Combine(directory, "radar-alert.png"));
        var settingsWindow = new SettingsWindow();
        settingsWindow.Apply(new OverlaySettings());
        Render((FrameworkElement)settingsWindow.Content, 520, 620, Path.Combine(directory, "settings.png"));
        settingsWindow.ClosePermanently();
        var details = new UsageDetailsWindow();
        details.Update(parsed, false);
        details.UpdateResetSignal("重置雷达 · 暂无新信号", "最近检查 9/13 11:00 · 每 5 分钟\n上次全局重置公告：9/12 16:09\n社区已确认重置公告", false, false);
        Render((FrameworkElement)details.Content, 370, 680, Path.Combine(directory, "details.png"));
        capsule.Close(); details.ClosePermanently();
        File.WriteAllText(Path.Combine(directory, "result.txt"), $"PASS: {assertions} assertions; capsule and details rendered.");
        return 0;
    }
    private static void Render(FrameworkElement element, double width, double height, string path)
    {
        element.Measure(new System.Windows.Size(width, height));
        element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
