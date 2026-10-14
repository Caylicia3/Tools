using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
namespace DesktopTodo.App;
internal partial class PlannerWindow {
 internal void ZoomTimelineAt(double fraction,int direction){
  if(NodeDragging||TimelinePanning||Mouse.LeftButton==MouseButtonState.Pressed)return;
  int next=ViewMath.ZoomDays(TimelineDays,direction);if(next==TimelineDays)return;
  TimelineStart=ViewMath.AnchoredStart(TimelineStart,TimelineDays,next,fraction);
  App.Instance.ChangeView(p=>p.TimelineDays=next);
 }
 internal bool MoveNode(string taskId,string nodeId,DateTime target){
  var task=Store.Current.Tasks.Single(t=>t.Id==taskId);var node=task.Milestones.Single(m=>m.Id==nodeId);target=target.Date;
  if(target<task.Start.Date||target>task.End.Date||target==node.Date.Date)return false;
  Store.Edit("移动节点",s=>s.Tasks.Single(t=>t.Id==taskId).Milestones.Single(m=>m.Id==nodeId).Date=target);return true;
 }
 internal void CopyTimelineFrom(PlannerWindow other){TimelineStart=other.TimelineStart;timelineScroll=other.timelineScroll;ShowCompleted=other.ShowCompleted;timelineBrowseMin=other.timelineBrowseMin;timelineBrowseMax=other.timelineBrowseMax;timelineBrowseDays=other.timelineBrowseDays;}
 void ChooseTimelineDays(){
  var w=new FormWindow(this,"自定义时间轴范围",370);var input=w.Input("显示天数（1～3650 的整数）",TimelineDays.ToString());
  w.Hint("例如：14 表示两周，180 表示约半年。");
  w.Commit(()=>{if(!int.TryParse(input.Text,out int n)||n<1||n>3650)throw new InvalidDataException("请填写 1 到 3650 之间的整数。");App.Instance.ChangeView(p=>p.TimelineDays=n);});
  w.ShowDialog();Render();
 }
 FrameworkElement BuildTimeline(){
  var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  var toolbar=new DockPanel{Margin=new Thickness(0,0,0,7)};
  var right=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(right,Dock.Right);
  right.Children.Add(Button("＋ 任务",()=>EditTask(null),true));if(!TasksOnly)right.Children.Add(Button(timelineCollapsed?"展开":"收起",()=>{timelineCollapsed=!timelineCollapsed;Render();}));
  toolbar.Children.Add(right);
  var nav=new StackPanel{Orientation=Orientation.Horizontal};nav.Children.Add(Text("任务时间轴",14,true));
  nav.Children.Add(Button("‹",()=>{TimelineStart=ViewMath.ShiftTimeline(TimelineStart,-TimelineDays,TimelineDays);Render();}));
  nav.Children.Add(Button("今天",()=>{TimelineStart=DateTime.Today.AddDays(-Math.Max(1,TimelineDays/7));Render();}));
  nav.Children.Add(Button("›",()=>{TimelineStart=ViewMath.ShiftTimeline(TimelineStart,TimelineDays,TimelineDays);Render();}));
  var options=new List<string>{"7 天","30 天","90 天"};string selected=TimelineDays+" 天";if(!options.Contains(selected))options.Add(selected);options.Add("自定义…");
  var scale=new ComboBox{Width=100,Margin=new Thickness(5,3,5,3),ItemsSource=options,SelectedItem=selected,ToolTip="Ctrl＋滚轮也可缩放日期刻度"};
  System.Windows.Automation.AutomationProperties.SetName(scale,"时间轴显示天数");
  scale.SelectionChanged+=(_,_)=>{if(scale.SelectedItem as string=="自定义…"){ChooseTimelineDays();return;}if(scale.SelectedItem is string value&&int.TryParse(value.Split(' ')[0],out int n))App.Instance.ChangeView(p=>p.TimelineDays=n);};
  nav.Children.Add(scale);
  var completed=new CheckBox{Content="已完成",IsChecked=ShowCompleted,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(5),Foreground=Brush(Palette.Ink),FontSize=11};
  completed.Click+=(_,_)=>{ShowCompleted=completed.IsChecked==true;Render();};nav.Children.Add(completed);toolbar.Children.Add(nav);grid.Children.Add(toolbar);if(timelineCollapsed&&!TasksOnly)return grid;
  grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
  FrameworkElement plotView=BuildTimelinePlot();Grid.SetRow(plotView,1);grid.Children.Add(plotView);
  var browse=new ScrollBar{Orientation=Orientation.Horizontal,Height=SystemParameters.HorizontalScrollBarHeight,Margin=new Thickness(165,5,0,0),SmallChange=1,LargeChange=TimelineDays,ViewportSize=TimelineDays,ToolTip="左右拖动滑块浏览日期；两端箭头每次移动一天"};
  System.Windows.Automation.AutomationProperties.SetName(browse,"时间轴横向滚动条");
  void SetRange(bool extend=false) {
   double current=TimelineStart.Ticks/(double)TimeSpan.TicksPerDay,padding=Math.Max(90,TimelineDays*2);
   double earliest=Store.Current.Tasks.Count==0?current:Store.Current.Tasks.Min(t=>t.Start.Date.Ticks)/(double)TimeSpan.TicksPerDay;
   double latest=Store.Current.Tasks.Count==0?current:Store.Current.Tasks.Max(t=>t.End.Date.Ticks)/(double)TimeSpan.TicksPerDay-TimelineDays+1;
   if(double.IsNaN(timelineBrowseMin)||timelineBrowseDays!=TimelineDays||current<timelineBrowseMin||current>timelineBrowseMax||extend){
    timelineBrowseMin=Math.Max(new DateTime(1900,1,1).Ticks/(double)TimeSpan.TicksPerDay,Math.Min(current,earliest)-padding);
    timelineBrowseMax=Math.Min(new DateTime(9990,1,1).Ticks/(double)TimeSpan.TicksPerDay-TimelineDays,Math.Max(current,latest)+padding);
   }
   timelineBrowseDays=TimelineDays;
   browse.Minimum=timelineBrowseMin;browse.Maximum=timelineBrowseMax;browse.Value=current;
  }
  SetRange();
  browse.ValueChanged+=(_,_)=>{
   var next=new DateTime((long)Math.Round(browse.Value)*TimeSpan.TicksPerDay);
   if(next==TimelineStart)return;TimelineStart=next;
   grid.Children.Remove(plotView);plotView=BuildTimelinePlot();Grid.SetRow(plotView,1);grid.Children.Add(plotView);
  };
  browse.Scroll+=(_,e)=>{
   if(e.ScrollEventType==ScrollEventType.ThumbTrack||e.ScrollEventType==ScrollEventType.ThumbPosition)return;
   if(Math.Abs(browse.Value-browse.Minimum)<1||Math.Abs(browse.Value-browse.Maximum)<1)SetRange(true);
  };
  Grid.SetRow(browse,2);grid.Children.Add(browse);return grid;
 }
 double timelineBrowseMin=double.NaN,timelineBrowseMax=double.NaN;
 int timelineBrowseDays;
 FrameworkElement BuildTimelinePlot(){
  double width=CalendarWidth,left=165,plot=width-left-12,day=plot/TimelineDays,rowHeight=App.Instance.Appearance.TimelineRowHeight;
  var tasks=Store.Current.Tasks.Where(t=>(ShowCompleted||!t.Completed)&&t.Start.Date<TimelineStart.AddDays(TimelineDays)&&t.End.Date>=TimelineStart).OrderBy(t=>t.Completed).ThenBy(t=>t.End).ToList();
  var canvas=new Canvas{VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,Width=width,Height=Math.Max(125,36+tasks.Count*rowHeight),Background=Brush("#FFFFFF",.22)};
  ScrollViewer? scroll=null;
  string rangeFormat=TimelineStart.Year!=DateTime.Today.Year||TimelineStart.Year!=TimelineStart.AddDays(TimelineDays-1).Year?"yyyy/M/d":"M/d";
  At(canvas,Text(TimelineStart.ToString(rangeFormat)+" — "+TimelineStart.AddDays(TimelineDays-1).ToString(rangeFormat),10,true),7,10);
  int targetStep=Math.Max(1,(int)Math.Ceiling(TimelineDays*62/plot));
  int step=new[]{1,2,3,7,14,30,60,90,180,365,730}.FirstOrDefault(v=>v>=targetStep,730);
  if(TimelineDays<=180)for(int i=0;i<TimelineDays;i++){var date=TimelineStart.AddDays(i);if(date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)At(canvas,new Rectangle{Width=day,Height=canvas.Height,Fill=Brush(Palette.Ink,.025),IsHitTestVisible=false},left+i*day,0);}
  for(int i=0;i<TimelineDays;i+=step){double x=left+i*day;canvas.Children.Add(new Line{X1=x,Y1=30,X2=x,Y2=canvas.Height,Stroke=Brush(Palette.Ink,.08),IsHitTestVisible=false});At(canvas,Text(TimelineStart.AddDays(i).ToString(TimelineDays>365?"yyyy/M":"M/d"),10),x+2,10);}
  if(tasks.Count==0){At(canvas,Text(Store.Current.Tasks.Count==0?"把重要的事放上时间轴":"这个日期范围内没有任务",16,true),18,52);At(canvas,Text("拖动日期刻度或空白处浏览 · Ctrl＋滚轮缩放日期",12),18,84);}
  for(int i=0;i<tasks.Count;i++){
   var t=tasks[i];double y=36+i*rowHeight,center=y+rowHeight/2-7;
   canvas.Children.Add(new Line{X1=0,Y1=y+rowHeight-1,X2=width,Y2=y+rowHeight-1,Stroke=Brush(Palette.Ink,.09),IsHitTestVisible=false});
   var title=Text((t.Completed?"✓ ":"")+t.Title,12,true);title.Width=left-20;title.Height=16;title.Cursor=Cursors.Hand;title.ToolTip=t.Title+"\n"+t.Start.ToString("yyyy/MM/dd")+" — "+t.End.ToString("yyyy/MM/dd")+"\n点击编辑任务和节点";
   title.MouseLeftButtonUp+=(_,_)=>EditTask(t);At(canvas,title,7,y+(rowHeight-34)/2);
   int days=(t.End.Date-DateTime.Today).Days;
   string status=t.Completed?"已完成":days<0?"已逾期 "+(-days)+" 天":days==0?"今天截止":"剩余 "+days+" 天";
   var summary=Text(status+" · "+t.Milestones.Count(m=>m.Completed)+"/"+t.Milestones.Count+" 节点",10,false,!t.Completed&&days<0?"#A43F4F":!t.Completed&&days<=3?"#926619":null);summary.Width=left-16;summary.Height=14;At(canvas,summary,7,y+(rowHeight-34)/2+18);
   double startX=left+Math.Max(0,(t.Start.Date-TimelineStart).Days)*day,endX=left+Math.Min(TimelineDays,(t.End.Date-TimelineStart).Days+1)*day;
   string color=t.Color==""?Palette.Colors[i%Palette.Colors.Length]:t.Color;double barHeight=App.Instance.Appearance.TimelineBarHeight;
   var bar=new Border{Height=barHeight,CornerRadius=new CornerRadius(Math.Min(6,barHeight/2)),Background=Brush(color,t.Completed?.45:1),BorderBrush=Brush(Palette.Ink,.20),BorderThickness=new Thickness(1),VerticalAlignment=VerticalAlignment.Center};
   var hit=new Border{Width=Math.Max(5,endX-startX-4),Height=22,Background=Brushes.Transparent,Child=bar,Cursor=Cursors.Hand,ToolTip="点击编辑任务；起止日期请在编辑窗口修改"};
   System.Windows.Automation.AutomationProperties.SetName(hit,"任务条："+t.Title);
   Point? barDown=null;bool barMoved=false;
   hit.MouseLeftButtonDown+=(_,e)=>{barDown=e.GetPosition(hit);barMoved=false;hit.CaptureMouse();e.Handled=true;};
   hit.MouseMove+=(_,e)=>{if(barDown is Point p&&(e.GetPosition(hit)-p).Length>4)barMoved=true;};
   hit.MouseLeftButtonUp+=(_,e)=>{bool click=barDown!=null&&!barMoved;barDown=null;hit.ReleaseMouseCapture();e.Handled=true;if(click)EditTask(t);};
   hit.LostMouseCapture+=(_,_)=>barDown=null;At(canvas,hit,startX+2,center-11);
   var visibleNodes=t.Milestones.Where(m=>m.Date.Date>=TimelineStart&&m.Date.Date<TimelineStart.AddDays(TimelineDays)).OrderBy(m=>m.Date).ToList();
   foreach(var m in visibleNodes){
    int offset=(m.Date.Date-TimelineStart).Days;if(offset<0||offset>=TimelineDays)continue;double x=left+(offset+.5)*day;
    var marker=Diamond(m.Completed?Palette.Ink:"#FFFFFF");marker.Cursor=Cursors.Hand;marker.Focusable=true;marker.ToolTip=m.Title+" · "+m.Date.ToString("yyyy/MM/dd")+"\n"+m.Notes+"\n点击编辑 · 拖动预览，松手保存；Ctrl+Z 撤销";
    System.Windows.Automation.AutomationProperties.SetName(marker,"节点："+m.Title);At(canvas,marker,x-7,center-7);
    // Labels are placed after markers, with collision-aware widths.
    Point? down=null;bool moved=false,valid=false;DateTime proposed=m.Date;Polygon? ghost=null;Border? bubble=null;TextBlock? bubbleText=null;Canvas? previewLayer=null;
    void Clear(){if(ghost!=null)canvas.Children.Remove(ghost);if(previewLayer!=null)content.Children.Remove(previewLayer);ghost=null;bubble=null;bubbleText=null;previewLayer=null;NodeDragging=false;moved=false;valid=false;}
    marker.MouseLeftButtonDown+=(_,e)=>{down=e.GetPosition(canvas);moved=false;valid=false;marker.Focus();marker.CaptureMouse();e.Handled=true;};
    marker.MouseMove+=(_,e)=>{
     if(down is not Point origin||e.LeftButton!=MouseButtonState.Pressed)return;var p=e.GetPosition(canvas);if(!moved&&(p-origin).Length<4)return;moved=true;NodeDragging=true;
     int index=Math.Clamp((int)Math.Floor((p.X-left)/day),0,TimelineDays-1);proposed=TimelineStart.AddDays(index);
     var viewport=scroll==null?new Point():e.GetPosition(scroll);
     valid=p.X>=left&&p.X<=left+plot&&p.Y>=y&&p.Y<y+rowHeight&&proposed>=t.Start.Date&&proposed<=t.End.Date&&(scroll==null||(viewport.Y>=0&&viewport.Y<scroll.ViewportHeight));
     if(ghost==null){ghost=Diamond(Palette.Accent);ghost.Opacity=.8;ghost.IsHitTestVisible=false;Panel.SetZIndex(ghost,100);canvas.Children.Add(ghost);bubbleText=Text("",11,true);bubble=new Border{Background=Brush(Palette.Background),BorderBrush=Brush(Palette.Accent),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(6,3,6,3),Child=bubbleText,IsHitTestVisible=false};previewLayer=new Canvas{IsHitTestVisible=false};Grid.SetRowSpan(previewLayer,content.RowDefinitions.Count);Panel.SetZIndex(previewLayer,101);previewLayer.Children.Add(bubble);content.Children.Add(previewLayer);}
     double gx=left+(index+.5)*day;Canvas.SetLeft(ghost,gx-7);Canvas.SetTop(ghost,center-7);
     bubbleText!.Text=proposed.ToString("yyyy/MM/dd")+(valid?"":" · 超出范围");bubbleText.Foreground=Brush(valid?Palette.Ink:"#A43F4F");ghost.Stroke=Brush(valid?Palette.Ink:"#A43F4F");
     bubble!.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var pos=canvas.TranslatePoint(new Point(gx,center),content);Canvas.SetLeft(bubble,Math.Clamp(pos.X-bubble.DesiredSize.Width/2,0,Math.Max(0,content.ActualWidth-bubble.DesiredSize.Width)));Canvas.SetTop(bubble,Math.Max(0,pos.Y-10-bubble.DesiredSize.Height));
     e.Handled=true;
    };
    marker.MouseLeftButtonUp+=(_,e)=>{bool drag=moved,accept=valid,click=down!=null&&!moved;var date=proposed;down=null;marker.ReleaseMouseCapture();Clear();e.Handled=true;if(drag&&accept)Run(()=>MoveNode(t.Id,m.Id,date));else if(click)Dialogs.EditSavedMilestone(this,t,m);};
    marker.PreviewKeyDown+=(_,e)=>{if(e.Key!=Key.Escape)return;down=null;marker.ReleaseMouseCapture();Clear();e.Handled=true;};
    marker.LostMouseCapture+=(_,_)=>{down=null;Clear();};
   }
   // Nearby nodes share one caption at distant zoom levels; complete names remain in task editing.
   var groups=new List<List<Milestone>>();
   foreach(var m in visibleNodes){if(groups.Count==0||(m.Date.Date-groups[^1][^1].Date.Date).Days*day>=18)groups.Add([]);groups[^1].Add(m);}
   for(int j=0;j<groups.Count;j++){
    var group=groups[j];double x=left+((group[0].Date.Date-TimelineStart).Days+.5)*day-6;
    double next=j+1<groups.Count?left+((groups[j+1][0].Date.Date-TimelineStart).Days+.5)*day-6:width-4;
    double available=Math.Min(100,next-x-4);if(available<12)continue;
    var label=Text(group.Count==1?group[0].Title:group.Count+" 个节点",10);label.Width=available;label.Height=13;label.ToolTip=string.Join("\n",group.Select(m=>m.Date.ToString("yyyy/MM/dd")+" · "+m.Title));At(canvas,label,x,center+11);
   }
  }
  int todayOffset=(DateTime.Today-TimelineStart).Days;
  if(todayOffset>=0&&todayOffset<TimelineDays){double x=left+(todayOffset+.5)*day;canvas.Children.Add(new Line{X1=x,Y1=28,X2=x,Y2=canvas.Height,Stroke=Brush(Palette.Accent,.7),StrokeThickness=1.5,StrokeDashArray=new DoubleCollection{3,3},IsHitTestVisible=false});var today=Text("今天",10,true,Palette.Accent);today.IsHitTestVisible=false;At(canvas,today,x-11,0);}
  canvas.MouseLeftButtonDown+=(_,e)=>{
   if(NodeDragging||TimelinePanning)return;var p=e.GetPosition(canvas);
   if(p.X<left||p.X>left+plot)return;
   int row=(int)Math.Floor((p.Y-36)/rowHeight);
   if(e.ClickCount==2&&row>=0&&row<tasks.Count){
    var t=tasks[row];var date=TimelineStart.AddDays(Math.Clamp((int)((p.X-left)/day),0,TimelineDays-1));Dialogs.EditSavedMilestone(this,t,null,date);e.Handled=true;return;
   }
   BeginTimelinePan(e.GetPosition(this),plot);e.Handled=true;
  };
  scroll=new ScrollViewer{VerticalContentAlignment=VerticalAlignment.Top,HorizontalContentAlignment=HorizontalAlignment.Left,Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,ToolTip="拖动日期刻度或空白处左右浏览 · Ctrl＋滚轮缩放日期 · 普通滚轮上下浏览任务"};
  System.Windows.Automation.AutomationProperties.SetName(scroll,"任务时间轴内容");bool ready=false;double restore=timelineScroll;
  scroll.Loaded+=(_,_)=>{scroll.ScrollToVerticalOffset(restore);ready=true;};scroll.ScrollChanged+=(_,_)=>{if(ready)timelineScroll=scroll.VerticalOffset;};
  scroll.PreviewMouseWheel+=(_,e)=>{if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;e.Handled=true;ZoomTimelineAt((e.GetPosition(canvas).X-left)/plot,Math.Sign(e.Delta));};
  return scroll;
 }
 void BeginTimelinePan(Point origin,double plot) {
  var before=TimelineStart;int days=TimelineDays;bool moved=false,finished=false;
  var previousCursor=Cursor;
  void Finish(bool cancel) {
   if(finished)return;finished=true;
   PreviewMouseMove-=Move;PreviewMouseLeftButtonUp-=Up;PreviewKeyDown-=Cancel;LostMouseCapture-=Lost;
   TimelinePanning=false;Cursor=previousCursor;
   if(IsMouseCaptured)ReleaseMouseCapture();
   if(cancel)TimelineStart=before;
   if(moved)Render();
  }
  void Move(object sender,MouseEventArgs e) {
   if(e.LeftButton!=MouseButtonState.Pressed){Finish(false);return;}
   double delta=e.GetPosition(this).X-origin.X;
   if(!moved&&Math.Abs(delta)<4)return;moved=true;
   var next=ViewMath.PanTimeline(before,days,delta,plot);
   if(next!=TimelineStart){TimelineStart=next;Render();}
   e.Handled=true;
  }
  void Up(object sender,MouseButtonEventArgs e){Finish(false);e.Handled=true;}
  void Cancel(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Finish(true);e.Handled=true;}}
  void Lost(object sender,MouseEventArgs e){if(!IsMouseCaptured)Finish(true);}
  TimelinePanning=true;Cursor=Cursors.ScrollWE;
  PreviewMouseMove+=Move;PreviewMouseLeftButtonUp+=Up;PreviewKeyDown+=Cancel;LostMouseCapture+=Lost;
  if(!CaptureMouse())Finish(true);
 }
 Polygon Diamond(string fill)=>new(){Points=new PointCollection{new(7,0),new(14,7),new(7,14),new(0,7)},Fill=Brush(fill),Stroke=Brush(Palette.Ink),StrokeThickness=1.5,Width=14,Height=14};
}




