using System.Text.Json;

namespace CodexPetLimitRings.Windows;

internal static class WorkPacingSelfTest
{
    internal static void Run(Action<bool, string> check)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("WorkTest", TimeSpan.FromHours(8), "WorkTest", "WorkTest");
        DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 9, day, hour, minute, 0, zone.BaseUtcOffset);
        var settings = new OverlaySettings { WorkHoursEnabled = true, WorkDays = [1,2,3,4,5,6], WorkStartMinute = 420, WorkEndMinute = 1260 };
        var reset = At(21, 7).ToUnixTimeSeconds();
        PaceResult Calc(double left, DateTimeOffset now) => WeeklyPacing.Calculate(left, reset, now, settings, zone);
        void Near(double? actual, double expected, string name) => check(actual is { } value && Math.Abs(value - expected) < 0.00001, name);
        Near(WorkSchedule.Seconds(WorkSchedule.Build(At(14,7), At(21,7), settings, zone), At(14,7), At(21,7)) / 3600, 84, "Six 14-hour shifts");
        Near(Calc(70, At(15,21)).TimePercent, 100d/3, "Two shifts out of six");
        Near(Calc(70, At(15,23)).TimePercent, Calc(70, At(16,7)).TimePercent!.Value, "Target freezes overnight");
        Near(Calc(70, At(15,21)).DailyBudget, 0, "No budget after today's shift");
        Near(Calc(70, At(15,7)).DailyBudget, 14, "Remaining balance across five workdays");
        Near(Calc(45, At(17,7)).DailyBudget, 15, "Three workdays at fifteen percent");
        Near(Calc(45, At(17,14)).DailyBudget, 9, "Partial working day budget");
        check(Calc(60, At(16,1)).Ratio > Calc(70, At(15,23)).Ratio, "Off-hours spending still raises pace ratio");
        check(Calc(70, At(15,7)).Prediction.Contains(UiText.Date(At(17,11,40).DateTime)), "Exhaustion skips nights");
        check(Calc(10, At(20,12)).Status == "工作已结束" && Calc(10,At(20,12)).DailyBudget == 0, "No work before reset does not divide by zero");
        check(Calc(80, At(21,7)).Status == "待确认重置", "Reset still requires confirmation");
        check(Calc(100, At(14,7)).Ratio is null, "No pace before first work minute");
        check(Calc(0, At(15,7)).Status == "已耗尽", "Exhausted during work mode");
        var allDay = WeeklyPacing.Calculate(70, reset, At(15,7));
        settings.WorkHoursEnabled = false;
        check(WeeklyPacing.Calculate(70, reset, At(15,7), settings,zone) == allDay, "All-day mode retains prior behavior");
        settings.WorkHoursEnabled = true;
        settings.WorkBreakEnabled = true; settings.WorkBreakStartMinute = 720; settings.WorkBreakEndMinute = 780;
        Near(WorkSchedule.Seconds(WorkSchedule.Build(At(14,7),At(21,7),settings,zone),At(14,7),At(21,7))/3600,78,"Lunch excluded six times");
        Near(Calc(70,At(15,12,10)).TimePercent,Calc(70,At(15,12,50)).TimePercent!.Value,"Target freezes during lunch");
        settings.WorkBreakEnabled = false;
        var midReset = At(21,12);
        var cut = WorkSchedule.Build(midReset.AddDays(-7), midReset,settings,zone);
        Near(WorkSchedule.Seconds(cut,midReset.AddDays(-7),midReset)/3600,84,"Mid-shift reset clips both ends");
        settings.WorkDays = [1]; settings.WorkStartMinute=22*60; settings.WorkEndMinute=2*60;
        var nightReset = At(21,12);
        var night = WeeklyPacing.Calculate(80, nightReset.ToUnixTimeSeconds(), At(14,23), settings,zone);
        Near(night.TimePercent,25,"Overnight shift uses start weekday");
        Near(night.DailyBudget,80d/3,"Today budget stops at midnight during overnight shift");
        Near(WeeklyPacing.Calculate(80,nightReset.ToUnixTimeSeconds(),At(15,1),settings,zone).TimePercent,75,"Unselected next day still contains overnight shift");
        settings.WorkBreakEnabled=true; settings.WorkBreakStartMinute=30; settings.WorkBreakEndMinute=60;
        Near(WorkSchedule.Seconds(WorkSchedule.Build(At(14,12),nightReset,settings,zone),At(14,12),nightReset)/3600,3.5,"Post-midnight break excluded");
        settings.WorkDays=[];
        check(WeeklyPacing.Calculate(80, reset,At(15,7),settings,zone).Status=="检查作息","Empty working days pause predictions");
        settings.WorkDays=[1]; settings.WorkStartMinute=settings.WorkEndMinute;
        check(WeeklyPacing.Calculate(80, reset,At(15,7),settings,zone).Ratio is null,"Equal shift times are invalid, not 24 hours");
        var restored=JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(settings))!;
        restored.Normalize();
        check(restored.WorkHoursEnabled && restored.WorkDays.SequenceEqual(settings.WorkDays) && restored.WorkBreakStartMinute==30,"Work settings survive serialization");
        check(!JsonSerializer.Deserialize<OverlaySettings>("{}")!.WorkHoursEnabled,"Existing settings keep all-day mode");
        var eastern=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var dst=new OverlaySettings { WorkDays=[0], WorkStartMinute=60, WorkEndMinute=240 };
        foreach(var item in new[] {(date:new DateTime(2026,3,8), hours:2d), (date:new DateTime(2026,11,1), hours:4d)})
        {
            var a=WorkSchedule.Instant(item.date,eastern,false); var b=WorkSchedule.Instant(item.date.AddDays(1),eastern,true);
            Near(WorkSchedule.Seconds(WorkSchedule.Build(a,b,dst,eastern),a,b)/3600,item.hours,"DST uses elapsed duration without double counting");
        }
    }
}
