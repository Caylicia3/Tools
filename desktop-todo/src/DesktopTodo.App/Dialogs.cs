using System.Globalization;
namespace DesktopTodo.App;
internal sealed class FormWindow : Window {
 internal readonly StackPanel Fields=new(){Margin=new Thickness(24,18,24,18)};
 internal readonly StackPanel Footer=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(15)};
 internal readonly Button SaveButton=new(){Content="保存",MinWidth=90};
 internal FormWindow(Window owner,string title,double width=470) {
  Title=title;Width=width;MaxHeight=Math.Min(850,SystemParameters.WorkArea.Height-60);SizeToContent=SizeToContent.Height;MinHeight=200;
  WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
  SetResourceReference(BackgroundProperty,"PanelBackground");SetResourceReference(ForegroundProperty,"PanelForeground");ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;
  if(owner is not PlannerWindow pw || !Native.IsAttached(pw)) {Owner=owner;WindowStartupLocation=WindowStartupLocation.CenterOwner;}
  Topmost=owner.Topmost;
  var root=new DockPanel();var head=new TextBlock{Text=title,FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new Thickness(24,18,24,0)};
  DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
  var cancel=new Button{Content="取消",MinWidth=75,IsCancel=true};cancel.Click+=(_,_)=>Close();Footer.Children.Add(cancel);Footer.Children.Add(SaveButton);
  DockPanel.SetDock(Footer,Dock.Bottom);root.Children.Add(Footer);
  root.Children.Add(new ScrollViewer{Content=Fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=MaxHeight-150});Content=root;
  KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};
 }
 internal T Field<T>(string label,T control) where T:UIElement {Fields.Children.Add(new TextBlock{Text=label,FontSize=12,Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.Wrap});Fields.Children.Add(control);return control;}
 internal TextBox Input(string label,string text="")=>Field(label,new TextBox{Text=text});
 internal DatePicker Date(string label,DateTime? date)=>Field(label,new DatePicker{SelectedDate=date});
 internal CheckBox Check(string text,bool check){var c=new CheckBox{Content=text,IsChecked=check};Fields.Children.Add(c);return c;}
 internal void Hint(string text){var block=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(0,3,0,10)};block.SetResourceReference(TextBlock.ForegroundProperty,"MutedForeground");Fields.Children.Add(block);}
 internal void Commit(Action action){SaveButton.Click+=(_,_)=>Try(()=>{action();DialogResult=true;});}
 internal void Try(Action action){try{action();}catch(Exception ex){if(ex is not InvalidDataException)App.Instance.Log(ex);MessageBox.Show(this,ex.Message,"请检查输入",MessageBoxButton.OK,MessageBoxImage.Warning);}}
}
internal static partial class Dialogs {
 static DateTime Required(DatePicker p,string label)=>p.SelectedDate?.Date??throw new InvalidDataException("请选择"+label+"。");
 static DateTime Time(DatePicker p,TextBox t,string label){
  if(!TimeSpan.TryParseExact(t.Text.Trim(),new[]{"h\\:mm","hh\\:mm"},CultureInfo.InvariantCulture,out var value)||value.TotalHours>=24||value.TotalHours<0)
   throw new InvalidDataException(label+"请填写 00:00 到 23:59 之间的时间。");
  return Required(p,label)+value;
 }
 static int Number(TextBox t,string label,int min,int max){if(!int.TryParse(t.Text,out int n)||n<min||n>max)throw new InvalidDataException(label+"需要填写 "+min+" 到 "+max+" 之间的整数。");return n;}
 internal static TextBox Color(FormWindow w,string current,string label="颜色（留空跟随主题）"){
  var panel=new DockPanel{Margin=new Thickness(0,4,0,4)};
  var pick=new Button{Content="选色",Padding=new Thickness(8,5,8,5)};DockPanel.SetDock(pick,Dock.Right);panel.Children.Add(pick);
  var swatch=new Border{Width=28,Height=28,Margin=new Thickness(0,0,8,0),BorderThickness=new Thickness(1),BorderBrush=PlannerWindow.Brush("#888888")};DockPanel.SetDock(swatch,Dock.Left);panel.Children.Add(swatch);
  var text=new TextBox{Text=current,Margin=new Thickness(0),VerticalContentAlignment=VerticalAlignment.Center,ToolTip="例如 #B7ABD4：# 后接 6 位数字或 A～F 字母；也可点击选色。"};panel.Children.Add(text);
  void Preview(){try{Validation.Color(text.Text.Trim());swatch.Background=text.Text.Trim()==""?Brushes.Transparent:PlannerWindow.Brush(text.Text.Trim());}catch(InvalidDataException){}}
  text.TextChanged+=(_,_)=>Preview();Preview();
  pick.Click+=(_,_)=>{
   using var dialog=new System.Windows.Forms.ColorDialog{FullOpen=true};
   try{if(text.Text!="")dialog.Color=System.Drawing.ColorTranslator.FromHtml(text.Text);}catch{}
   var owner=new DialogHandle(new System.Windows.Interop.WindowInteropHelper(w).Handle);
   if(dialog.ShowDialog(owner)==System.Windows.Forms.DialogResult.OK)text.Text="#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2");
  };
  w.Field(label,panel);w.Hint("例如 #B7ABD4：# 后接 6 位数字或 A～F 字母。也可直接点「选色」。");return text;
 }
 sealed record DialogHandle(IntPtr Handle):System.Windows.Forms.IWin32Window;
 internal sealed record ReminderFields(ComboBox Mode,TextBox Days,TextBox Time,int OriginalDays,int OriginalHour,int OriginalMinute) {
  internal (bool Enabled,int Days,int Hour,int Minute) Read(){
   bool daysValid=int.TryParse(Days.Text,out int days)&&days>=0&&days<=3650;
   bool timeValid=TimeSpan.TryParseExact(Time.Text.Trim(),new[]{"h\\:mm","hh\\:mm"},CultureInfo.InvariantCulture,out var time)&&time.TotalHours<24&&time.TotalHours>=0;
   if(Mode.SelectedIndex==1)return(false,daysValid?days:OriginalDays,timeValid?time.Hours:OriginalHour,timeValid?time.Minutes:OriginalMinute);
   if(!daysValid)throw new InvalidDataException("提前提醒天数需要填写 0 到 3650 之间的整数。");
   if(!timeValid)throw new InvalidDataException("提醒时间请填写 00:00 到 23:59，例如 09:30。");
   return(true,days,time.Hours,time.Minutes);
  }
 }
 static ReminderFields ReminderEditor(FormWindow w,bool enabled,int days,int hour,int minute,DatePicker date,string label){
  var mode=w.Field(label,new ComboBox{ItemsSource=new[]{"提醒","不提醒"},SelectedIndex=enabled?0:1});
  System.Windows.Automation.AutomationProperties.SetName(mode,"提醒方式");
  var before=w.Input("提前几天（0 表示当天，最多 3650 天）",days.ToString());var clock=w.Input("提醒时间（HH:mm，例如 09:30）",hour.ToString("00")+":"+minute.ToString("00"));
  System.Windows.Automation.AutomationProperties.SetName(before,"提前提醒天数");System.Windows.Automation.AutomationProperties.SetName(clock,"提醒时刻");
  var preview=new TextBlock{FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};preview.SetResourceReference(TextBlock.ForegroundProperty,"MutedForeground");w.Fields.Children.Add(preview);
  var fields=new ReminderFields(mode,before,clock,days,hour,minute);
  void Refresh(){
   before.IsEnabled=clock.IsEnabled=mode.SelectedIndex==0;
   if(mode.SelectedIndex==1){preview.Text="不提醒";return;}
   try{var r=fields.Read();var at=Required(date,"提醒基准日期").AddDays(-r.Days).AddHours(r.Hour).AddMinutes(r.Minute);preview.Text="实际提醒："+at.ToString("yyyy/MM/dd HH:mm")+(at<DateTime.Now?"（此时间已过去）":"");}
   catch(InvalidDataException ex){preview.Text=ex.Message;}catch(ArgumentOutOfRangeException){preview.Text="日期超出提醒支持范围，请选择 1900 年及以后的日期。";}
  }
  before.TextChanged+=(_,_)=>Refresh();clock.TextChanged+=(_,_)=>Refresh();date.SelectedDateChanged+=(_,_)=>Refresh();mode.SelectionChanged+=(_,_)=>Refresh();Refresh();return fields;
 }
 internal static void Event(PlannerWindow owner,Occurrence? occurrence,DateTime start,DateTime? end=null) {
  var existing=occurrence?.Source;
  var draft=existing==null?new CalendarEvent{Start=start,End=end??start.AddHours(1)}:Copies.Clone(existing);
  if(occurrence!=null){draft.Start=occurrence.Start;draft.End=occurrence.End;}
  var w=new FormWindow(owner,existing==null?"添加日程":"编辑日程");
  var title=w.Input("日程名称",draft.Title);
  var startDate=w.Date("开始日期",draft.Start);var startTime=w.Input("开始时间（HH:mm）",draft.Start.ToString("HH:mm"));
  var endDate=w.Date("结束日期",draft.End);var endTime=w.Input("结束时间（HH:mm）",draft.End.ToString("HH:mm"));
  var location=w.Input("地点（可选）",draft.Location);
  var notes=w.Input("备注（可选）",draft.Notes);notes.AcceptsReturn=true;notes.MinHeight=55;notes.TextWrapping=TextWrapping.Wrap;
  var color=Color(w,draft.Color);
  var weekly=w.Check("每周在这一天重复",draft.Weekly);
  var until=w.Date("重复结束日期（留空表示持续重复）",draft.RepeatUntil);
  var reminder=w.Input("提前多少分钟提醒（-1 表示不提醒）",draft.ReminderMinutes.ToString());
  ComboBox? scope=null;
  if(existing?.Weekly==true){scope=w.Field("这次修改应用于",new ComboBox{ItemsSource=new[]{"仅本次","本次及以后"},SelectedIndex=0});}
  if(existing!=null){
   var delete=new Button{Content="删除",Foreground=PlannerWindow.Brush("#A43F4F")};
   delete.Click+=(_,_)=>w.Try(()=>{if(MessageBox.Show(w,"确定删除"+(scope?.SelectedIndex==1?"本次及以后的日程":"这次日程")+"？","删除日程",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;owner.Store.Edit("删除日程",s=>CalendarLogic.DeleteOccurrence(s,occurrence!,scope?.SelectedIndex==1));w.DialogResult=true;});
   w.Footer.Children.Insert(0,delete);
  }
  w.Commit(()=>{
   draft.Title=title.Text.Trim();draft.Start=Time(startDate,startTime,"开始");draft.End=Time(endDate,endTime,"结束");draft.Location=location.Text.Trim();draft.Notes=notes.Text;draft.Color=color.Text.Trim();draft.Weekly=weekly.IsChecked==true;draft.RepeatUntil=until.SelectedDate?.Date;draft.ReminderMinutes=Number(reminder,"提前分钟",-1,525600);
   owner.Store.Edit(occurrence==null?"添加日程":"修改日程",s=>{if(occurrence==null)s.Events.Add(draft);else CalendarLogic.EditOccurrence(s,occurrence,draft,scope?.SelectedIndex==1);});
  });
  w.Loaded+=(_,_)=>title.Focus();w.ShowDialog();
 }
 internal static void Task(PlannerWindow owner,TodoTask? existing) {
  var draft=existing==null?new TodoTask():Copies.Clone(existing);
  var w=new FormWindow(owner,existing==null?"添加任务":"编辑任务",500);
  var title=w.Input("任务名称",draft.Title);var start=w.Date("开始日期",draft.Start);var end=w.Date("截止日期（含当天）",draft.End);
  var color=Color(w,draft.Color);
  var notes=w.Input("备注",draft.Notes);notes.AcceptsReturn=true;notes.MinHeight=55;notes.TextWrapping=TextWrapping.Wrap;
  var completed=w.Check("任务已完成",draft.Completed);
  var reminderFields=ReminderEditor(w,draft.Reminder,draft.ReminderDaysBefore,draft.ReminderHour,draft.ReminderMinute,end,"任务截止提醒");
  w.Hint("此选项只控制任务截止提醒。各个节点可独立设置提醒或不提醒。");
  w.Hint("任务日期修改后，已有节点仍需处于起止日期内。时间轴长条仅用于点击编辑，不能拖动。");
  var nodes=new StackPanel();w.Field("任务节点",nodes);
  void Refresh(){
   nodes.Children.Clear();
   foreach(var m in draft.Milestones.OrderBy(m=>m.Date).ToArray()){
    var row=new DockPanel{Margin=new Thickness(0,3,0,3)};
    var remove=new Button{Content="删除",Padding=new Thickness(7,3,7,3)};DockPanel.SetDock(remove,Dock.Right);remove.Click+=(_,_)=>{draft.Milestones.Remove(m);Refresh();};row.Children.Add(remove);
    var edit=new Button{Content="编辑",Padding=new Thickness(7,3,7,3)};DockPanel.SetDock(edit,Dock.Right);edit.Click+=(_,_)=>{w.Try(()=>{draft.Start=Required(start,"任务开始日期");draft.End=Required(end,"任务截止日期");if(Milestone(w,draft,m)!=null)Refresh();});};row.Children.Add(edit);
    row.Children.Add(new TextBlock{Text=(m.Completed?"✓ ":"◇ ")+m.Date.ToString("MM/dd")+"  "+m.Title,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});nodes.Children.Add(row);
   }
   if(draft.Milestones.Count==0)nodes.Children.Add(new TextBlock{Text="还没有节点，可添加初稿、检查、提交等步骤。",FontSize=11,TextWrapping=TextWrapping.Wrap});
  }
  Refresh();var add=new Button{Content="＋ 添加节点",HorizontalAlignment=HorizontalAlignment.Left};w.Fields.Children.Add(add);
  add.Click+=(_,_)=>w.Try(()=>{draft.Start=Required(start,"开始日期");draft.End=Required(end,"截止日期");if(Milestone(w,draft,null)!=null)Refresh();});
  if(existing!=null){var delete=new Button{Content="删除任务",Foreground=PlannerWindow.Brush("#A43F4F")};delete.Click+=(_,_)=>w.Try(()=>{if(MessageBox.Show(w,"删除任务及其全部节点？","删除任务",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;owner.Store.Edit("删除任务及节点",s=>s.Tasks.RemoveAll(t=>t.Id==draft.Id));w.DialogResult=true;});w.Footer.Children.Insert(0,delete);}
  w.Commit(()=>{
   draft.Title=title.Text.Trim();draft.Start=Required(start,"开始日期");draft.End=Required(end,"截止日期");draft.Notes=notes.Text;draft.Color=color.Text.Trim();draft.Completed=completed.IsChecked==true;var r=reminderFields.Read();draft.Reminder=r.Enabled;draft.ReminderDaysBefore=r.Days;draft.ReminderHour=r.Hour;draft.ReminderMinute=r.Minute;
   owner.Store.Edit(existing==null?"添加任务":"修改任务",s=>{var i=s.Tasks.FindIndex(t=>t.Id==draft.Id);if(i<0)s.Tasks.Add(draft);else s.Tasks[i]=draft;});
  });
  w.Loaded+=(_,_)=>title.Focus();w.ShowDialog();
 }
 static Milestone? Milestone(Window owner,TodoTask task,Milestone? existing,DateTime? proposed=null) {
  var draft=existing==null?new Milestone{Date=proposed??task.Start}:Copies.Clone(existing);
  var w=new FormWindow(owner,existing==null?"添加节点":"编辑节点");
  var title=w.Input("节点名称",draft.Title);var date=w.Date("节点日期",draft.Date);
  w.Hint("任务范围："+task.Start.ToString("yyyy/MM/dd")+" — "+task.End.ToString("yyyy/MM/dd"));
  var notes=w.Input("备注",draft.Notes);notes.AcceptsReturn=true;notes.MinHeight=55;
  var done=w.Check("这个节点已完成",draft.Completed);var reminderFields=ReminderEditor(w,draft.Reminder,draft.ReminderDaysBefore,draft.ReminderHour,draft.ReminderMinute,date,"节点提醒");
  Milestone? result=null;
  w.Commit(()=>{
   draft.Title=title.Text.Trim();draft.Date=Required(date,"节点日期");draft.Notes=notes.Text;draft.Completed=done.IsChecked==true;var r=reminderFields.Read();draft.Reminder=r.Enabled;draft.ReminderDaysBefore=r.Days;draft.ReminderHour=r.Hour;draft.ReminderMinute=r.Minute;
   if(string.IsNullOrWhiteSpace(draft.Title))throw new InvalidDataException("请填写节点名称。");
   if(draft.Date<task.Start.Date||draft.Date>task.End.Date)throw new InvalidDataException("节点日期必须在任务起止日期内。");
   int index=task.Milestones.FindIndex(m=>m.Id==draft.Id);if(index<0)task.Milestones.Add(draft);else task.Milestones[index]=draft;result=draft;
  });
  w.ShowDialog();return result;
 }
 internal static void EditSavedMilestone(PlannerWindow owner,TodoTask task,Milestone? existing,DateTime? proposed=null){
  var draft=Copies.Clone(task);
  if(Milestone(owner,draft,existing,proposed)!=null)owner.Run(()=>owner.Store.Edit(existing==null?"添加节点":"修改节点",s=>s.Tasks[s.Tasks.FindIndex(t=>t.Id==draft.Id)]=draft));
 }
}

