using System.Runtime.InteropServices;
using System.Windows.Interop;
using CodexPetLimitRings.Windows.Views;
using CodexPetLimitRings.Windows.Services;

namespace CodexPetLimitRings.Windows.Interop;

// A separate child HWND, owned by this process, lives inside the primary taskbar.
// No Explorer injection, taskbar resizing, topmost loop or Pet input proxy.
internal sealed class TaskbarHudHost : IDisposable
{
    private HwndSource? _source;
    private nint _parent;
    private DateTimeOffset _retryAfter;
    internal TaskbarHudView View { get; } = new();
    internal nint Handle => _source is { IsDisposed: false } ? _source.Handle : 0;
    internal bool IsVisible => Handle != 0 && IsWindowVisible(Handle);
    internal string Status { get; private set; } = "任务栏正在连接";

    internal bool Place(double offset)
    {
        var parent = FindWindow("Shell_TrayWnd", null);
        if (parent == 0 || !IsWindowVisible(parent) || !GetClientRect(parent, out var bounds))
        { Hide(); Status = "任务栏正在连接"; return false; }
        var tray = FindWindowEx(parent, 0, "TrayNotifyWnd", null);
        var dpi = GetDpiForWindow(parent) / 96d;
        var trayStart = bounds.Right;
        if (tray != 0 && GetWindowRect(tray, out var trayBounds))
        {
            var point = new Point { X = trayBounds.Left, Y = trayBounds.Top };
            if (ScreenToClient(parent, ref point)) trayStart = point.X;
        }
        else
        { Hide(); Status = "任务栏布局暂不支持，请使用宠物模式"; return false; }
        var placement = TaskbarLayout.Calculate(bounds.Right, bounds.Bottom, trayStart, dpi, offset);
        if (placement is not { } p)
        { Hide(); Status = "任务栏空间不足，请使用宠物模式"; return false; }
        if (_source is not null && (parent != _parent || !IsWindow(Handle))) Detach();
        if (_source is null)
        {
            if (DateTimeOffset.UtcNow < _retryAfter) return false;
            // Child and Explorer must share a DPI-awareness context. Restore our UI
            // thread immediately so Pet and detail windows retain their own context.
            var previous = SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(parent));
            try
            {
                _source = new HwndSource(new HwndSourceParameters("CodexWeeklyTaskbarHud")
                {
                    ParentWindow = parent, WindowStyle = 0x40000000 | 0x04000000, // CHILD | CLIPSIBLINGS
                    ExtendedWindowStyle = 0x08000000 | 0x00000080, // NOACTIVATE | TOOLWINDOW
                    PositionX = p.X, PositionY = p.Y, Width = p.Width, Height = p.Height
                });
                _source.RootVisual = View;
                _parent = parent;
                AppLog.Write("Taskbar HUD attached to primary taskbar.");
            }
            catch (Exception error)
            {
                Detach(); _retryAfter = DateTimeOffset.UtcNow.AddSeconds(5);
                Status = "任务栏正在连接";
                AppLog.Write($"Taskbar attach failed: {error.GetType().Name}.");
                return false;
            }
            finally { if (previous != 0) SetThreadDpiAwarenessContext(previous); }
        }
        var scale = _source.CompositionTarget?.TransformFromDevice.M11 ?? 1 / dpi;
        View.Width = p.Width * scale; View.Height = p.Height * scale;
        // Child ordering only; HWND_TOP here never makes a top-level topmost window.
        var placed = SetWindowPos(Handle, 0, p.X, p.Y, p.Width, p.Height, 0x0010 | 0x0040);
        Status = placed ? "任务栏显示" : "任务栏正在连接";
        return placed;
    }

    internal void Hide() { if (Handle != 0 && IsWindow(Handle)) ShowWindow(Handle, 0); }
    internal void PositionDetails(System.Windows.Window window)
    {
        if (Handle == 0 || !GetWindowRect(Handle, out var rect)) return;
        window.UpdateLayout();
        var scale = GetDpiForWindow(new WindowInteropHelper(window).EnsureHandle()) / 96d;
        if (scale <= 0) return;
        var area = System.Windows.Forms.Screen.FromHandle(_parent).WorkingArea;
        window.Left = Math.Clamp(rect.Right / scale - window.ActualWidth, area.Left / scale,
            Math.Max(area.Left / scale, area.Right / scale - window.ActualWidth));
        window.Top = Math.Clamp(rect.Top / scale - window.ActualHeight - 8, area.Top / scale,
            Math.Max(area.Top / scale, area.Bottom / scale - window.ActualHeight));
    }
    internal void Detach()
    {
        if (_source is { IsDisposed: false }) { _source.RootVisual = null; _source.Dispose(); }
        _source = null;
        _parent = 0;
    }
    public void Dispose() => Detach();
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string className, string? title);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window, ref Point point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetWindowDpiAwarenessContext(nint window);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
