using System.Text.Json;
using CodexPetLimitRings.Windows.Services;

namespace CodexPetLimitRings.Windows;

public sealed record QuotaDay(DateTime Date, DateTimeOffset Start, DateTimeOffset End, double Hours, double? Allocation, double? EndRemaining, string HoursLabel);
public sealed record QuotaPlan(DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<QuotaDay> Days, IReadOnlyList<QuotaCurvePoint> Curve);
public sealed record QuotaCurvePoint(DateTimeOffset At, double Remaining);
public sealed record QuotaObservation(DateTimeOffset At, long Reset, double Remaining, string Source);

public static class QuotaVisualModel
{
    public static QuotaPlan? Build(long? resetAt, DateTimeOffset now, OverlaySettings settings, TimeZoneInfo? zone = null)
    {
        if (WeeklyTargets.Calculate(resetAt, now, settings, zone).EvenRemaining is null) return null;
        zone ??= TimeZoneInfo.Local;
        var end = DateTimeOffset.FromUnixTimeSeconds(resetAt!.Value);
        var start = end.AddDays(-7);
        var work = WorkSchedule.Build(start, end, settings, zone);
        if (settings.WorkBreakEnabled && settings.WorkBreakStartMinute == settings.WorkBreakEndMinute) work.Clear();
        var total = WorkSchedule.Seconds(work, start, end);
        var days = new List<QuotaDay>();
        for (var date = TimeZoneInfo.ConvertTime(start, zone).Date; date < TimeZoneInfo.ConvertTime(end, zone).Date.AddDays(1); date = date.AddDays(1))
        {
            var a = WorkSchedule.Instant(date, zone, false); if (a < start) a = start;
            var b = WorkSchedule.Instant(date.AddDays(1), zone, false); if (b > end) b = end;
            if (b <= a) continue;
            var seconds = WorkSchedule.Seconds(work, a, b);
            var pieces = work.Where(w => w.Start < b && w.End > a).Select(w =>
                $"{TimeZoneInfo.ConvertTime(w.Start < a ? a : w.Start, zone):HH:mm}–{(w.End >= WorkSchedule.Instant(date.AddDays(1), zone, false) ? "24:00" : TimeZoneInfo.ConvertTime(w.End > b ? b : w.End, zone).ToString("HH:mm"))}");
            days.Add(new(date, a, b, seconds / 3600, total > 0 ? seconds / total * 100 : null,
                total > 0 ? WorkSchedule.Seconds(work, b, end) / total * 100 : null, string.Join(" / ", pieces)));
        }
        var curve = total > 0 ? work.SelectMany(w => new[] { w.Start, w.End }).Append(start).Append(end).Distinct().Order()
            .Select(at => new QuotaCurvePoint(at, WorkSchedule.Seconds(work, at, end) / total * 100)).ToArray() : [];
        return new(start, end, days, curve);
    }

    public static double? Available(double? balance, QuotaTargets targets) => balance is { } b && targets.CloseRemaining is { } c ? b - c : null;
    public static string BudgetText(double? balance, QuotaTargets targets, bool compact = false)
    {
        var gap = Available(balance, targets);
        if (gap is null) return UiText.IsEnglish ? "Budget —" : "今日 —";
        if (gap < 0) return UiText.IsEnglish ? $"Below {Math.Abs(gap.Value):0.#}pp" : $"低于目标 {Math.Abs(gap.Value):0.#}%";
        return UiText.IsEnglish ? $"{(compact ? "Today" : "Until close")} {gap:0.#}%" : $"{(compact ? "今日" : "到下班还可用")} {gap:0.#}%";
    }
}

// Stores observations only, never credentials. No timer-generated or backfilled points.
public sealed class QuotaHistory
{
    private readonly string _path;
    private List<QuotaObservation> _points = [];
    public IReadOnlyList<QuotaObservation> Points => _points;
    public QuotaHistory(string path)
    {
        _path = path;
        try { _points = (JsonSerializer.Deserialize<List<QuotaObservation>>(File.ReadAllText(path)) ?? []).Where(Valid).OrderBy(p => p.At).TakeLast(50000).ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
    }
    private static bool Valid(QuotaObservation p) => p is not null && double.IsFinite(p.Remaining) && p.Remaining is >= 0 and <= 100 &&
        p.Source is "live" or "manual" && p.Reset > p.At.ToUnixTimeSeconds() && p.Reset - p.At.ToUnixTimeSeconds() <= 604800;
    public bool Record(UsageSnapshot usage)
    {
        if (usage.SecondaryReset is not { } reset || usage.SecondaryRemaining is not { } remaining) return false;
        var point = new QuotaObservation(usage.ReadAt, reset, remaining, usage.Source);
        if (!Valid(point) || _points.Any(p => p.At == point.At && p.Reset == reset)) return false;
        _points.Add(point);
        _points = _points.Where(p => p.At >= usage.ReadAt.AddDays(-30)).OrderBy(p => p.At).TakeLast(50000).ToList();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_points));
            File.Move(_path + ".tmp", _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { AppLog.Write("Quota history could not be saved: " + e.GetType().Name); }
        return true;
    }
    public static bool Connect(QuotaObservation a, QuotaObservation b, TimeSpan gap) => a.Reset == b.Reset &&
        a.Source == "live" && b.Source == "live" && b.At > a.At && b.At - a.At <= gap && b.Remaining <= a.Remaining;
}
