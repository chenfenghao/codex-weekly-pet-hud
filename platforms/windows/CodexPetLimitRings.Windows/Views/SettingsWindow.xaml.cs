using System.Windows;
using System.Windows.Controls;

namespace CodexPetLimitRings.Windows.Views;

public partial class SettingsWindow : Window
{
    private bool _applying;
    private bool _allowClose;
    private OverlaySettings _settings = new();
    public event Action<OverlaySettings>? SettingsChanged;

    public SettingsWindow() => InitializeComponent();

    public void Apply(OverlaySettings settings)
    {
        _applying = true;
        _settings = settings;
        UiText.SetLanguage(settings.Language);
        LanguageChoice.SelectedItem = LanguageChoice.Items.Cast<ComboBoxItem>().First(item => item.Tag?.ToString() == settings.Language);
        RefreshMinutes.Value = settings.RefreshMinutes;
        ResetRadarEnabled.IsChecked = settings.ResetRadarEnabled;
        ResetNotificationsEnabled.IsChecked = settings.ResetNotificationsEnabled;
        Scale.Value = settings.Scale;
        HorizontalOffset.Value = settings.HorizontalOffset;
        VerticalOffset.Value = settings.VerticalOffset;
        PotionGap.Value = settings.PotionGap;
        Alignment.SelectedItem = Alignment.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), settings.Alignment, StringComparison.OrdinalIgnoreCase))
            ?? Alignment.Items[0];
        UpdateValueLabels();
        _applying = false;
    }

    private void Control_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_applying || !IsLoaded) return;
        _settings.Language = (LanguageChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "zh-CN";
        UiText.SetLanguage(_settings.Language);
        _settings.RefreshMinutes = (int)Math.Round(RefreshMinutes.Value);
        _settings.ResetRadarEnabled = ResetRadarEnabled.IsChecked == true;
        _settings.ResetNotificationsEnabled = ResetNotificationsEnabled.IsChecked == true;
        _settings.Scale = Scale.Value;
        _settings.HorizontalOffset = HorizontalOffset.Value;
        _settings.VerticalOffset = VerticalOffset.Value;
        _settings.PotionGap = PotionGap.Value;
        _settings.Alignment = (Alignment.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "right";
        UpdateValueLabels();
        SettingsChanged?.Invoke(_settings);
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        Apply(new OverlaySettings { Language = _settings.Language });
        SettingsChanged?.Invoke(_settings);
    }

    private void NudgeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;
        switch (button.Tag?.ToString())
        {
            case "left": HorizontalOffset.Value = Math.Max(HorizontalOffset.Minimum, HorizontalOffset.Value - 5); break;
            case "right": HorizontalOffset.Value = Math.Min(HorizontalOffset.Maximum, HorizontalOffset.Value + 5); break;
            case "up": VerticalOffset.Value = Math.Max(VerticalOffset.Minimum, VerticalOffset.Value - 5); break;
            case "down": VerticalOffset.Value = Math.Min(VerticalOffset.Maximum, VerticalOffset.Value + 5); break;
            case "center": HorizontalOffset.Value = 0; VerticalOffset.Value = 0; break;
        }
    }

    private void UpdateValueLabels()
    {
        RefreshMinutesValue.Text = UiText.F("每 {0:0} 分钟", RefreshMinutes.Value);
        ScaleValue.Text = $"{Scale.Value * 100:0}%";
        HorizontalOffsetValue.Text = $"{HorizontalOffset.Value:+0;-0;0}px";
        VerticalOffsetValue.Text = $"{VerticalOffset.Value:+0;-0;0}px";
        PotionGapValue.Text = $"{PotionGap.Value:0}px";
    }

    public void ClosePermanently() { _allowClose = true; Close(); }
    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } }
}
