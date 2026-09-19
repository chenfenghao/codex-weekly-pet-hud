namespace CodexPetLimitRings.Windows;

internal readonly record struct WorkInterval(DateTimeOffset Start, DateTimeOffset End);

internal static class WorkSchedule
{
    // Working days name the shift's starting date. A Monday 22:00–02:00 shift
    // therefore includes early Tuesday, even if Tuesday is not selected.
    internal static List<WorkInterval> Build(DateTimeOffset start, DateTimeOffset end, OverlaySettings settings, TimeZoneInfo zone)
    {
        var result = new List<WorkInterval>();
        if (settings.WorkStartMinute == settings.WorkEndMinute || settings.WorkDays is null) return result;
        var first = TimeZoneInfo.ConvertTime(start, zone).Date.AddDays(-1);
        var last = TimeZoneInfo.ConvertTime(end, zone).Date;
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            if (!settings.WorkDays.Contains((int)date.DayOfWeek)) continue;
            var localStart = date.AddMinutes(settings.WorkStartMinute);
            var localEnd = date.AddMinutes(settings.WorkEndMinute);
            if (localEnd <= localStart) localEnd = localEnd.AddDays(1);
            var shiftStart = Instant(localStart, zone, false);
            var shiftEnd = Instant(localEnd, zone, true);
            void Add(DateTimeOffset a, DateTimeOffset b)
            {
                a = a < start ? start : a; b = b > end ? end : b;
                if (b > a) result.Add(new(a, b));
            }
            if (settings.WorkBreakEnabled && settings.WorkBreakStartMinute != settings.WorkBreakEndMinute)
            {
                var pieces = new List<WorkInterval> { new(shiftStart, shiftEnd) };
                for (var breakDate = date.AddDays(-1); breakDate <= localEnd.Date; breakDate = breakDate.AddDays(1))
                {
                    var localBreakStart = breakDate.AddMinutes(settings.WorkBreakStartMinute);
                    var localBreakEnd = breakDate.AddMinutes(settings.WorkBreakEndMinute);
                    if (localBreakEnd <= localBreakStart) localBreakEnd = localBreakEnd.AddDays(1);
                    var a = Instant(localBreakStart, zone, false);
                    var b = Instant(localBreakEnd, zone, true);
                    var next = new List<WorkInterval>();
                    foreach (var piece in pieces)
                    {
                        if (a >= piece.End || b <= piece.Start) { next.Add(piece); continue; }
                        if (piece.Start < a) next.Add(new(piece.Start, a));
                        if (b < piece.End) next.Add(new(b, piece.End));
                    }
                    pieces = next;
                }
                foreach (var piece in pieces) Add(piece.Start, piece.End);
                continue;
            }
            Add(shiftStart, shiftEnd);
        }
        var merged = new List<WorkInterval>();
        foreach (var interval in result.OrderBy(value => value.Start))
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End)
                merged[^1] = new(merged[^1].Start, interval.End > merged[^1].End ? interval.End : merged[^1].End);
            else merged.Add(interval);
        }
        return merged;
    }

    internal static double Seconds(IEnumerable<WorkInterval> intervals, DateTimeOffset from, DateTimeOffset to) =>
        intervals.Sum(interval => Math.Max(0, ((interval.End < to ? interval.End : to) - (interval.Start > from ? interval.Start : from)).TotalSeconds));

    internal static DateTimeOffset? Consume(IEnumerable<WorkInterval> intervals, DateTimeOffset from, double seconds)
    {
        foreach (var interval in intervals)
        {
            var start = interval.Start > from ? interval.Start : from;
            var available = (interval.End - start).TotalSeconds;
            if (available <= 0) continue;
            if (seconds <= available) return start.AddSeconds(Math.Max(0, seconds));
            seconds -= available;
        }
        return null;
    }

    internal static DateTimeOffset Instant(DateTime local, TimeZoneInfo zone, bool end)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // Move nonexistent wall-clock times to the next valid minute at a DST gap.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local)
            ? (end ? zone.GetAmbiguousTimeOffsets(local).Min() : zone.GetAmbiguousTimeOffsets(local).Max())
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
