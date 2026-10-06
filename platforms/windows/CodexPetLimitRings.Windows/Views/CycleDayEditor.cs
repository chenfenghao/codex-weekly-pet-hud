using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Brushes = System.Windows.Media.Brushes;

namespace CodexPetLimitRings.Windows.Views;

public sealed class CycleDayEditor : StackPanel
{
    internal sealed record DayRow(DateTime Date, CheckBox Working, ComboBox Start, ComboBox End, Button Copy, TextBlock Label);
    internal List<DayRow> Rows { get; } = [];
    private readonly TextBlock _range = new() { FontSize=11, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,8) };
    private readonly StackPanel _days = new();
    private readonly TextBlock _hint = new() { FontSize=10, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,8,0,0), Foreground=Brushes.DimGray };
    private OverlaySettings _settings = new();
    private long? _reset;
    private DateTimeOffset? _testNow;
    private bool _updating;
    private string _structure = "";
    public event Action? Changed;
    private DateTimeOffset Now => _testNow ?? DateTimeOffset.Now;
    private static string L(string zh,string en) => UiText.IsEnglish ? en : zh;

    public CycleDayEditor() { Children.Add(_range); Children.Add(_days); Children.Add(_hint); }

    public void Update(OverlaySettings settings,long? reset,DateTimeOffset? testNow=null)
    {
        _settings=settings; _reset=reset; _testNow=testNow;
        var dates=DailySchedule.Dates(reset,Now);
        var structure=$"{reset}|{UiText.Instance.Language}|{dates.Count}";
        _updating=true;
        if(_structure!=structure)
        {
            _structure=structure; _days.Children.Clear(); Rows.Clear();
            foreach(var date in dates) AddRow(date);
        }
        _range.Text=dates.Count==0 ? L("读取有效重置时间后，这里会按周期显示每天。可先在更多设置中调整默认作息。", "Read a valid reset time to edit each date. Default hours are available under More settings.") :
            L("本周期 ","This cycle ")+DateTimeOffset.FromUnixTimeSeconds(reset!.Value).AddDays(-7).LocalDateTime.ToString("M/d HH:mm")+" → "+DateTimeOffset.FromUnixTimeSeconds(reset.Value).LocalDateTime.ToString("M/d HH:mm");
        foreach(var row in Rows)
        {
            var value=DailySchedule.Get(settings,row.Date,reset);
            row.Working.IsChecked=value.Working;
            row.Working.Content=L(value.Working?"工作":"休息", value.Working?"Work":"Off");
            row.Start.Text=Time(value.StartMinute); row.End.Text=Time(value.EndMinute);
            row.Start.IsEnabled=row.End.IsEnabled=row.Copy.IsEnabled=value.Working;
            row.Label.Text=row.Date.ToString(UiText.IsEnglish?"M/d ddd":"M/d ddd",UiText.Culture)+(row.Date==Now.LocalDateTime.Date?L(" · 今天"," · Today"):"");
        }
        _hint.Text=L("改完自动保存，下周期按星期沿用。复制仅用于今天起的其他工作日。", "Changes save automatically and repeat by weekday next cycle. Copy applies to other workdays from today onward.");
        _updating=false;
    }

    private void AddRow(DateTime date)
    {
        var grid=new Grid { Margin=new Thickness(0,3,0,3) };
        foreach(var width in new[]{double.NaN,54d,69d,12d,69d,44d})
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width=double.IsNaN(width)?new GridLength(1,GridUnitType.Star):new GridLength(width) });
        var label=new TextBlock { FontSize=11, VerticalAlignment=VerticalAlignment.Center, Foreground=date==Now.LocalDateTime.Date?QuotaInk.Color("#276946"):Brushes.DimGray };
        var working=new CheckBox { VerticalAlignment=VerticalAlignment.Center, FontSize=11 };
        ComboBox Clock(string name)
        {
            var box=new ComboBox { IsEditable=true, IsTextSearchEnabled=false, MaxDropDownHeight=220, FontSize=12, Height=28, Margin=new Thickness(1,0,1,0), ItemsSource=Enumerable.Range(0,96).Select(i=>Time(i*15)).ToArray() };
            AutomationProperties.SetName(box,date.ToString("M/d")+" "+name);
            box.ToolTip=name+L("（可直接输入，如 08:30）"," (type a time, e.g. 08:30)");
            return box;
        }
        var start=Clock(L("上班","Start"));var end=Clock(L("下班","End"));
        var copy=new Button { Content=L("复制","Copy"), FontSize=10, Background=QuotaInk.Color("#EAF1EC"), Foreground=QuotaInk.Color("#315541"), BorderBrush=QuotaInk.Color("#D5E1D9"), Padding=new Thickness(3,2,3,2), Margin=new Thickness(3,0,0,0), ToolTip=L("将这一行的时间复制给今天及之后的其他工作日；休息日和过去日期保持不变。", "Copy these hours to other workdays from today onward; keep days off and past dates unchanged.") };
        var row=new DayRow(date,working,start,end,copy,label); Rows.Add(row);
        void Add(UIElement item,int column) { Grid.SetColumn(item,column);grid.Children.Add(item); }
        Add(label,0);Add(working,1);Add(start,2);Add(new TextBlock {Text="–",VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=System.Windows.HorizontalAlignment.Center},3);Add(end,4);Add(copy,5);
        var border=new Border { CornerRadius=new CornerRadius(5),Padding=new Thickness(6,4,6,4),Background=date==Now.LocalDateTime.Date?QuotaInk.Color("#EDF5EF"):Brushes.Transparent,Child=grid };
        _days.Children.Add(border);
        working.Checked+=(_,_)=>Commit(row);working.Unchecked+=(_,_)=>Commit(row);
        foreach(var box in new[]{start,end})
        {
            box.SelectionChanged+=(_,_)=> { if(!_updating && box.SelectedItem is string text) {box.Text=text;Commit(row);} };
            box.LostKeyboardFocus+=(_,_)=>Commit(row);
            box.PreviewKeyDown+=(_,e)=> {if(e.Key==Key.Enter){Commit(row);e.Handled=true;}};
        }
        copy.Click+=(_,_)=>
        {
            if(_reset is not {} id || !Commit(row))return;
            var count=DailySchedule.CopyToRemaining(_settings,id,date,Now);
            Changed?.Invoke(); Update(_settings,_reset,_testNow);
            _hint.Text=UiText.IsEnglish?$"Copied to {count} workdays. Days off and past dates were kept.":$"已复制到 {count} 个工作日，休息日与过去日期保持不变。";
        };
    }

    internal bool Commit(DayRow row)
    {
        if(_updating || _reset is not {} id)return false;
        var validStart=Parse(row.Start.Text,out var a);var validEnd=Parse(row.End.Text,out var b);
        if(row.Working.IsChecked!=true && (!validStart || !validEnd))
        {
            var previous=DailySchedule.Get(_settings,row.Date,id);a=previous.StartMinute;b=previous.EndMinute;
            validStart=validEnd=true;
        }
        if(!validStart||!validEnd||(row.Working.IsChecked==true && a==b))
        {
            _hint.Text=L("请输入有效的上下班时间，例如 08:00–21:00；起止时间不能相同。", "Use valid, different start/end times, e.g. 08:00–21:00."); return false;
        }
        var value=new DailyWorkHours(row.Working.IsChecked==true,a,b);
        if(value!=DailySchedule.Get(_settings,row.Date,id))
        {
            if(!DailySchedule.Set(_settings,id,row.Date,value,Now))return false;
            Changed?.Invoke();
        }
        // Update only this row so entering one time never steals focus from another.
        _updating=true;
        row.Start.Text=Time(a); row.End.Text=Time(b);
        row.Start.IsEnabled=row.End.IsEnabled=row.Copy.IsEnabled=value.Working;
        row.Working.Content=L(value.Working?"工作":"休息",value.Working?"Work":"Off");
        _hint.Text=value.Working && b<a ? L("已保存，较早的下班时间表示次日下班。", "Saved. An earlier end time means the next day.") : L("已保存 · 下周期按星期沿用", "Saved · Repeats by weekday next cycle");
        _updating=false;return true;
    }
    internal static bool Parse(string text,out int minute)
    {
        minute=0;var parts=text.Trim().Split(':');
        if(parts.Length!=2||!int.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out var h)||!int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out var m)||h<0||h>23||m<0||m>59)return false;
        minute=h*60+m;return true;
    }
    private static string Time(int minute)=>$"{minute/60:00}:{minute%60:00}";
}
