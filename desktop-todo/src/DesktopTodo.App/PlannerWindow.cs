using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace DesktopTodo.App;
internal partial class PlannerWindow : Window {
 internal bool AllowClose;
 internal bool TasksOnly { get; }
 internal DateTime SelectedDate=DateTime.Today;
 internal bool MonthView;
 internal DateTime TimelineStart=DateTime.Today.AddDays(-4);
 internal int TimelineDays=>App.Instance.Appearance.TimelineDays;
 internal bool ShowCompleted=true;
 bool calendarCollapsed,timelineCollapsed,loaded,rendering;
 bool followToday=true; DateTime observedToday=DateTime.Today;
 double weekScrollX,weekScrollY=8*52,monthScrollX,monthScrollY,timelineScroll;
 internal double CalendarZoom=>MonthView?App.Instance.Appearance.MonthZoom:App.Instance.Appearance.WeekZoom;
 internal bool NodeDragging;
 readonly Border frame=new();
 readonly Grid content=new();
 readonly DispatcherTimer resizeDebounce=new(){Interval=TimeSpan.FromMilliseconds(160)};
 internal Store Store=>App.Instance.Store;
 internal Palette Palette=>Palette.For(App.Instance.Appearance);
 internal PlannerWindow(bool tasksOnly) {
  TasksOnly=tasksOnly;Title=tasksOnly?"日程管理 · 任务时间轴":"日程管理 · 周计划";
  using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)){if(icon!=null)Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());}
  FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
  WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResizeWithGrip;AllowsTransparency=true;Background=Brushes.Transparent;
  ShowInTaskbar=true;MinWidth=tasksOnly?620:740;MinHeight=tasksOnly?290:500;
  var p=Store.Current.Settings;
  Width=tasksOnly?p.TaskWidth:p.Width;Height=tasksOnly?p.TaskHeight:p.Height;
  Left=tasksOnly?p.TaskLeft:p.Left;Top=tasksOnly?p.TaskTop:p.Top;
  Native.PlaceOnScreen(this);
  frame.CornerRadius=new CornerRadius(16);frame.BorderThickness=new Thickness(1);frame.Padding=new Thickness(18,12,18,12);frame.Child=content;Content=frame;
  Store.Changed+=OnChanged;
  Loaded+=(_,_)=>{loaded=true;Render();ApplyWindowMode();};
  resizeDebounce.Tick+=(_,_)=>{resizeDebounce.Stop();Render();SaveBounds();};
  SizeChanged+=(_,_)=>{if(loaded){resizeDebounce.Stop();resizeDebounce.Start();}};
  Closing+=(_,e)=>{SaveBounds();if(!AllowClose&&!App.Instance.Exiting){e.Cancel=true;Dispatcher.BeginInvoke(App.Instance.ExitApp);}};
  Closed+=(_,_)=>{Store.Changed-=OnChanged;resizeDebounce.Stop();};
  PreviewKeyDown+=(_,e)=>{if((e.Key==Key.System&&e.SystemKey==Key.F4)||(e.Key==Key.F4&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0)){e.Handled=true;App.Instance.ExitApp();}};
  KeyDown+=(_,e)=>{
   if(Keyboard.FocusedElement is TextBoxBase)return;
   if((Keyboard.Modifiers&ModifierKeys.Control)!=0){
    if(e.Key==Key.Z){Run(()=>{if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)Store.Redo();else Store.Undo();});e.Handled=true;}
    else if(e.Key==Key.Y){Run(Store.Redo);e.Handled=true;}
    else if(e.Key==Key.N){if(TasksOnly)EditTask(null);else EditEvent(null,SelectedDate.Date.AddHours(9));e.Handled=true;}
   }
  };
 }
 void OnChanged(){ if(!Dispatcher.CheckAccess()){Dispatcher.Invoke(Render);return;} Render(); }
 internal void SaveBounds() {
  if(!loaded || Store==null || WindowState!=WindowState.Normal) return;
  try {
   Store.Update(s=>{
    var p=s.Settings;
    if(TasksOnly){p.TaskLeft=Left;p.TaskTop=Top;p.TaskWidth=ActualWidth;p.TaskHeight=ActualHeight;}
    else {p.Left=Left;p.Top=Top;p.Width=ActualWidth;p.Height=ActualHeight;}
   },false);
  }catch(Exception ex){App.Instance.Log(ex);}
 }
 internal void ApplyWindowMode() {
  if(!loaded)return;
  var p=Store.Current.Settings;
  if(p.Topmost || !p.DesktopMode){Native.Detach(this);Topmost=p.Topmost;}
  else {Topmost=false;if(!Native.Attach(this)) ToolTip="桌面附着暂不可用，当前为普通浮动面板，可在设置中重新启用。";}
 }
 internal void RefreshClock(){if(observedToday!=DateTime.Today){if(followToday)SelectedDate=DateTime.Today;observedToday=DateTime.Today;} if(IsVisible && !NodeDragging && WindowState!=WindowState.Minimized && Mouse.LeftButton!=MouseButtonState.Pressed && !Application.Current.Windows.OfType<FormWindow>().Any())Render();if(Store.Current.Settings.DesktopMode&&!Store.Current.Settings.Topmost&&!Native.IsAttached(this))ApplyWindowMode();}
 internal void ShowTimeline(){timelineCollapsed=false;Render();}
 internal static SolidColorBrush Brush(string hex,double opacity=1){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Opacity=opacity;return b;}
 internal static string InkFor(string hex){try{var c=(Color)ColorConverter.ConvertFromString(hex);return c.R*.299+c.G*.587+c.B*.114>148?"#302B3D":"#FFFFFF";}catch{return "#302B3D";}}
 internal TextBlock Text(string value,double size=13,bool bold=false,string? ink=null) => new(){Text=value,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Foreground=Brush(ink??Palette.Ink),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
 internal Button Button(string text,Action action,bool primary=false) {
  var b=new Button{Content=text,Background=Brush(primary?Palette.Accent:Palette.Background),Foreground=Brush(primary?InkFor(Palette.Accent):Palette.Ink),BorderBrush=Brush(Palette.Ink,.14)};
  b.Click+=(_,_)=>Run(action);System.Windows.Automation.AutomationProperties.SetName(b,text);return b;
 }
 internal void Run(Action action){try{action();}catch(Exception ex){App.Instance.Log(ex);MessageBox.Show(this,ex.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Warning);}}
 static void At(Canvas c,UIElement v,double x,double y){Canvas.SetLeft(v,x);Canvas.SetTop(v,y);c.Children.Add(v);}
 internal void Render() {
  if(!loaded||rendering)return;
  rendering=true;
  try {
   var p=App.Instance.Appearance;
   frame.Background=Brush(Palette.Background,p.Opacity);frame.BorderBrush=Brush(Palette.Ink,.20);
   content.Children.Clear();content.RowDefinitions.Clear();
   content.RowDefinitions.Add(new(){Height=GridLength.Auto});
   content.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
   var header=new DockPanel{Margin=new Thickness(0,0,0,12),LastChildFill=true};
   var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);
   var undo=Button("↶",Store.Undo);undo.IsEnabled=Store.CanUndo;undo.ToolTip=Store.CanUndo?"撤销："+Store.UndoLabel+"（Ctrl+Z）":"没有可撤销的操作";undo.Padding=new Thickness(7,7,7,7);System.Windows.Automation.AutomationProperties.SetName(undo,"撤销");actions.Children.Add(undo);
   var redo=Button("↷",Store.Redo);redo.IsEnabled=Store.CanRedo;redo.ToolTip=Store.CanRedo?"重做："+Store.RedoLabel+"（Ctrl+Y）":"没有可重做的操作";redo.Padding=new Thickness(7,7,7,7);System.Windows.Automation.AutomationProperties.SetName(redo,"重做");actions.Children.Add(redo);
   actions.Children.Add(Button(p.Topmost?"取消置顶":"置顶",App.Instance.ToggleTopmost));
   actions.Children.Add(Button(p.Detached?"合并":"拆分",()=>App.Instance.SetDetached(!Store.Current.Settings.Detached)));
   actions.Children.Add(Button("设置",()=>Dialogs.Settings(this)));
   var minimize=Button("—",()=>WindowState=WindowState.Minimized);minimize.ToolTip="最小化到任务栏";actions.Children.Add(minimize);
   var close=Button("×",App.Instance.ExitApp);close.ToolTip="退出整个程序";close.Foreground=Brush("#A43F4F");System.Windows.Automation.AutomationProperties.SetName(close,"退出整个程序");actions.Children.Add(close);
   header.Children.Add(actions);
   var handle=new StackPanel{Cursor=Cursors.SizeAll,Background=Brushes.Transparent};
   handle.Children.Add(Text(TasksOnly?"任务时间轴":"日程管理",21,true));
   handle.Children.Add(Text(DateTime.Today.ToString("yyyy / MM / dd")+"   ·   "+new[]{"周日","周一","周二","周三","周四","周五","周六"}[(int)DateTime.Today.DayOfWeek],11,false));
   Point? drag=null;
   handle.MouseLeftButtonDown+=(_,e)=>{drag=PointToScreen(e.GetPosition(this));handle.CaptureMouse();e.Handled=true;};
   handle.MouseMove+=(_,e)=>{if(drag is Point last&&e.LeftButton==MouseButtonState.Pressed){var next=PointToScreen(e.GetPosition(this));Native.Move(this,next.X-last.X,next.Y-last.Y);drag=next;}};
   handle.MouseLeftButtonUp+=(_,_)=>{drag=null;handle.ReleaseMouseCapture();SaveBounds();};
   header.Children.Add(handle);content.Children.Add(header);
   var body=new Grid();Grid.SetRow(body,1);content.Children.Add(body);
   if(TasksOnly){body.Children.Add(BuildTimeline());return;}
   if(p.Detached){body.Children.Add(BuildCalendar());return;}
   body.RowDefinitions.Add(new(){Height=calendarCollapsed?GridLength.Auto:new GridLength(1,GridUnitType.Star),MinHeight=calendarCollapsed?0:200});
   body.RowDefinitions.Add(new(){Height=GridLength.Auto});
   body.RowDefinitions.Add(new(){Height=timelineCollapsed?GridLength.Auto:calendarCollapsed?new GridLength(1,GridUnitType.Star):new GridLength(Math.Max(200,Math.Min(290,ActualHeight*.34)))});
   body.Children.Add(BuildCalendar());
   var sep=new Border{Height=1,Margin=new Thickness(0,10,0,10),Background=Brush(Palette.Ink,.13)};Grid.SetRow(sep,1);body.Children.Add(sep);
   var timeline=BuildTimeline();Grid.SetRow(timeline,2);body.Children.Add(timeline);
  }finally{rendering=false;}
 }
 internal void Navigate(int direction){followToday=false;SelectedDate=MonthView?SelectedDate.AddMonths(direction):SelectedDate.AddDays(7*direction);Render();}
 internal FrameworkElement BuildCalendar(){
  var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  var toolbar=new DockPanel{Margin=new Thickness(0,0,0,8)};
  var right=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(right,Dock.Right);
  right.Children.Add(Button("＋ 日程",()=>EditEvent(null,SelectedDate.Date.AddHours(9)),true));
  right.Children.Add(Button(calendarCollapsed?"展开":"收起",()=>{calendarCollapsed=!calendarCollapsed;Render();}));
  toolbar.Children.Add(right);
  var nav=new StackPanel{Orientation=Orientation.Horizontal};
  nav.Children.Add(Button("‹",()=>Navigate(-1)));nav.Children.Add(Button("今天",()=>{SelectedDate=DateTime.Today;followToday=true;weekScrollY=8*52*CalendarZoom;Render();}));nav.Children.Add(Button("›",()=>Navigate(1)));
  var date=new DatePicker{SelectedDate=SelectedDate,Width=125,Margin=new Thickness(6,3,6,3),VerticalContentAlignment=VerticalAlignment.Center};
  date.SelectedDateChanged+=(_,_)=>{if(date.SelectedDate is DateTime d&&d!=SelectedDate){SelectedDate=d;followToday=false;Render();}};
  nav.Children.Add(date);nav.Children.Add(Button("周",()=>{MonthView=false;Render();},!MonthView));nav.Children.Add(Button("月",()=>{MonthView=true;Render();},MonthView));
  nav.Children.Add(Button(Math.Round(CalendarZoom*100)+"%",()=>ResetCalendarZoom()));
  toolbar.Children.Add(nav);grid.Children.Add(toolbar);
  if(calendarCollapsed)return grid;
  var view=MonthView?BuildMonth():BuildWeek();Grid.SetRow(view,1);grid.Children.Add(view);return grid;
 }
 internal void EditEvent(Occurrence? occurrence,DateTime? start=null,DateTime? end=null) {
  Dialogs.Event(this,occurrence,start??SelectedDate.AddHours(9),end);
 }
 internal void EditTask(TodoTask? task){Dialogs.Task(this,task);}
 internal void MoveOccurrence(Occurrence o,DateTime newStart,DateTime? newEnd=null){
  var edited=Copies.Clone(o.Source);edited.Start=newStart;edited.End=newEnd??newStart+(o.End-o.Start);
  bool future=false;
  if(o.Source.Weekly) {
   var answer=MessageBox.Show(this,"仅修改这一次？\n\n“是”仅修改本次；“否”修改本次及以后。","重复日程",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
   if(answer==MessageBoxResult.Cancel)return;future=answer==MessageBoxResult.No;
  }
  Store.Edit("移动或调整日程",s=>CalendarLogic.EditOccurrence(s,o,edited,future));
 }
}



