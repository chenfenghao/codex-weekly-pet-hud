using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexPetLimitRings.Windows.Views;
using Brushes = System.Windows.Media.Brushes;

namespace CodexPetLimitRings.Windows;

internal static class DailyScheduleSelfTest
{
    internal static void Run(Action<bool,string> check,string directory)
    {
        var zone=TimeZoneInfo.CreateCustomTimeZone("DailyTest",TimeSpan.FromHours(8),"DailyTest","DailyTest");
        DateTimeOffset At(int day,int hour)=>new(2026,10,day,hour,0,0,zone.BaseUtcOffset);
        var now=At(4,10);var reset=At(8,12).ToUnixTimeSeconds();
        OverlaySettings Settings()=>new() { WorkHoursEnabled=true,WorkDays=[1,2,3,4,5,6],WorkStartMinute=480,WorkEndMinute=1260,RefreshMinutes=17 };
        var settings=Settings();var dates=DailySchedule.Dates(reset,now,zone);
        check(dates.Count==8 && dates[0].Day==1 && dates[0].DayOfWeek==DayOfWeek.Thursday && dates[^1].Day==8,"Dates follow reset cycle, not a fixed weekday");
        check(DailySchedule.Dates(At(8,0).ToUnixTimeSeconds(),now,zone).Count==7,"Midnight reset excludes empty boundary date");
        check(DailySchedule.Dates(reset,At(8,12),zone).Count==0,"Expired reset cannot edit an invented cycle");
        check(DailySchedule.Dates(null,now,zone).Count==0,"Missing reset has no artificial dates");
        check(DailySchedule.Get(settings,new DateTime(2026,10,5),reset)==new DailyWorkHours(true,480,1260),"Legacy work schedule is inherited");
        check(DailySchedule.Set(settings,reset,new DateTime(2026,10,5),new(true,540,1020),now,zone),"Can edit a single date");
        var plan=QuotaVisualModel.Build(reset,now,settings,zone)!;
        check(Math.Abs(plan.Days.Sum(d=>d.Hours)-73)<1e-8,"Independent shorter Monday changes total from 78 to 73 hours");
        check(Math.Abs(plan.Days.Sum(d=>d.Allocation??0)-100)<1e-8,"Daily allocations still sum to 100 percent");
        var target=WeeklyTargets.Calculate(reset,At(5,10),settings,zone);
        check(target.Deadline==At(5,17) && Math.Abs(target.CloseRemaining!.Value-3000d/73)<1e-8,"Closing target uses today's independent end time");
        check(WeeklyPacing.Calculate(50,reset,At(5,17),settings,zone).DailyBudget==0,"Budget ends at the independent closing time");
        check(DailySchedule.Get(settings,new DateTime(2026,10,12))==new DailyWorkHours(true,540,1020),"Next week inherits weekday hours");
        DailySchedule.Set(settings,reset,dates[^1],new(true,600,720),now,zone);
        check(DailySchedule.Get(settings,dates[0],reset).StartMinute==480,"Same weekday at opposite boundaries stays independent");
        var past=DailySchedule.Get(settings,dates[1],reset);
        var copied=DailySchedule.CopyToRemaining(settings,reset,new DateTime(2026,10,5),now,zone);
        check(copied==3,"Copy only targets remaining workdays");
        check(DailySchedule.Get(settings,dates[1],reset)==past && !DailySchedule.Get(settings,new DateTime(2026,10,4),reset).Working,"Copy preserves past days and days off");
        check(DailySchedule.Get(settings,new DateTime(2026,10,6),reset).EndMinute==1020,"Copy applies the chosen end time");
        var loaded=JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(settings))!;loaded.Normalize();
        check(DailySchedule.Get(loaded,dates[^1],reset)==DailySchedule.Get(settings,dates[^1],reset) && loaded.RefreshMinutes==17,"Per-date schedule persists without changing refresh");
        var overnight=Settings();
        DailySchedule.Set(overnight,reset,new DateTime(2026,10,5),new(true,1320,120),now,zone);
        DailySchedule.Set(overnight,reset,new DateTime(2026,10,6),new(true,540,1080),now,zone);
        check(WeeklyTargets.Calculate(reset,At(6,1),overnight,zone).Deadline==At(6,2),"Yesterday's independent overnight end remains active after midnight");
        check(WeeklyTargets.Calculate(reset,At(6,10),overnight,zone).Deadline==At(6,18),"Next shift uses its own different end time");
        check(CycleDayEditor.Parse("8:35",out var minute) && minute==515 && !CycleDayEditor.Parse("24:30",out _) && !CycleDayEditor.Parse("08:60",out _),"Typed times validate hour and minute ranges");

        var original=UiText.Instance.Language;
        try
        {
            foreach(var language in new[]{"zh-CN","en"})
            {
                UiText.SetLanguage(language);
                var uiNow=DateTimeOffset.Now;var uiReset=uiNow.AddDays(4).ToUnixTimeSeconds();
                var uiSettings=Settings();var editor=new CycleDayEditor();editor.Update(uiSettings,uiReset,uiNow);
                var changes=0;editor.Changed+=()=>changes++;
                var row=editor.Rows.First(r=>r.Date>=uiNow.LocalDateTime.Date);
                row.Start.Text="09:15";row.End.Text="18:30";editor.Commit(row);
                check(DailySchedule.Get(uiSettings,row.Date,uiReset).StartMinute==555 && changes>0,"Typing a time changes that date and emits save event");
                var before=DailySchedule.Get(uiSettings,row.Date,uiReset);row.End.Text="99:99";
                check(!editor.Commit(row) && DailySchedule.Get(uiSettings,row.Date,uiReset)==before,"Invalid input cannot overwrite the schedule");
                row.End.Text="18:30";editor.Commit(row);
                row.Working.IsChecked=false;
                check(!row.Start.IsEnabled && !DailySchedule.Get(uiSettings,row.Date,uiReset).Working,"One click makes a day off and disables time inputs");
                row.Working.IsChecked=true;
                row.Copy.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                check(editor.Rows.Where(r=>r.Date>=uiNow.LocalDateTime.Date && r.Working.IsChecked==true).All(r=>r.Start.Text=="09:15"),"Copy button updates remaining working rows");
                var host=new Border { Background=Brushes.White,Padding=new Thickness(16),Child=editor };
                host.Measure(new System.Windows.Size(460,double.PositiveInfinity));var height=host.DesiredSize.Height;host.Arrange(new Rect(0,0,460,height));host.UpdateLayout();
                check(editor.Rows.All(r=>r.Start.ActualWidth>=60 && r.Label.ActualWidth>=90),"Both languages fit in compact settings width");
                var bitmap=new RenderTargetBitmap(920,(int)Math.Ceiling(height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(host);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,$"daily-schedule.{language}.png")))encoder.Save(file);
                host.Child=null;
            }
        }
        finally { UiText.SetLanguage(original); }
    }
}
