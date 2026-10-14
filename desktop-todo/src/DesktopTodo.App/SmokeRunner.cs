using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
namespace DesktopTodo.App;
internal static class SmokeRunner {
 internal static async System.Threading.Tasks.Task Run(App app){
  var results=new List<string>();
  var output=Path.Combine(app.DataDir,"screenshots");Directory.CreateDirectory(output);
  void CaptureNow(Window window,string name){
   window.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);
   results.Add(name);
  }
  async System.Threading.Tasks.Task Capture(Window window,string name){await System.Threading.Tasks.Task.Delay(350);CaptureNow(window,name);}
  void ExerciseDialog(Action open,Action<FormWindow> edit,string name) {
   bool handled=false;Exception? failure=null;
   app.Dispatcher.BeginInvoke(new Action(()=>{
    var form=app.Windows.OfType<FormWindow>().LastOrDefault();
    try {
     if(form==null)throw new Exception("Dialog did not open: "+name);
     edit(form);CaptureNow(form,name);
     form.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
     if(form.IsVisible)throw new Exception("Dialog did not save: "+name);
     handled=true;
    }catch(Exception ex){failure=ex;form?.Close();}
   }),DispatcherPriority.ApplicationIdle);
   open();
   if(failure!=null)throw failure;
   if(!handled)throw new Exception("Dialog handler not reached: "+name);
  }
  try {
   await Capture(app.Planner,"01-week");
   app.Planner.MonthView=true;app.Planner.Render();await Capture(app.Planner,"02-month");
   app.SetDetached(true);await Capture(app.TaskWindow!,"03-detached-timeline");await Capture(app.Planner,"04-detached-calendar");
   app.SetDetached(false);app.Planner.MonthView=false;
   foreach(var palette in Palette.All){app.Store.Update(s=>s.Settings.Theme=palette.Name);await Capture(app.Planner,"theme-"+palette.Name);}
   app.Store.Update(s=>{s.Settings.Theme="雪景紫";s.Settings.Opacity=.35;});await Capture(app.Planner,"05-opacity");
   app.Store.Update(s=>s.Settings.Opacity=.95);
   int eventCount=app.Store.Current.Events.Count;
   ExerciseDialog(()=>app.Planner.EditEvent(null,DateTime.Today.AddHours(17)),form=>form.Fields.Children.OfType<TextBox>().First().Text="界面测试：添加日程","06-event-editor");
   if(app.Store.Current.Events.Count!=eventCount+1)throw new Exception("Event editor failed to persist");
   int taskCount=app.Store.Current.Tasks.Count;
   ExerciseDialog(()=>app.Planner.EditTask(null),form=>form.Fields.Children.OfType<TextBox>().First().Text="界面测试：添加任务","07-task-editor");
   if(app.Store.Current.Tasks.Count!=taskCount+1)throw new Exception("Task editor failed to persist");
   var task=app.Store.Current.Tasks.Last();
   ExerciseDialog(()=>Dialogs.EditSavedMilestone(app.Planner,task,null,DateTime.Today),form=>form.Fields.Children.OfType<TextBox>().First().Text="界面测试：添加节点","08-node-editor");
   if(app.Store.Current.Tasks.Last().Milestones.Count!=1)throw new Exception("Node editor failed to persist");
   ExerciseDialog(()=>Dialogs.Settings(app.Planner),form=>{},"09-settings");
   if(Environment.GetCommandLineArgs().Contains("--interactions"))await InteractionChecks.Run(app,CaptureNow,results);
   if(Environment.GetCommandLineArgs().Contains("--update-checks"))await UpdateChecks.Run(app,CaptureNow,results);
   app.Planner.Width=760;app.Planner.Height=630;app.Planner.Render();await Capture(app.Planner,"10-small-window");
   app.Planner.Width=1050;app.Planner.Height=820;app.Planner.Render();
   for(int i=0;i<3;i++){app.SetDetached(true);app.TaskWindow!.Hide();app.TaskWindow.Show();app.SetDetached(false);}
   results.Add("detach-rejoin-repeat");
   // Test native parent changes against the actual Windows desktop.
   app.Store.Update(s=>s.Settings.DesktopMode=true);app.ApplyWindows();
   await System.Threading.Tasks.Task.Delay(400);
   if(!Native.IsAttached(app.Planner))throw new Exception("Desktop attachment failed");
   results.Add("desktop-attachment");
   // Verify actual shell entries, rather than inferring from window style.
      var entries=InteractionChecks.TaskbarEntries();if(entries.Length==0)throw new Exception("Actual Windows taskbar entry missing");results.Add("taskbar-buttons: "+string.Join(" | ",entries));
   var screenBounds=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
   using(var taskbarCapture=new System.Drawing.Bitmap(screenBounds.Width,100)){
    using(var g=System.Drawing.Graphics.FromImage(taskbarCapture))g.CopyFromScreen(0,screenBounds.Height-100,0,0,new System.Drawing.Size(screenBounds.Width,100));
    taskbarCapture.Save(Path.Combine(output,"19-taskbar.png"),System.Drawing.Imaging.ImageFormat.Png);
   }
   if(Environment.GetCommandLineArgs().Contains("--desktop-capture")) {
    Native.Activate(app.Planner);await System.Threading.Tasks.Task.Delay(200);
    NativeTestInput.WinD();await System.Threading.Tasks.Task.Delay(500);
    if(app.Planner.WindowState!=WindowState.Minimized)throw new Exception("Win+D should minimize with taskbar enabled");
    NativeTestInput.WinD();await System.Threading.Tasks.Task.Delay(500);
    // Desktop-owned windows can remain minimized after a second Win+D; the taskbar is the restore entry.
    app.Planner.WindowState=WindowState.Minimized;await System.Threading.Tasks.Task.Delay(300);
    if(!InteractionChecks.ClickTaskbar())throw new Exception("Taskbar button not found for restore");
    await System.Threading.Tasks.Task.Delay(500);
    if(app.Planner.WindowState!=WindowState.Normal)throw new Exception("Actual taskbar click did not restore window");
    results.Add("physical-win-d-minimize-restore-taskbar-click");
    dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
    try {
     shell.MinimizeAll();await System.Threading.Tasks.Task.Delay(400);Native.Activate(app.Planner);await System.Threading.Tasks.Task.Delay(300);
     results.Add(Native.Diagnostic(app.Planner));var rect=Native.Bounds(app.Planner);
     using var screen=new System.Drawing.Bitmap(rect.Width,rect.Height);
     using(var graphics=System.Drawing.Graphics.FromImage(screen))graphics.CopyFromScreen(rect.X,rect.Y,0,0,new System.Drawing.Size(rect.Width,rect.Height));
     screen.Save(Path.Combine(output,"11-real-desktop.png"),System.Drawing.Imaging.ImageFormat.Png);
    }finally{shell.UndoMinimizeALL();System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
   }
   app.ToggleTopmost();if(Native.IsAttached(app.Planner)||!app.Planner.Topmost)throw new Exception("Topmost transition failed");
   results.Add("desktop-to-topmost");
   app.ToggleTopmost();if(!Native.IsAttached(app.Planner))throw new Exception("Return to desktop failed");
   results.Add("topmost-to-desktop");
   app.Store.Update(s=>s.Settings.DesktopMode=false);app.ApplyWindows();
   app.Store.Backup(Path.Combine(app.DataDir,"smoke.backup.json"));
   app.Store.Restore(Path.Combine(app.DataDir,"smoke.backup.json"));
   results.Add("backup-restore");
   if(app.NotificationCheck){app.Notify("日程管理 · 通知测试","发布版系统通知检查，测试结束后程序将自动退出。");results.Add("notification: "+app.NotificationStatus+" / id="+app.LastNotificationId);await System.Threading.Tasks.Task.Delay(1500);}
   File.WriteAllText(Path.Combine(app.DataDir,"smoke-result.json"),JsonSerializer.Serialize(new{passed=true,checks=results,events=app.Store.Current.Events.Count,tasks=app.Store.Current.Tasks.Count}));
  }catch(Exception ex){app.Log(ex);File.WriteAllText(Path.Combine(app.DataDir,"smoke-result.json"),JsonSerializer.Serialize(new{passed=false,error=ex.ToString(),checks=results}));}
  finally{NativeTestInput.Up();NativeTestInput.Key(0x11,true);NativeTestInput.Key(0x5B,true);app.ExitApp();}
 }
}











