using System.Windows.Media;

namespace CodexPetLimitRings.Windows;

// Shared by XAML and vector drawing so every surface follows the quota ring.
public static class HudTheme
{
    public static readonly SolidColorBrush Canvas = Make("#EFF1F5");
    public static readonly SolidColorBrush Card = Make("#F5F6FA");
    public static readonly SolidColorBrush Inset = Make("#E6E9EF");
    public static readonly SolidColorBrush Border = Make("#DCE0E8");
    public static readonly SolidColorBrush Selected = Make("#E7D9F8");
    public static readonly SolidColorBrush Hover = Make("#EADFF8");
    public static readonly SolidColorBrush Pressed = Make("#DCC9F4");
    public static readonly SolidColorBrush Text = Make("#4C4F69");
    public static readonly SolidColorBrush Muted = Make("#6C6F85");
    public static readonly SolidColorBrush Focus = Make("#8839EF");
    public static readonly SolidColorBrush Accent = Make("#8839EF");
    public static readonly SolidColorBrush AccentHover = Make("#782ED8");
    public static readonly SolidColorBrush AccentPressed = Make("#6925C4");
    public static readonly SolidColorBrush Success = Make("#3B7523");
    public static readonly SolidColorBrush Warning = Make("#996515");
    public static readonly SolidColorBrush Danger = Make("#D20F39");
    public static readonly SolidColorBrush ChartBlue = Make("#1E66F5");
    public static readonly SolidColorBrush ChartGreen = Make("#3B7523");
    public static readonly SolidColorBrush Track = Make("#CCD0DA");

    // PaceResult keeps its existing classification and calculations. Only its
    // presentation changes: the former pastel inks need contrast on light paper.
    internal static SolidColorBrush Pace(string color) => color switch
    {
        "#9BDFB5" => Success,
        "#EDC788" => Warning,
        "#EE9D9D" => Danger,
        _ => Muted
    };

    private static SolidColorBrush Make(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
