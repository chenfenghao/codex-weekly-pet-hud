using System.Runtime.InteropServices;
using System.Windows.Interop;
using CodexPetLimitRings.Windows.Interop;
using CodexPetLimitRings.Windows.Views;

namespace CodexPetLimitRings.Windows;

internal static class OverlayOrderSelfTest
{
    public static async Task<int> RunAsync(string output)
    {
        var checks = 0;
        void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
            checks++;
        }
        var proxy = new PetInputProxyWindow { Left = 80, Top = 100, Width = 112, Height = 121 };
        var capsule = new PotionWindow("test", default, default, default, default) { Left = 80, Top = 25 };
        nint popup = 0;
        var module = GetModuleHandle(null);
        var windowClass = new WindowClass { Instance = module, ClassName = "NotifyIconOverflowWindow", WindowProcedure = Procedure };
        ushort atom = 0;
        try
        {
            Check(NativeMethods.IsShellFlyoutClass("NotifyIconOverflowWindow"), "Windows 10 tray not recognized");
            Check(NativeMethods.IsShellFlyoutClass("TopLevelWindowForOverflowXamlIsland"), "Windows 11 tray not recognized");
            Check(NativeMethods.IsShellFlyoutClass("#32768"), "Native menu not recognized");
            Check(!NativeMethods.IsShellFlyoutClass("Shell_TrayWnd"), "Taskbar must not permanently suppress HUD");
            Check(!NativeMethods.IsShellFlyoutClass("Chrome_WidgetWin_1"), "Ordinary app must not suppress HUD");
            proxy.Show(); capsule.Show();
            NativeMethods.PlacePetInputProxy(proxy);
            NativeMethods.PlacePotionWindow(capsule, 0, true);
            await Task.Delay(80);
            Check(NativeMethods.TryGetPhysicalWindowRect(proxy, out var proxyBounds), "Missing proxy bounds");
            Check(NativeMethods.TryGetPhysicalWindowRect(capsule, out var capsuleBounds), "Missing capsule bounds");
            var bounds = System.Drawing.Rectangle.Union(proxyBounds, capsuleBounds);
            atom = RegisterClass(ref windowClass);
            Check(atom != 0, "Could not create test popup class");
            popup = CreateWindowEx(0x08000088, windowClass.ClassName, "Overlay overlap regression test", 0x90000000,
                bounds.X, bounds.Y, bounds.Width, bounds.Height, 0, 0, module, 0);
            Check(popup != 0, "Could not create test popup");
            await Task.Delay(80);
            var petPoint = new NativeMethods.NativePoint(proxyBounds.X + 40, proxyBounds.Y + 40);
            var capsulePoint = new NativeMethods.NativePoint(capsuleBounds.X + 40, capsuleBounds.Y + 25);
            Check(NativeMethods.IsShellFlyoutOpen(), "Visible tray popup was not detected");
            for (var i = 0; i < 8; i++)
            {
                NativeMethods.PlacePetInputProxy(proxy);
                NativeMethods.PlacePotionWindow(capsule, 0, true);
                await Task.Delay(40);
            }
            Check(NativeMethods.GetRootWindowAtPoint(petPoint) == popup, "Invisible proxy stole the popup's hit target");
            Check(NativeMethods.GetRootWindowAtPoint(capsulePoint) == popup, "Capsule stole the popup's hit target");
            proxy.Hide(); capsule.Hide();
            Check(NativeMethods.GetRootWindowAtPoint(petPoint) == popup, "Yielding did not release popup input");
            DestroyWindow(popup); popup = 0;
            proxy.Show(); capsule.Show();
            NativeMethods.PlacePetInputProxy(proxy);
            NativeMethods.PlacePotionWindow(capsule, 0, true);
            await Task.Delay(80);
            Check(NativeMethods.GetRootWindowAtPoint(petPoint) == new WindowInteropHelper(proxy).Handle, "Proxy did not resume after popup closed");
            Check(NativeMethods.GetRootWindowAtPoint(capsulePoint) == new WindowInteropHelper(capsule).Handle, "Capsule did not resume after popup closed");
            File.WriteAllText(output, $"PASS: {checks} shell flyout, native z-order, hit target and resume assertions.");
            return 0;
        }
        catch (Exception error) { File.WriteAllText(output, error.ToString()); return 1; }
        finally
        {
            if (popup != 0) DestroyWindow(popup);
            proxy.Close(); capsule.Close();
            if (atom != 0) UnregisterClass(windowClass.ClassName, module);
        }
    }
    private delegate nint WindowProcedure(nint handle, uint message, nint wParam, nint lParam);
    private static readonly WindowProcedure Procedure = DefWindowProc;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string ClassName;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClass(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint extendedStyle, string className, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint handle, uint message, nint wParam, nint lParam);
}
