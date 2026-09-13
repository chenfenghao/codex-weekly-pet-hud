using System.Text.Json;

namespace CodexPetLimitRings.Windows.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexWeeklyPetHud");
    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public string AlertStatePath => Path.Combine(DataDirectory, "alert-state.json");
    public string ManualUsagePath => Path.Combine(DataDirectory, "manual-usage.json");
    public string LatestUsagePath => Path.Combine(DataDirectory, "latest-usage.json");
    public string ResetSignalPath => Path.Combine(DataDirectory, "reset-radar.json");
    public ResetSignalState LoadResetSignals() => Read<ResetSignalState>(ResetSignalPath) ?? new();
    public void SaveResetSignals(ResetSignalState value) => Write(ResetSignalPath, value);
    public UsageSnapshot? LoadLatestUsage()
    {
        var value = Read<UsageSnapshot>(LatestUsagePath);
        return value is { SecondaryUsed: >= 0 and <= 100, SecondaryReset: not null } ? value : null;
    }
    public void SaveLatestUsage(UsageSnapshot value) => Write(LatestUsagePath, value);
    public UsageSnapshot? LoadManualUsage()
    {
        var value = Read<UsageSnapshot>(ManualUsagePath);
        return value is { Source: "manual", SecondaryUsed: >= 0 and <= 100, SecondaryReset: not null } ? value : null;
    }
    public void SaveManualUsage(UsageSnapshot value) => Write(ManualUsagePath, value);
    public void ClearManualUsage()
    {
        try { File.Delete(ManualUsagePath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppLog.Write("Manual usage cleanup failed: " + error.Message); }
    }

    public OverlaySettings LoadSettings()
    {
        var settings = Read<OverlaySettings>(SettingsPath) ?? new OverlaySettings();
        settings.Normalize();
        return settings;
    }

    public AlertDeliveryState LoadAlertState()
    {
        var state = Read<AlertDeliveryState>(AlertStatePath) ?? new AlertDeliveryState();
        state.Normalize();
        return state;
    }
    public void SaveSettings(OverlaySettings value) { value.Normalize(); Write(SettingsPath, value); }
    public void SaveAlertState(AlertDeliveryState value) => Write(AlertStatePath, value);

    private static T? Read<T>(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions);
        }
        catch { return default; }
    }

    private void Write<T>(string path, T value)
    {
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(temporary, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Settings write failed: {error.Message}");
            try { File.Delete(temporary); } catch { }
        }
    }
}
