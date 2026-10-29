using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TextBlock = System.Windows.Controls.TextBlock;

namespace CodexPetLimitRings.Windows.Views;

// Keep the same ring and percentage layout in both display modes. Scaling the
// entire badge preserves room for decimal values at fractional taskbar DPI.
public sealed class QuotaBadge : Viewbox
{
    private readonly QuotaRing _ring = new() { Width = HudLayout.RingDiameter, Height = HudLayout.RingDiameter };
    private readonly TextBlock _value = new()
    {
        Text = "—%", FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"), FontSize = 13,
        FontWeight = FontWeights.SemiBold, Foreground = HudTheme.Muted,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _budget = new()
    {
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"), FontSize = 10,
        Foreground = HudTheme.Muted
    };
    public string RemainingText { get; private set; } = "—%";
    public string BudgetText => _budget.Text;

    public QuotaBadge()
    {
        Stretch = Stretch.Uniform;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HudLayout.RingDiameter) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var numbers = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 0) };
        numbers.Children.Add(_value); numbers.Children.Add(_budget);
        grid.Children.Add(_ring); Grid.SetColumn(numbers, 1); grid.Children.Add(numbers);
        Child = new Border
        {
            Width = HudLayout.BadgeWidth, Height = HudLayout.RingDiameter,
            Background = HudTheme.Canvas, CornerRadius = new CornerRadius(HudLayout.RingDiameter / 2), Child = grid
        };
    }

    public void UpdateUsage(double? remaining, bool fresh, QuotaTargets? targets = null)
    {
        var value = remaining is { } number && double.IsFinite(number) ? Math.Clamp(number, 0, 100) : (double?)null;
        _ring.UpdateUsage(value, fresh);
        RemainingText = value is { } known ? (fresh ? "" : "≈") + known.ToString("0.#", UiText.Culture) + "%" : "—%";
        _value.Text = RemainingText;
        _value.Foreground = fresh && value is not null ? HudTheme.Accent : HudTheme.Muted;
        var available = fresh && targets is not null ? QuotaVisualModel.Available(value, targets) : null;
        _ring.UpdateBudget(available);
        _budget.Text = available is { } amount ? amount.ToString("0.#", UiText.Culture) + "%" : "—%";
        _budget.Foreground = available < 0 ? HudTheme.Warning : available is not null ? HudTheme.ChartBlue : HudTheme.Muted;
    }

    public void SetAttention(bool attention) => _ring.SetAttention(attention);
}
