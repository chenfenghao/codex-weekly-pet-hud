using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using FlowDirection = System.Windows.FlowDirection;

namespace CodexPetLimitRings.Windows.Views;

internal static class QuotaInk
{
    internal static Brush Color(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    internal static readonly Brush Text = Color("#DFEAE3"), Muted = Color("#95ADA0"), Blue = Color("#81BEF9"), Green = Color("#9BDFB5"), Track = Color("#2B3B32");
    internal static string L(string zh, string en) => UiText.IsEnglish ? en : zh;
    internal static void Label(DrawingContext dc, string text, double x, double y, double size = 11, Brush? brush = null, double max = 1000)
    {
        var f = new FormattedText(text, UiText.Culture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), size, brush ?? Text, 1);
        f.MaxTextWidth = Math.Max(1, max); f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(f, new Point(x, y));
    }
}

public sealed class QuotaGauge : FrameworkElement
{
    private double? _balance;
    private QuotaTargets _targets = new(null, null, null, false, false);
    private bool _fresh;
    private string _language = "";
    public void Update(double? balance, QuotaTargets targets, bool fresh)
    {
        if (_balance == balance && _targets == targets && _fresh == fresh && _language == UiText.Instance.Language) return;
        _language = UiText.Instance.Language;
        _balance = balance; _targets = targets; _fresh = fresh;
        ToolTip = targets.Compact(fresh) + "\n" + QuotaVisualModel.BudgetText(fresh ? balance : null, targets);
        AutomationProperties.SetName(this, ToolTip.ToString()); InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        var width = Math.Max(1, ActualWidth - 6); var y = ActualHeight / 2;
        dc.DrawRoundedRectangle(QuotaInk.Track, null, new Rect(3, y - 2, width, 4), 2, 2);
        Brush fill = QuotaInk.Muted;
        if (_fresh && _balance is { } b && _targets.EvenRemaining is { } e && _targets.CloseRemaining is { } c)
            fill = b >= Math.Max(e,c) ? QuotaInk.Green : b < Math.Min(e,c) ? QuotaInk.Color("#F29A9A") : QuotaInk.Color("#EDCB85");
        if (_balance is { } balance)
        {
            var x = 3 + width * Math.Clamp(balance, 0, 100) / 100;
            dc.DrawRoundedRectangle(fill, null, new Rect(3, y - 2, Math.Max(0, x - 3), 4), 2, 2);
            dc.DrawEllipse(fill, new Pen(QuotaInk.Track, 1), new Point(x, y), 3, 3);
        }
        if (!_fresh) return;
        // Opposite tick directions keep coincident targets distinguishable.
        if (_targets.EvenRemaining is { } even) { var x = 3 + width * even / 100; dc.DrawLine(new Pen(Brushes.WhiteSmoke, 2), new Point(x,y-6),new Point(x,y+1)); }
        if (_targets.CloseRemaining is { } close) { var x = 3 + width * close / 100; dc.DrawLine(new Pen(QuotaInk.Blue, 2),new Point(x,y-1),new Point(x,y+6)); }
    }
}

