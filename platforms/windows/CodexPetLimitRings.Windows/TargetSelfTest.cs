namespace CodexPetLimitRings.Windows;

internal static class TargetSelfTest
{
    internal static void Run(Action<bool, string> check)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("TargetTest", TimeSpan.FromHours(8), "TargetTest", "TargetTest");
        DateTimeOffset At(int day, int hour) => new(2026, 9, day, hour, 0, 0, zone.BaseUtcOffset);
        var settings = new OverlaySettings { WorkHoursEnabled = true, WorkDays = [1,2,3,4,5,6], WorkStartMinute = 420, WorkEndMinute = 1260 };
        var reset = At(21,7).ToUnixTimeSeconds();
        QuotaTargets Calc(DateTimeOffset now) => WeeklyTargets.Calculate(reset,now,settings,zone);
        void Near(double? actual, double expected, string label) => check(actual is { } value && Math.Abs(value-expected)<0.000001,label);
        var morning=Calc(At(15,7)); var evening=Calc(At(15,20));
        Near(morning.EvenRemaining,600d/7,"Even target counts all remaining calendar time");
        Near(morning.CloseRemaining,200d/3,"Closing target leaves four shifts out of six");
        Near(evening.CloseRemaining,morning.CloseRemaining!.Value,"Closing target remains fixed during work");
        Near(Calc(At(15,23)).CloseRemaining,morning.CloseRemaining.Value,"Closing target remains fixed after work");
        check(evening.EvenRemaining<morning.EvenRemaining,"Even target moves during day");
        check(morning.Deadline==At(15,21) && !morning.DayOff,"Deadline is today's actual closing time");
        settings.WorkHoursEnabled=false;
        check(Calc(At(15,7))==morning,"Dual targets independent of pace mode");
        settings.WorkBreakEnabled=true;
        Near(Calc(At(15,7)).CloseRemaining,200d/3,"Daily break excluded from both total and future hours");
        settings.WorkBreakEnabled=false;
        var early=WeeklyTargets.Calculate(At(15,12).ToUnixTimeSeconds(),At(15,7),settings,zone);
        Near(early.CloseRemaining,0,"Reset before closing clips target to this cycle");
        check(early.ResetBeforeClose && early.Deadline==At(15,12),"Reset deadline disclosed");
        check(Calc(At(20,7)).DayOff,"Sunday uses day-off label");
        Near(Calc(At(20,23)).CloseRemaining,Calc(At(20,7)).CloseRemaining!.Value,"Day-off target remains fixed");
        check(Calc(At(21,7)).EvenRemaining is null && Calc(At(21,7)).CloseRemaining is null,"Expired cycle clears both targets");
        check(WeeklyTargets.Calculate(null,At(15,7),settings,zone).EvenRemaining is null,"Missing reset has no targets");
        check(WeeklyTargets.Calculate(long.MaxValue,At(15,7),settings,zone).CloseRemaining is null,"Invalid timestamp has no targets");
        check(WeeklyTargets.Calculate(At(23,7).ToUnixTimeSeconds(),At(15,7),settings,zone).EvenRemaining is null,"Beyond-seven-days reset rejected");
        settings.WorkDays=[];
        check(Calc(At(15,7)).EvenRemaining is not null && Calc(At(15,7)).CloseRemaining is null,"Invalid schedule preserves valid calendar benchmark");
        settings.WorkDays=[1,2]; settings.WorkStartMinute=22*60; settings.WorkEndMinute=2*60;
        var nightReset=At(21,12).ToUnixTimeSeconds();
        var night=WeeklyTargets.Calculate(nightReset,At(14,23),settings,zone);
        var afterMidnight=WeeklyTargets.Calculate(nightReset,At(15,1),settings,zone);
        check(night.CloseRemaining==afterMidnight.CloseRemaining && night.Deadline==At(15,2) && afterMidnight.Deadline==night.Deadline,"Active overnight target survives midnight");
        Near(night.CloseRemaining,50,"Overnight target preserves next shift");
        check(WeeklyTargets.Calculate(nightReset,At(15,10),settings,zone).Deadline==At(16,2),"After overnight shift use today's upcoming shift");
    }
}
