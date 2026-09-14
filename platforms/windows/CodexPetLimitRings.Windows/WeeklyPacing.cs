using System.Globalization;
using System.Text.RegularExpressions;

namespace CodexPetLimitRings.Windows;

public sealed record PaceResult(double? TimePercent, double? Ratio, double? DailyBudget, string Status, string Prediction, string Color);

public static class WeeklyPacing
{
    public static PaceResult Calculate(double? remaining, long? resetAt, DateTimeOffset now)
    {
        if (remaining is null || !double.IsFinite(remaining.Value) || resetAt is null)
            return new(null, null, null, "等待数据", UiText.T("粘贴周额度与重置时间，开始记录。"), "#A1B2A7");
        DateTimeOffset reset;
        try { reset = DateTimeOffset.FromUnixTimeSeconds(resetAt.Value); }
        catch (ArgumentOutOfRangeException) { return new(null, null, null, "检查时间", UiText.T("重置时间无效。"), "#EDC788"); }
        var days = (reset - now).TotalDays;
        if (days <= 0) return new(100, null, null, "待确认重置", UiText.T("重置时间已到，请重新粘贴最新额度。"), "#EDC788");
        if (days > 7) return new(null, null, null, "检查时间", UiText.T("重置时间超过 7 天，暂停预测。"), "#EDC788");
        var left = Math.Clamp(remaining.Value, 0, 100);
        var elapsed = 7 - days;
        var time = elapsed / 7 * 100;
        var budget = Math.Min(left, left / days);
        if (left == 0) return new(time, time > 0 ? 100 / time : null, 0, "已耗尽", UiText.T("额度已耗尽，等待重置后更新。"), "#EE9D9D");
        if (elapsed * 86400 < 60) return new(time, null, budget, "刚刚开始", UiText.T("周期刚开始，满 1 分钟后估算使用速率。"), "#A1B2A7");
        var used = 100 - left;
        var ratio = used / time;
        var status = ratio < 0.85 ? "偏慢" : ratio <= 1 + 1e-9 ? "稳健" : ratio <= 1.3 + 1e-9 ? "偏快" : "超速";
        var color = ratio <= 1 + 1e-9 ? "#9BDFB5" : ratio <= 1.3 + 1e-9 ? "#EDC788" : "#EE9D9D";
        var prediction = UiText.T("按当前平均速度，可用到重置日。");
        if (ratio > 1 && used > 0)
        {
            var end = reset.AddDays(-7 + elapsed * 100 / used);
            prediction = UiText.F("预计 {0} 耗尽\n比重置提前 {1:0.#} 小时", UiText.Date(end.LocalDateTime), (reset - end).TotalHours);
        }
        return new(time, ratio, budget, status, prediction, color);
    }

    public static string Countdown(long? resetAt, DateTimeOffset now)
    {
        if (resetAt is null) return "—";
        try
        {
            var span = DateTimeOffset.FromUnixTimeSeconds(resetAt.Value) - now;
            return span <= TimeSpan.Zero ? UiText.T("时间已到，请更新") : UiText.F("{0}天 {1:00}:{2:00}:{3:00}", (int)span.TotalDays, span.Hours, span.Minutes, span.Seconds);
        }
        catch (ArgumentOutOfRangeException) { return UiText.T("无效时间"); }
    }
}

public static class WeeklyPasteParser
{
    private static Match Match(string text, string pattern) => Regex.Match(text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    public static UsageSnapshot Parse(string raw, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) throw new FormatException(UiText.T("请只粘贴周额度与重置时间。"));
        var text = raw.Normalize(System.Text.NormalizationForm.FormKC);
        var values = Regex.Matches(text, @"(?<before>剩余|可用|已用|已使用|remaining|used|available)?\s*[:：]?\s*(?<percent>-?\d+(?:\.\d+)?)\s*%\s*(?<after>remaining|left|used|available|剩余|可用)?", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        if (values.Count == 0) throw new FormatException(UiText.T("未识别到百分比，例如：剩余 80%。"));
        var usedValues = values.Select(m =>
        {
            var value = double.Parse(m.Groups["percent"].Value, CultureInfo.InvariantCulture);
            if (!double.IsFinite(value) || value < 0 || value > 100) throw new FormatException(UiText.T("百分比必须在 0–100 之间。"));
            return Match(m.Groups["before"].Value + m.Groups["after"].Value, "剩余|可用|remaining|left|available").Success ? 100 - value : value;
        }).Distinct().ToArray();
        if (usedValues.Length != 1) throw new FormatException(UiText.T("存在多个不同额度，请只复制周额度那一段。"));
        DateTimeOffset reset;
        var date = Match(text, @"(?<year>\d{4})[年/-](?<month>\d{1,2})[月/-](?<day>\d{1,2})日?\s*(?:T|\s)\s*(?<hour>\d{1,2}):(?<minute>\d{2})");
        if (date.Success)
        {
            int Part(string name) => int.Parse(date.Groups[name].Value, CultureInfo.InvariantCulture);
            try { reset = new DateTimeOffset(new DateTime(Part("year"), Part("month"), Part("day"), Part("hour"), Part("minute"), 0, DateTimeKind.Local)); }
            catch (ArgumentOutOfRangeException) { throw new FormatException(UiText.T("日期或时间无效，请检查后重试。")); }
        }
        else
        {
            var clock = Match(text, @"(\d+)\s*(?:d\b|天)\s*(\d{1,2}):(\d{2})(?::(\d{2}))?");
            if (clock.Success)
            {
                int Part(int index) => clock.Groups[index].Success ? int.Parse(clock.Groups[index].Value, CultureInfo.InvariantCulture) : 0;
                if (Part(1) > 7 || Part(2) > 23 || Part(3) > 59 || Part(4) > 59) throw new FormatException(UiText.T("倒计时无效。"));
                reset = now.AddDays(Part(1)).AddHours(Part(2)).AddMinutes(Part(3)).AddSeconds(Part(4));
            }
            else
            {
                var duration = Match(text, @"(?:(?<days>\d+)\s*(?:天|days?\b|d\b))\s*(?:(?<hours>\d+)\s*(?:小时|hours?\b|h\b))?");
                if (!duration.Success) throw new FormatException(UiText.T("请同时粘贴重置时间，例如：2026年9月19日 16:12。"));
                var days = double.Parse(duration.Groups["days"].Value, CultureInfo.InvariantCulture);
                var hours = duration.Groups["hours"].Success ? double.Parse(duration.Groups["hours"].Value, CultureInfo.InvariantCulture) : 0;
                if (days > 7 || hours > 23) throw new FormatException(UiText.T("倒计时无效。"));
                reset = now.AddDays(days).AddHours(hours);
            }
        }
        if (reset <= now || reset - now > TimeSpan.FromDays(7)) throw new FormatException(UiText.T("重置时间应在未来 7 天内。"));
        return new(null, usedValues[0], null, reset.ToUnixTimeSeconds(), "manual", now, 604800);
    }
}