public sealed class QuotaDashboard : FrameworkElement
{
    public bool AllocationOnly { get; set; }
    private QuotaPlan? _plan;
    private DateTimeOffset _now;
    private QuotaTargets _targets = new(null,null,null,false,false);
    private IReadOnlyList<QuotaObservation> _history = [];
    private TimeSpan _gap;
    private string _cache = "";
    public void Update(long? reset, DateTimeOffset now, OverlaySettings settings, IReadOnlyList<QuotaObservation>? history = null)
    {
        var key = $"{DailySchedule.Fingerprint(settings,reset)}|{reset}|{now.ToUnixTimeSeconds()/30}|{UiText.Instance.Language}|{settings.WorkStartMinute}|{settings.WorkEndMinute}|{string.Join(',',settings.WorkDays)}|{settings.WorkBreakEnabled}|{settings.WorkBreakStartMinute}|{settings.WorkBreakEndMinute}|{history?.Count}|{history?.LastOrDefault()}|{settings.RefreshMinutes}";
        if (_cache == key) return;
        _cache = key; _now = now; _plan = QuotaVisualModel.Build(reset, now, settings);
        _targets = WeeklyTargets.Calculate(reset,now,settings); _history = history ?? [];
        _gap = settings.RefreshInterval * 2 + TimeSpan.FromMinutes(2);
        AutomationProperties.SetName(this, _plan is null ? QuotaInk.L("等待有效重置时间", "Waiting for a valid reset") :
            string.Join("; ", _plan.Days.Select(d => $"{d.Date:M/d}: {d.Hours:0.#}h, {QuotaTargets.Percent(d.Allocation)}, {d.HoursLabel}")));
        InvalidateVisual();
    }
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_plan is null) return;
        var p = e.GetPosition(this);
        if (p.Y >= 28 && p.Y <= 158)
        {
            var index = Math.Clamp((int)(p.X / (ActualWidth / _plan.Days.Count)),0,_plan.Days.Count-1);
            var d = _plan.Days[index];
            ToolTip = $"{d.Date:yyyy-MM-dd}\n{d.HoursLabel}\n{QuotaInk.L("计划分配", "Planned allocation")} {QuotaTargets.Percent(d.Allocation)}\n{QuotaInk.L("日末应留", "End-of-day target")} {QuotaTargets.Percent(d.EndRemaining)}";
        }
        else if (!AllocationOnly && p.Y >= 220 && p.Y <= 354)
        {
            var at = _plan.Start.AddSeconds(Math.Clamp((p.X-32)/Math.Max(1,ActualWidth-44),0,1)*(_plan.End-_plan.Start).TotalSeconds);
            var sample = _history.Where(h => h.Reset == _plan.End.ToUnixTimeSeconds()).MinBy(h => Math.Abs((h.At-at).TotalSeconds));
            ToolTip = sample is null ? QuotaInk.L("尚无实际记录", "No observations yet") :
                $"{QuotaInk.L("最近记录", "Nearest observation")}: {UiText.Date(sample.At.LocalDateTime)}\n{sample.Remaining:0.#}% · {QuotaInk.L(sample.Source == "manual" ? "手动导入" : "自动读取", sample.Source == "manual" ? "Manual import" : "Automatic read")}";
        }
        else ToolTip = null;
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(QuotaInk.Color("#18251E"),null,new Rect(0,0,ActualWidth,ActualHeight),10,10);
        if (_plan is null) { QuotaInk.Label(dc,QuotaInk.L("等待有效重置时间后显示本周期分配", "A valid reset time is needed to show the plan"),12,20,12,max:ActualWidth-24); return; }
        var plan = _plan;
        QuotaInk.Label(dc,QuotaInk.L("本周期 · 计划分配", "This cycle · Planned allocation"),12,8,12);
        var cell = ActualWidth / plan.Days.Count;
        var max = Math.Max(1, plan.Days.Max(d => d.Allocation ?? 0));
        for (var i=0;i<plan.Days.Count;i++)
        {
            var d=plan.Days[i]; var x=i*cell+4; var w=cell-8; var today=d.Date==_now.LocalDateTime.Date;
            dc.DrawRoundedRectangle(QuotaInk.Color(today ? "#294738" : "#1D2D24"), today ? new Pen(QuotaInk.Green,1) : null,new Rect(x,32,w,122),6,6);
            QuotaInk.Label(dc,d.Date.ToString(UiText.IsEnglish ? "ddd" : "ddd",UiText.Culture),x+5,38,10,today?QuotaInk.Green:QuotaInk.Muted,w-10);
            QuotaInk.Label(dc,d.Date.ToString("M/d"),x+5,53,11,max:w-10);
            var height=(d.Allocation??0)/max*30;
            dc.DrawRoundedRectangle(d.Hours>0?QuotaInk.Blue:QuotaInk.Track,null,new Rect(x+7,100-height,w-14,Math.Max(2,height)),2,2);
            QuotaInk.Label(dc,d.Allocation is null ? "—" : d.Hours>0?$"{d.Allocation:0.#}%":QuotaInk.L("休息","Off"),x+5,105,12,d.Hours>0?QuotaInk.Text:QuotaInk.Muted,w-10);
            QuotaInk.Label(dc,$"{d.Hours:0.#}h",x+5,127,10,QuotaInk.Muted,w-10);
            QuotaInk.Label(dc,QuotaInk.L("留 ", "Keep ")+QuotaTargets.Percent(d.EndRemaining),x+5,140,9,QuotaInk.Muted,w-10);
        }
        QuotaInk.Label(dc,QuotaInk.L("7 天周期，按日期分列；首尾及夜班按实际时长拆分。悬停查看时间与日末目标。", "7-day cycle, split by local date. Hover for hours and end-of-day targets."),12,164,10,QuotaInk.Muted,ActualWidth-24);
        if (AllocationOnly) return;
        QuotaInk.Label(dc,QuotaInk.L("余额走势", "Balance over time"),12,190,12);
        QuotaInk.Label(dc,QuotaInk.L("灰 匀速    蓝 工作计划    绿 实际读取 · 空心为手动", "Gray Even    Blue Work plan    Green Observed · Hollow = manual"),12,209,10,QuotaInk.Muted,ActualWidth-24);
        var left=32d; var right=ActualWidth-12; var top=241d; var bottom=354d;
        double X(DateTimeOffset at)=>left+(at-plan.Start).TotalSeconds/(plan.End-plan.Start).TotalSeconds*(right-left);
        double Y(double value)=>bottom-value/100*(bottom-top);
        foreach(var value in new[]{0,50,100}) { dc.DrawLine(new Pen(QuotaInk.Track,1),new(left,Y(value)),new(right,Y(value))); QuotaInk.Label(dc,value.ToString(),3,Y(value)-6,9,QuotaInk.Muted); }
        dc.PushClip(new RectangleGeometry(new Rect(left-3,top-3,right-left+6,bottom-top+6)));
        dc.DrawLine(new Pen(QuotaInk.Muted,1){DashStyle=DashStyles.Dash},new(left,top),new(right,bottom));
        for(var i=1;i<plan.Curve.Count;i++) dc.DrawLine(new Pen(QuotaInk.Blue,2),new(X(plan.Curve[i-1].At),Y(plan.Curve[i-1].Remaining)),new(X(plan.Curve[i].At),Y(plan.Curve[i].Remaining)));
        var points=_history.Where(h=>h.Reset==plan.End.ToUnixTimeSeconds() && h.At>=plan.Start && h.At<=_now && h.At<=plan.End).OrderBy(h=>h.At).ToArray();
        for(var i=0;i<points.Length;i++)
        {
            var p=points[i]; var location=new Point(X(p.At),Y(p.Remaining));
            if(i>0 && QuotaHistory.Connect(points[i-1],p,_gap)) dc.DrawLine(new Pen(QuotaInk.Green,2),new(X(points[i-1].At),Y(points[i-1].Remaining)),location);
            dc.DrawEllipse(p.Source=="manual"?QuotaInk.Color("#18251E"):QuotaInk.Green,new Pen(QuotaInk.Green,1),location,p.Source=="manual"?3:1.5,p.Source=="manual"?3:1.5);
        }
        dc.DrawLine(new Pen(Brushes.WhiteSmoke,1){DashStyle=DashStyles.Dot},new(X(_now),top),new(X(_now),bottom));
        if(_targets.Deadline is {} end) dc.DrawLine(new Pen(QuotaInk.Blue,1){DashStyle=DashStyles.Dot},new(X(end),top),new(X(end),bottom));
        dc.Pop();
        QuotaInk.Label(dc,QuotaInk.L("现在", "Now"),Math.Clamp(X(_now)-12,left,right-36),226,9);
        if (_targets.Deadline is {} deadline)
            QuotaInk.Label(dc,_targets.ResetBeforeClose ? QuotaInk.L("重置", "Reset") : _targets.DayOff ? QuotaInk.L("日末", "End") : QuotaInk.L("下班", "Close"),Math.Clamp(X(deadline)-12,left,right-36),Math.Abs(X(deadline)-X(_now))<42 ? 248 : 226,9,QuotaInk.Blue);
        QuotaInk.Label(dc,plan.Start.LocalDateTime.ToString("M/d HH:mm"),left,360,10,QuotaInk.Muted);
        QuotaInk.Label(dc,QuotaInk.L("重置 ","Reset ")+plan.End.LocalDateTime.ToString("M/d HH:mm"),right-115,360,10,QuotaInk.Muted);
        QuotaInk.Label(dc,points.Length==0 ? QuotaInk.L("尚无实际记录；成功读取或粘贴后开始绘制。", "No observations yet; recording starts with a successful read or paste.") : QuotaInk.L("实际线只连接连续读取；离线、补额与手动记录留空。工作线按当前作息计算。", "Gaps, refills and manual entries stay unconnected. Work line uses the current schedule."),12,382,10,QuotaInk.Muted,ActualWidth-24);
    }
}

