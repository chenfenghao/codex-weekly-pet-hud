using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexPetLimitRings.Windows.Interop;
using CodexPetLimitRings.Windows.Views;

namespace CodexPetLimitRings.Windows;

internal static class TaskbarSelfTest
{
    internal static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var count = 0;
        void Check(bool pass, string label) { if (!pass) throw new InvalidOperationException(label); count++; }
        using var host = new TaskbarHudHost();
        var settings = new SettingsWindow { ShowActivated = false, ShowInTaskbar = false };
        var original = UiText.Instance.Language;
        try
        {
            var usage = new UsageSnapshot(null, 20, null, DateTimeOffset.Now.AddDays(5).ToUnixTimeSeconds(), "manual", DateTimeOffset.Now);
            foreach (var language in new[] { "zh-CN", "en" })
            {
                UiText.SetLanguage(language);
                host.View.Update(usage, UiText.T("重置雷达 · 暂无新信号"), "Sample signal data", false);
                Check(host.Place(0), "Taskbar attachment");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await Task.Delay(250);
                var parent = GetParent(host.Handle);
                Check(parent == FindWindow("Shell_TrayWnd", null), "Real taskbar child HWND");
                Check((GetWindowLongPtr(host.Handle, -16).ToInt64() & 0x40000000) != 0, "Child style");
                Check((GetWindowLongPtr(host.Handle, -20).ToInt64() & 8) == 0, "Not topmost");
                Check(GetWindowRect(host.Handle, out var bounds), "Native bounds");
                Check(GetWindowRect(parent, out var taskbar), "Parent bounds");
                Check(bounds.Left >= taskbar.Left && bounds.Top >= taskbar.Top && bounds.Right <= taskbar.Right && bounds.Bottom <= taskbar.Bottom, "Clipped inside taskbar");
                var center = new Point { X = (bounds.Left + bounds.Right)/2, Y = (bounds.Top + bounds.Bottom)/2 };
                Check(WindowFromPoint(center) == host.Handle, "Taskbar widget receives its clicks");
                var tray = FindWindowEx(parent, 0, "TrayNotifyWnd", null);
                Check(GetWindowRect(tray, out var trayBounds), "Notification area available");
                Check(bounds.Right <= trayBounds.Left, "Notification area unobstructed");
                Check(WindowFromPoint(new Point { X = trayBounds.Left + 10, Y = center.Y }) != host.Handle, "Notification hit test unchanged");
                using (var bitmap = new System.Drawing.Bitmap(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top))
                {
                    using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                    var brightPixels = 0;
                    // Native compositing may trail WPF layout on the first HWND.
                    // Validate actual on-screen text, not just a valid hit-test rectangle.
                    for (var attempt = 0; attempt < 10 && brightPixels < 40; attempt++)
                    {
                        await Task.Delay(200);
                        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size);
                        brightPixels = 0;
                        for (var y = 0; y < bitmap.Height; y++)
                        for (var x = 0; x < bitmap.Width; x++)
                        {
                            var pixel = bitmap.GetPixel(x, y);
                            if (pixel.R > 160 && pixel.G > 160 && pixel.B > 160) brightPixels++;
                        }
                    }
                    Check(brightPixels >= 40, "Native text actually painted");
                    bitmap.Save(Path.Combine(directory, "taskbar-live." + language + ".png"));
                }
                Render(host.View, Path.Combine(directory, "taskbar." + language + ".png"));
                host.Hide(); Check(!host.IsVisible, "Shell flyout hide");
                Check(host.Place(100), "Restore with offset");
                Check(GetWindowRect(host.Handle, out var shifted) && shifted.Left < bounds.Left, "Offset moves within taskbar");
                host.Detach(); Check(host.Handle == 0, "Dispose old HWND");
                Check(host.Place(0), "Recreate after detach");
                host.Detach();
            }
            var saved = new OverlaySettings { Language = "en", RefreshMinutes = 17 };
            settings.Apply(saved); settings.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var changes = 0;
            settings.SettingsChanged += value => { changes++; saved = JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(value))!; };
            var choice = (System.Windows.Controls.ComboBox)settings.FindName("DisplayModeChoice");
            choice.SelectedIndex = 1;
            Check(saved.DisplayMode == "taskbar" && changes == 1, "Live taskbar setting saves");
            Check(!((Slider)settings.FindName("Scale")).IsEnabled, "Pet scale disabled for taskbar");
            ((Slider)settings.FindName("TaskbarOffset")).Value = 100;
            Check(saved.TaskbarOffset == 100 && saved.RefreshMinutes == 17, "Offset preserves refresh setting");
            choice.SelectedIndex = 0;
            Check(saved.DisplayMode == "pet" && ((Slider)settings.FindName("Scale")).IsEnabled, "Return to Pet mode");
            File.WriteAllText(Path.Combine(directory, "result.txt"), $"PASS: {count} taskbar native placement, hit-test, lifecycle, language and settings assertions.");
            return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(directory, "result.txt"), error.ToString()); return 1; }
        finally { settings.ClosePermanently(); UiText.SetLanguage(original); }
    }

    private static void Render(FrameworkElement view, string path)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth * 2), (int)Math.Ceiling(view.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll")] private static extern nint GetParent(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern nint FindWindow(string name, string? title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string name, string? title);
}
