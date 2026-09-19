namespace CodexPetLimitRings.Windows;

public sealed record QuotaTargets(double? EvenRemaining, double? CloseRemaining, DateTimeOffset? Deadline, bool DayOff, bool ResetBeforeClose)
{
    public string Compact(bool fresh) => UiText.F(DayOff ? "匀速 {0} · 休息 {1}" : "匀速 {0} · 下班 {1}",
        Percent(fresh ? EvenRemaining : null), Percent(fresh ? CloseRemaining : null));
    public static string Percent(double? value) => value is { } number ? $"{number:0.#}%" : "—";
}

public static class WeeklyTargets
{
    // Targets are schedule benchmarks; actual quota spending must not move them.
    public static QuotaTargets Calculate(long? resetAt, DateTimeOffset now, OverlaySettings? settings, TimeZoneInfo? zone = null)
    {
        var empty = new QuotaTargets(null, null, null, false, false);
        if (resetAt is null) return empty;
        DateTimeOffset reset;
        try { reset = DateTimeOffset.FromUnixTimeSeconds(resetAt.Value); }
        catch (ArgumentOutOfRangeException) { return empty; }
        var left = (reset - now).TotalSeconds;
        if (left <= 0 || left > 7 * 86400) return empty;
        var even = Math.Clamp(left / (7 * 86400) * 100, 0, 100);
        if (settings is null || settings.WorkStartMinute == settings.WorkEndMinute ||
            settings.WorkBreakEnabled && settings.WorkBreakStartMinute == settings.WorkBreakEndMinute)
            return empty with { EvenRemaining = even };
        zone ??= TimeZoneInfo.Local;
        var start = reset.AddDays(-7);
        var intervals = WorkSchedule.Build(start, reset, settings, zone);
        var total = WorkSchedule.Seconds(intervals, start, reset);
        if (total <= 0) return empty with { EvenRemaining = even };
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var date = local.Date;
        var overnight = settings.WorkEndMinute < settings.WorkStartMinute;
        var previousShift = overnight && settings.WorkDays.Contains((int)date.AddDays(-1).DayOfWeek);
        var previousEnd = WorkSchedule.Instant(date.AddMinutes(settings.WorkEndMinute), zone, true);
        var todayShift = settings.WorkDays.Contains((int)date.DayOfWeek);
        // An active overnight shift retains its closing target across midnight.
        // On an off-day immediately following it, retain that same closing target.
        var usePrevious = previousShift && (now < previousEnd || !todayShift);
        var dayOff = !todayShift && !usePrevious;
        var end = usePrevious ? previousEnd : dayOff
            ? WorkSchedule.Instant(date.AddDays(1), zone, false)
            : WorkSchedule.Instant(date.AddDays(overnight ? 1 : 0).AddMinutes(settings.WorkEndMinute), zone, true);
        var deadline = end > reset ? reset : end;
        var future = WorkSchedule.Seconds(intervals, deadline < start ? start : deadline, reset);
        return new(even, Math.Clamp(future / total * 100, 0, 100), deadline, dayOff, end > reset);
    }
}