public sealed class WorkScheduleEditor : FrameworkElement
{
    private OverlaySettings _settings = new();
    private int _drag;
    public event Action<int?,int?,int?>? Edited;
    public void Update(OverlaySettings settings) { _settings=settings; InvalidateVisual(); }
    internal static int Snap(double x, double width) => (int)Math.Clamp(Math.Round((x-48)/Math.Max(1,width-62)*96)*15,0,1425);
    protected override void OnRender(DrawingContext dc)
    {
        var width=Math.Max(1,ActualWidth-62);
        dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,ActualWidth,ActualHeight));
        foreach(var h in new[]{0,6,12,18,24}) QuotaInk.Label(dc,$"{h:00}",48+width*h/24-5,0,10,QuotaInk.Color("#657C70"));
        // Drawing a representative week also shows the previous night's continuation.
        var monday=new DateTime(2026,9,14); var zone=TimeZoneInfo.Local;
        var intervals=WorkSchedule.Build(WorkSchedule.Instant(monday,zone,false),WorkSchedule.Instant(monday.AddDays(7),zone,false),_settings,zone);
        for(var i=0;i<7;i++)
        {
            var day=(i+1)%7; var y=29+i*27; var selected=_settings.WorkDays.Contains(day); var date=monday.AddDays(i);
            QuotaInk.Label(dc,UiText.T(new[]{"周日","周一","周二","周三","周四","周五","周六"}[day]),0,y-7,11,QuotaInk.Color(selected?"#254F39":"#7A8780"),45);
            dc.DrawRoundedRectangle(QuotaInk.Color("#E6EDE8"),null,new Rect(48,y-5,width,10),4,4);
            foreach(var w in intervals)
            {
                var a=w.Start.LocalDateTime<date?date:w.Start.LocalDateTime; var b=w.End.LocalDateTime>date.AddDays(1)?date.AddDays(1):w.End.LocalDateTime;
                if(b<=a)continue;
                dc.DrawRoundedRectangle(QuotaInk.Color("#71AF8B"),null,new Rect(48+width*(a-date).TotalMinutes/1440,y-5,width*(b-a).TotalMinutes/1440,10),3,3);
            }
            if(selected)
            {
                foreach(var minute in new[]{_settings.WorkStartMinute,_settings.WorkEndMinute})
                    dc.DrawEllipse(Brushes.White,new Pen(QuotaInk.Color("#315F46"),2),new Point(48+width*minute/1440,y),4,7);
            }
        }
        ToolTip=QuotaInk.L("点击星期切换工作日；拖动圆点调整所有工作日的统一上下班时间。跨夜下班点表示次日时间。", "Click a weekday to toggle it. Drag handles to change shared shift times. Overnight end handles refer to the next day.");
        AutomationProperties.SetName(this,ToolTip.ToString());
    }
    internal bool BeginEdit(Point p)
    {
        var row=(int)Math.Round((p.Y-29)/27);
        if(row<0||row>6)return false;
        var day=(row+1)%7;
        if(p.X<45) { Edited?.Invoke(day,null,null); return true; }
        if(!_settings.WorkDays.Contains(day))return false;
        var start=48+(ActualWidth-62)*_settings.WorkStartMinute/1440;
        var end=48+(ActualWidth-62)*_settings.WorkEndMinute/1440;
        if(Math.Min(Math.Abs(p.X-start),Math.Abs(p.X-end))>12)return false;
        _drag=Math.Abs(p.X-start)<=Math.Abs(p.X-end)?1:2; return true;
    }
    internal void DragTo(double x)
    {
        if(_drag==0)return;
        var minute=Snap(x,ActualWidth);
        Edited?.Invoke(null,_drag==1?minute:null,_drag==2?minute:null);
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!BeginEdit(e.GetPosition(this))) return;
        if (_drag!=0) CaptureMouse(); e.Handled=true;
    }
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e); if(_drag==0)return;
        DragTo(e.GetPosition(this).X); e.Handled=true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); if(_drag==0)return; _drag=0; ReleaseMouseCapture(); e.Handled=true; }
    protected override void OnLostMouseCapture(System.Windows.Input.MouseEventArgs e) { base.OnLostMouseCapture(e); _drag=0; }
}
