using System.Windows;
using System.Windows.Controls;

namespace CodexPetLimitRings.Windows.Views;

public partial class SettingsWindow : Window
{
    private bool _applying;
    private bool _allowClose;
    private OverlaySettings _settings = new();
    public event Action<OverlaySettings>? SettingsChanged;

    public SettingsWindow()
    {
        InitializeComponent();
        var times = Enumerable.Range(0, 96).Select(i => $"{i / 4:00}:{i % 4 * 15:00}").ToArray();
        foreach (var choice in new[] { WorkStart, WorkEnd, WorkBreakStart, WorkBreakEnd }) choice.ItemsSource = times;
    }

    public void Apply(OverlaySettings settings)
    {
        _applying = true;
        _settings = settings;
        UiText.SetLanguage(settings.Language);
        LanguageChoice.SelectedItem = LanguageChoice.Items.Cast<ComboBoxItem>().First(item => item.Tag?.ToString() == settings.Language);
        DisplayModeChoice.SelectedItem = DisplayModeChoice.Items.Cast<ComboBoxItem>().First(item => item.Tag?.ToString() == settings.DisplayMode);
        TaskbarOffset.Value = settings.TaskbarOffset;
        WorkHoursEnabled.IsChecked = settings.WorkHoursEnabled;
        WorkBreakEnabled.IsChecked = settings.WorkBreakEnabled;
        WorkStart.SelectedIndex = settings.WorkStartMinute / 15;
        WorkEnd.SelectedIndex = settings.WorkEndMinute / 15;
        WorkBreakStart.SelectedIndex = settings.WorkBreakStartMinute / 15;
        WorkBreakEnd.SelectedIndex = settings.WorkBreakEndMinute / 15;
        foreach (var day in WorkDaysPanel.Children.OfType<System.Windows.Controls.CheckBox>())
            day.IsChecked = settings.WorkDays.Contains(int.Parse(day.Tag.ToString()!));
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
        _settings.DisplayMode = (DisplayModeChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "pet";
        _settings.TaskbarOffset = TaskbarOffset.Value;
        _settings.WorkHoursEnabled = WorkHoursEnabled.IsChecked == true;
        _settings.WorkDays = WorkDaysPanel.Children.OfType<System.Windows.Controls.CheckBox>().Where(day => day.IsChecked == true).Select(day => int.Parse(day.Tag.ToString()!)).ToArray();
        _settings.WorkStartMinute = Math.Max(0, WorkStart.SelectedIndex) * 15;
        _settings.WorkEndMinute = Math.Max(0, WorkEnd.SelectedIndex) * 15;
        _settings.WorkBreakEnabled = WorkBreakEnabled.IsChecked == true;
        _settings.WorkBreakStartMinute = Math.Max(0, WorkBreakStart.SelectedIndex) * 15;
        _settings.WorkBreakEndMinute = Math.Max(0, WorkBreakEnd.SelectedIndex) * 15;
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
        var taskbar = _settings.DisplayMode == "taskbar";
        WorkHoursOptions.Visibility = _settings.WorkHoursEnabled ? Visibility.Visible : Visibility.Collapsed;
        WorkBreakStart.IsEnabled = WorkBreakEnd.IsEnabled = _settings.WorkBreakEnabled;
        var invalid = _settings.WorkDays.Length == 0 || _settings.WorkStartMinute == _settings.WorkEndMinute || _settings.WorkBreakEnabled && _settings.WorkBreakStartMinute == _settings.WorkBreakEndMinute;
        WorkHoursSummary.Text = UiText.T(invalid ? "请选择工作日和不同的起止时间；无有效时段时暂停预测。" : "下班后应剩不再下降；今日建议按今天剩余工作时间分配。休息时段仅扣除与工作重叠的部分。");
        TaskbarOptions.Visibility = taskbar ? Visibility.Visible : Visibility.Collapsed;
        Alignment.IsEnabled = HorizontalOffset.IsEnabled = VerticalOffset.IsEnabled = Scale.IsEnabled = PotionGap.IsEnabled = !taskbar;
        TaskbarOffsetValue.Text = $"{TaskbarOffset.Value:0}px";
        RefreshMinutesValue.Text = UiText.F("每 {0:0} 分钟", RefreshMinutes.Value);
        ScaleValue.Text = $"{Scale.Value * 100:0}%";
        HorizontalOffsetValue.Text = $"{HorizontalOffset.Value:+0;-0;0}px";
        VerticalOffsetValue.Text = $"{VerticalOffset.Value:+0;-0;0}px";
        PotionGapValue.Text = $"{PotionGap.Value:0}px";
    }

    public void ClosePermanently() { _allowClose = true; Close(); }
    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } }
}
