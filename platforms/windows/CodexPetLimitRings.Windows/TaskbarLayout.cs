namespace CodexPetLimitRings.Windows;

// All inputs/outputs are physical pixels. Settings are converted from DIP once.
internal readonly record struct TaskbarPlacement(int X, int Y, int Width, int Height);
internal static class TaskbarLayout
{
    internal static TaskbarPlacement? Calculate(int width, int height, int trayStart, double dpiScale, double offset)
    {
        if (width <= height || width <= 0 || height <= 0 || !double.IsFinite(dpiScale) || dpiScale <= 0)
            return null; // Vertical/unsupported bars keep the tray menu available.
        var gap = Math.Max(2, (int)Math.Round(4 * dpiScale));
        var hudWidth = (int)Math.Round(190 * dpiScale);
        var hudHeight = Math.Min(height - gap * 2, (int)Math.Round(36 * dpiScale));
        var right = Math.Clamp(trayStart, 0, width) - gap;
        var leftLimit = (int)Math.Round(64 * dpiScale); // Never cover Start.
        if (hudHeight < 20 * dpiScale || right - leftLimit < hudWidth) return null;
        var shift = double.IsFinite(offset) ? Math.Clamp(offset, 0, 1600) : 0;
        var x = Math.Max(leftLimit, right - hudWidth - (int)Math.Round(shift * dpiScale));
        return new TaskbarPlacement(x, (height - hudHeight) / 2, hudWidth, hudHeight);
    }
}
