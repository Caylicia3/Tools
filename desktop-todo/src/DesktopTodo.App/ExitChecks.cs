using System.Windows.Interop;
using System.Windows.Threading;
namespace DesktopTodo.App;
internal static class ExitChecks {
 internal static async System.Threading.Tasks.Task Run(App app,string mode){
  try{
   app.Store.Update(s=>{s.Settings.Topmost=true;s.Settings.DesktopMode=false;});app.ApplyWindows();
   app.SetDetached(true);await System.Threading.Tasks.Task.Delay(300);
   var target=mode.StartsWith("split-")?app.TaskWindow!:app.Planner;Native.Activate(target);
   bool editorClosed=false;
   _ = app.Dispatcher.BeginInvoke(new Action(()=>{var form=app.Windows.OfType<FormWindow>().Last();form.Close();editorClosed=true;}),DispatcherPriority.ApplicationIdle);
   Dialogs.Event(app.Planner,null,DateTime.Today.AddHours(9));
   if(!editorClosed||app.Exiting||app.Windows.OfType<PlannerWindow>().Count()!=2)throw new Exception("Editor close exited app");
   Native.Activate(target);await System.Threading.Tasks.Task.Delay(160);NativeTestInput.PointAt(target,25,25);NativeTestInput.Down();NativeTestInput.Up();await System.Threading.Tasks.Task.Delay(80);
   File.WriteAllText(Path.Combine(app.DataDir,"exit-requested.txt"),mode+"; panels=2; editor-close=passed");
   if(mode.EndsWith("-x"))InteractionChecks.Find<Button>(target).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="退出整个程序").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   else if(mode.EndsWith("-alt-f4")){NativeTestInput.Key(0x12);await System.Threading.Tasks.Task.Delay(50);NativeTestInput.Key(0x73);await System.Threading.Tasks.Task.Delay(50);NativeTestInput.Key(0x73,true);NativeTestInput.Key(0x12,true);}
   else if(mode.EndsWith("-shell"))NativeTestInput.ShellClose(new WindowInteropHelper(target).Handle);
   else throw new Exception("Unknown exit check");
   await System.Threading.Tasks.Task.Delay(2000);if(!app.Exiting)throw new Exception("Close request did not exit app");
  }catch(Exception ex){app.Log(ex);app.ExitApp();}
 }
}



