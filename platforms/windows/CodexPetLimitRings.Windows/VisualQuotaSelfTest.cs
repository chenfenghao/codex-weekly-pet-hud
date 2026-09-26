using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexPetLimitRings.Windows.Views;
using Color = System.Windows.Media.Color;

namespace CodexPetLimitRings.Windows;

internal static class VisualQuotaSelfTest
{
    internal static void Run(Action<bool,string> check, string directory)
    {
        var zone=TimeZoneInfo.CreateCustomTimeZone("VisualTest",TimeSpan.FromHours(8),"VisualTest","VisualTest");
        DateTimeOffset At(int day,int hour)=>new(2026,9,day,hour,0,0,zone.BaseUtcOffset);
        var settings=new OverlaySettings { WorkDays=[1,2,3,4,5,6],WorkStartMinute=420,WorkEndMinute=1260,WorkHoursEnabled=true };
        var reset=At(21,7).ToUnixTimeSeconds(); var now=At(16,12);
        var plan=QuotaVisualModel.Build(reset,now,settings,zone)!;
        check(plan.Days.Count==8,"Seven-day cycle may span eight local calendar dates");
        check(Math.Abs(plan.Days.Sum(d=>d.Allocation??0)-100)<1e-8,"Daily allocations conserve 100 percent");
        check(Math.Abs(plan.Days.Sum(d=>d.Hours)-84)<1e-8,"Six full fourteen-hour shifts");
        check(plan.Days.Single(d=>d.Date.Day==20).Allocation==0,"Sunday has no allocated quota");
        check(plan.Curve.First().Remaining==100 && plan.Curve.Last().Remaining==0,"Plan spans full cycle");
        check(plan.Curve.Zip(plan.Curve.Skip(1)).All(p=>p.First.Remaining>=p.Second.Remaining),"Work curve never increases");
        var partial=QuotaVisualModel.Build(At(21,14).ToUnixTimeSeconds(),now,settings,zone)!;
        check(partial.Days[0].Hours==7 && partial.Days[^1].Hours==7,"Partial first and last shifts clipped");
        check(Math.Abs(partial.Days[0].Allocation!.Value-100d/12)<1e-8,"Half shift gets half daily quota");
        settings.WorkBreakEnabled=true;settings.WorkBreakStartMinute=720;settings.WorkBreakEndMinute=780;
        check(QuotaVisualModel.Build(reset,now,settings,zone)!.Days.Sum(d=>d.Hours)==78,"Breaks excluded from allocation");
        settings.WorkBreakEnabled=false;settings.WorkStartMinute=22*60;settings.WorkEndMinute=2*60;settings.WorkDays=[1];
        var overnight=QuotaVisualModel.Build(reset,now,settings,zone)!;
        check(overnight.Days.Where(d=>d.Hours>0).Count()==2 && overnight.Days.Sum(d=>d.Hours)==4,"Overnight shift split across dates without duplication");
        settings.WorkDays=[];
        check(QuotaVisualModel.Build(reset,now,settings,zone)!.Days.All(d=>d.Allocation is null),"Invalid schedule never invents allocations");
        check(QuotaVisualModel.Build(reset,At(21,7),settings,zone) is null,"Expired cycle has no plan");
        check(WorkScheduleEditor.Snap(48,400)==0 && WorkScheduleEditor.Snap(400,400)==1425,"Drag values clamp to allowed time range");
        check(WorkScheduleEditor.Snap(48+(400-62)*420d/1440,400)==420,"Drag snaps to fifteen minutes");
        var targets=new QuotaTargets(40,33,now,false,false);
        check(QuotaVisualModel.Available(48,targets)==15 && QuotaVisualModel.Available(30,targets)==-3,"Budget is actual balance minus closing target, retaining deficit");

        var historyPath=Path.Combine(directory,"test-history.json");
        var history=new QuotaHistory(historyPath);
        var usage=new UsageSnapshot(null,20,null,reset,"live",now);
        check(history.Record(usage),"Successful live read recorded");
        check(!history.Record(usage),"Duplicate observations rejected");
        check(!history.Record(usage with { Source="stale", ReadAt=now.AddMinutes(1) }),"Stale cache is not a new observation");
        check(history.Record(usage with { Source="manual",ReadAt=now.AddMinutes(2) }),"Manual observation preserved with source");
        check(new QuotaHistory(historyPath).Points.Count==2,"History persists across restarts");
        var a=new QuotaObservation(now,reset,80,"live"); var b=a with { At=now.AddMinutes(5),Remaining=79 };
        check(QuotaHistory.Connect(a,b,TimeSpan.FromMinutes(12)),"Consecutive live reads connect");
        check(!QuotaHistory.Connect(a,b with { Reset=reset+60 },TimeSpan.FromMinutes(12)),"Different reset cycles never connect");
        check(!QuotaHistory.Connect(a,b with { Remaining=90 },TimeSpan.FromMinutes(12)),"Refill never appears as continued consumption");
        check(!QuotaHistory.Connect(a,b with { At=now.AddHours(3) },TimeSpan.FromMinutes(12)),"Missing intervals stay blank");
        check(!QuotaHistory.Connect(a,b with { Source="manual" },TimeSpan.FromMinutes(12)),"Manual input is an isolated point");
        File.WriteAllText(historyPath,"invalid json");
        check(new QuotaHistory(historyPath).Points.Count==0,"Corrupt history does not prevent startup");
        File.Delete(historyPath);
        settings.WorkDays=[1,2,3,4,5,6];settings.WorkStartMinute=420;settings.WorkEndMinute=1260;
        var original=UiText.Instance.Language;
        foreach(var language in new[]{"zh-CN","en"})
        {
            UiText.SetLanguage(language);
            var sampleNow=DateTimeOffset.Now;var sampleReset=sampleNow.AddDays(3).ToUnixTimeSeconds();
            var sampleStart=DateTimeOffset.FromUnixTimeSeconds(sampleReset).AddDays(-7);
            var observations=Enumerable.Range(0,193).Select(i=>new QuotaObservation(sampleStart.AddMinutes(i*30),sampleReset,100-i*0.27,"live")).ToArray();
            var dashboard=new QuotaDashboard();dashboard.Update(sampleReset,sampleNow,settings,observations);
            Render(dashboard,580,408,Path.Combine(directory,$"dashboard.{language}.png"));
            var editor=new WorkScheduleEditor();editor.Update(settings);
            Render(editor,440,211,Path.Combine(directory,$"schedule.{language}.png"),true);
            var capsule=new PotionWindow("sample",default,default,default,default) { PacingSettings=settings };
            capsule.UpdateUsage(48,sampleReset,"live");
            capsule.UpdateResetSignal(UiText.T("重置雷达 · 暂无新信号"),"Sample data",false);
            var capsuleContent=(FrameworkElement)capsule.Content;capsule.Content=null;
            Render(capsuleContent,190,72,Path.Combine(directory,$"visual-capsule.{language}.png"));
            capsule.UpdateResetSignal(UiText.T("雷达 · 新重置信号"),"Sample data",true);
            Render(capsuleContent,190,72,Path.Combine(directory,$"visual-radar.{language}.png"));
            capsule.Close();
            var details=new UsageDetailsWindow { PacingSettings=settings, History=observations };
            details.ApplyLanguage();details.Update(new UsageSnapshot(null,52,null,sampleReset,"live",sampleNow),false);
            var detailContent=(FrameworkElement)details.Content; details.Content=null;
            Render(detailContent,640,820,Path.Combine(directory,$"visual-details.{language}.png"));
            details.ClosePermanently();
        }
        UiText.SetLanguage(original);
    }
    private static void Render(FrameworkElement content,double width,double height,string file,bool light=false)
    {
        var host=new System.Windows.Controls.Border { Background=new SolidColorBrush(light?Colors.White:Color.FromRgb(26,32,29)),Child=content };
        host.Measure(new System.Windows.Size(width,height));host.Arrange(new Rect(0,0,width,height));host.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)width*2,(int)height*2,192,192,PixelFormats.Pbgra32);bitmap.Render(host);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);
        host.Child=null;
    }
}
