using System.Globalization;

namespace CodexPetLimitRings.Windows;

internal static class DailySchedule
{
    internal static string Key(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    internal static string Fingerprint(OverlaySettings settings, long? reset) =>
        string.Join(';', settings.WeeklyWorkHours.OrderBy(p=>p.Key)) + "|" +
        (reset is {} id && settings.CycleWorkHours.TryGetValue(id,out var cycle) ? string.Join(';',cycle.OrderBy(p=>p.Key)) : "");

    internal static DailyWorkHours Get(OverlaySettings settings, DateTime date, long? reset = null)
    {
        if (reset is { } id && settings.CycleWorkHours.TryGetValue(id, out var cycle) && cycle.TryGetValue(Key(date), out var daily)) return daily;
        if (settings.WeeklyWorkHours.TryGetValue((int)date.DayOfWeek, out var weekly)) return weekly;
        return new(settings.WorkDays.Contains((int)date.DayOfWeek), settings.WorkStartMinute, settings.WorkEndMinute);
    }

    internal static IReadOnlyList<DateTime> Dates(long? reset, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        DateTimeOffset end;
        try { end = DateTimeOffset.FromUnixTimeSeconds(reset ?? 0); }
        catch (ArgumentOutOfRangeException) { return []; }
        if (end <= now || end > now.AddDays(7)) return [];
        var first = TimeZoneInfo.ConvertTime(end.AddDays(-7), zone).Date;
        // Exclude the new day if reset is exactly at midnight.
        var last = TimeZoneInfo.ConvertTime(end.AddTicks(-1), zone).Date;
        return Enumerable.Range(0, (last-first).Days+1).Select(i => first.AddDays(i)).ToArray();
    }

    private static Dictionary<string, DailyWorkHours> FreezeCycle(OverlaySettings settings, long reset, IReadOnlyList<DateTime> dates)
    {
        if (settings.CycleWorkHours.TryGetValue(reset, out var existing)) return existing;
        // Preserve the whole current cycle before changing defaults for future cycles.
        // Include yesterday so an overnight carry-in cannot change accidentally.
        var snapshot = dates.Prepend(dates[0].AddDays(-1)).ToDictionary(Key, date => Get(settings,date));
        settings.CycleWorkHours[reset] = snapshot;
        return snapshot;
    }

    internal static bool Set(OverlaySettings settings, long reset, DateTime date, DailyWorkHours value, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        var dates = Dates(reset,now,zone);
        if (!dates.Contains(date.Date)) return false;
        var cycle = FreezeCycle(settings,reset,dates);
        cycle[Key(date)] = value.Normalize();
        settings.WeeklyWorkHours[(int)date.DayOfWeek] = value.Normalize();
        return true;
    }

    internal static int CopyToRemaining(OverlaySettings settings, long reset, DateTime sourceDate, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var dates = Dates(reset,now,zone);
        if (!dates.Contains(sourceDate.Date)) return 0;
        var source = Get(settings,sourceDate,reset);
        if (!source.Working || source.StartMinute==source.EndMinute) return 0;
        var today = TimeZoneInfo.ConvertTime(now,zone).Date;
        var cycle = FreezeCycle(settings,reset,dates);
        var count = 0;
        foreach (var date in dates.Where(d => d>=today && d!=sourceDate.Date))
        {
            if (!Get(settings,date,reset).Working) continue;
            cycle[Key(date)] = source;
            settings.WeeklyWorkHours[(int)date.DayOfWeek] = source;
            count++;
        }
        return count;
    }
}
