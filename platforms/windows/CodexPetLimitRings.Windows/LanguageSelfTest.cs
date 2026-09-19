using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexPetLimitRings.Windows.Services;
using CodexPetLimitRings.Windows.Views;
using ComboBox = System.Windows.Controls.ComboBox;
using FlowDirection = System.Windows.FlowDirection;

namespace CodexPetLimitRings.Windows;

internal static class LanguageSelfTest
{
    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var originalLanguage = UiText.Instance.Language;
        var count = 0;
        void Check(bool valid, string description) { if (!valid) throw new InvalidOperationException(description); count++; }
        bool English(string text) => !Regex.IsMatch(text, @"[\u4e00-\u9fff]");
        var capsule = new PotionWindow("test", default, default, default, default);
        var details = new UsageDetailsWindow();
        var settings = new SettingsWindow { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            foreach (var pair in UiText.Translations)
            {
                Check(!string.IsNullOrWhiteSpace(pair.Value) && (English(pair.Value) || pair.Key == "语言 / Language"), "Missing English translation: " + pair.Key);
                Check(System.Text.CompositeFormat.Parse(pair.Key).MinimumArgumentCount == System.Text.CompositeFormat.Parse(pair.Value).MinimumArgumentCount, "Format placeholder mismatch: " + pair.Key);
            }
            UiText.SetLanguage("en");
            var now = DateTimeOffset.Now;
            var usage = WeeklyPasteParser.Parse("Remaining 80%\n6d 07:37:30", now);
            Check(usage.SecondaryUsed == 20, "English remaining input");
            Check(WeeklyPasteParser.Parse("已用 20% 6天", now).SecondaryUsed == 20, "Chinese input remains supported in English UI");
            try { WeeklyPasteParser.Parse("Remaining 180% 6d", now); Check(false, "Invalid input accepted"); }
            catch (FormatException error) { Check(English(error.Message), "English validation error"); }
            Check(English(WeeklyPacing.Calculate(20, now.AddDays(5).ToUnixTimeSeconds(), now).Prediction), "English exhaustion prediction");
            Check(English(WeeklyPacing.Countdown(usage.SecondaryReset, now)), "English countdown");
            var alert = new ResetSignalEvent("sample", "reset", "confirmed", now, "Example announcement");
            Check(alert.Title == "Community-confirmed reset", "English notification title");
            var eventKey = alert.Key;
            var saved = new OverlaySettings { Language = "en", RefreshMinutes = 17, Scale = 1.2 };
            var restored = JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(saved))!;
            restored.Normalize();
            Check(restored.Language == "en" && restored.RefreshMinutes == 17 && restored.Scale == 1.2, "Language persists alongside other settings");
            var invalid = new OverlaySettings { Language = "unsupported" }; invalid.Normalize();
            Check(invalid.Language == "zh-CN", "Invalid language fallback");
            settings.Apply(saved);
            capsule.UpdateUsage(80, usage.SecondaryReset, "live");
            capsule.UpdateResetSignal(UiText.T("重置雷达 · 暂无新信号"), "Example data", false);
            details.ApplyLanguage(); details.Update(usage, false);
            details.UpdateResetSignal(UiText.T("重置雷达 · 暂无新信号"), "Example data · Last global reset notice: Sep 12, 16:09", false, false);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(settings.Title == "Weekly quota · Settings", "Settings title binding");
            Check(details.Title == "Weekly quota · Pace companion", "Details title binding");
            Render((FrameworkElement)capsule.Content, 190, 72, Path.Combine(directory, "capsule.en.png"));
            Render((FrameworkElement)details.Content, 410, 680, Path.Combine(directory, "details.en.png"));
            Render((FrameworkElement)settings.Content, 520, 620, Path.Combine(directory, "settings.en.png"));
            foreach (var view in new FrameworkElement[] { (FrameworkElement)capsule.Content, (FrameworkElement)details.Content })
                foreach (var text in TextBlocks(view))
                    Check(English(text.Text), "Untranslated visible label: " + text.Text);
            var left = (TextBlock)capsule.FindName("RemainingText");
            var pace = (TextBlock)capsule.FindName("PaceText");
            var leftBounds = left.TransformToAncestor((Visual)capsule.Content).TransformBounds(new Rect(left.RenderSize));
            var paceBounds = pace.TransformToAncestor((Visual)capsule.Content).TransformBounds(new Rect(pace.RenderSize));
            var measured = new FormattedText(left.Text, UiText.Culture, FlowDirection.LeftToRight, new Typeface(left.FontFamily, left.FontStyle, left.FontWeight, left.FontStretch), left.FontSize, left.Foreground, 1);
            Check(leftBounds.Left + measured.Width < paceBounds.Left - 3, "English capsule labels overlap");
            capsule.UpdateResetSignal(UiText.T("雷达 · 新重置信号"), "Example signal", true);
            Render((FrameworkElement)capsule.Content, 190, 72, Path.Combine(directory, "radar-alert.en.png"));
            // Exercise the real settings control and its save event on an interactive desktop.
            var changes = 0;
            settings.SettingsChanged += value => { changes++; File.WriteAllText(Path.Combine(directory, "settings-roundtrip.json"), JsonSerializer.Serialize(value)); };
            settings.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var language = (ComboBox)settings.FindName("LanguageChoice");
            language.SelectedIndex = 0;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(UiText.Instance.Language == "zh-CN" && settings.Title == "周额度 · 挂件设置", "Live switch back to Chinese");
            Check(alert.Key == eventKey && alert.Title == "社区已确认重置公告", "Language change preserves notification identity");
            language.SelectedIndex = 1;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(UiText.Instance.Language == "en" && changes == 2, "Live English selection emits save event");
            var file = JsonSerializer.Deserialize<OverlaySettings>(File.ReadAllText(Path.Combine(directory, "settings-roundtrip.json")))!;
            Check(file.Language == "en" && file.RefreshMinutes == 17, "Saved language retains refresh interval");
            ((System.Windows.Controls.CheckBox)settings.FindName("WorkHoursEnabled")).IsChecked = true;
            ((ComboBox)settings.FindName("WorkStart")).SelectedIndex = 28;
            ((ComboBox)settings.FindName("WorkEnd")).SelectedIndex = 84;
            var days = (System.Windows.Controls.WrapPanel)settings.FindName("WorkDaysPanel");
            days.Children.OfType<System.Windows.Controls.CheckBox>().First(day => day.Tag.ToString() == "6").IsChecked = true;
            var workSettings = JsonSerializer.Deserialize<OverlaySettings>(File.ReadAllText(Path.Combine(directory, "settings-roundtrip.json")))!;
            Check(workSettings.WorkHoursEnabled && workSettings.WorkStartMinute == 420 && workSettings.WorkEndMinute == 1260 && workSettings.WorkDays.SequenceEqual(new[] {1,2,3,4,5,6}), "Work schedule UI saves 07-21 Monday-Saturday");
            Check(workSettings.Language == "en" && workSettings.RefreshMinutes == 17, "Work schedule preserves unrelated settings");
            capsule.PacingSettings = details.PacingSettings = workSettings;
            capsule.UpdateUsage(usage.SecondaryRemaining, usage.SecondaryReset, usage.Source);
            details.Update(usage, false);
            Check(((TextBlock)capsule.FindName("IdealText")).Text.Contains("Even") && (((TextBlock)capsule.FindName("IdealText")).Text.Contains("Close") || ((TextBlock)capsule.FindName("IdealText")).Text.Contains("Off")), "Capsule shows both targets");
            Check(((TextBlock)details.FindName("BudgetLabel")).Text == "Budget left today", "Details identifies today's budget");
            Render((FrameworkElement)capsule.Content, 190, 72, Path.Combine(directory, "work-capsule.en.png"));
            settings.Height = 900;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Render((FrameworkElement)settings.Content, 520, 900, Path.Combine(directory, "work-settings.en.png"));
            File.WriteAllText(Path.Combine(directory, "result.txt"), $"PASS: {count} translation, formatting, live-switch, persistence, input and layout assertions.");
            return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(directory, "result.txt"), error.ToString()); return 1; }
        finally { settings.ClosePermanently(); details.ClosePermanently(); capsule.Close(); UiText.SetLanguage(originalLanguage); }
    }
    private static IEnumerable<TextBlock> TextBlocks(DependencyObject root)
    {
        if (root is TextBlock text) yield return text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in TextBlocks(VisualTreeHelper.GetChild(root, i))) yield return child;
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
