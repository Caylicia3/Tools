using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace DesktopTodo.App;
internal static class Native {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string? name);
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int cmd);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
 [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }
 static readonly Dictionary<IntPtr,IntPtr> Owners=[];
 public static bool IsAttached(Window window) => Owners.TryGetValue(new WindowInteropHelper(window).Handle,out var owner) && IsWindow(owner);
 public static bool Attach(Window window) {
  var hwnd=new WindowInteropHelper(window).Handle;
  if(hwnd==IntPtr.Zero)return false;
  if(IsAttached(window))return true;
  var desktop=FindWindow("Progman",null);
  if(desktop==IntPtr.Zero)return false;
  // Use a desktop-owned top-level window so WPF keeps its compositor and DPI context.
  SetWindowLongPtr(hwnd,-8,desktop);
  TaskbarStyle(hwnd);
  if(GetWindowLongPtr(hwnd,-8)!=desktop)return false;
  SetWindowPos(hwnd,new IntPtr(1),0,0,0,0,0x0040|0x0020|0x0001|0x0002|0x0010);
  Owners[hwnd]=desktop;EnsureTaskbar(hwnd);return true;
 }
 public static void Detach(Window window) {
  var hwnd=new WindowInteropHelper(window).Handle;
  if(!Owners.ContainsKey(hwnd))return;
  SetWindowLongPtr(hwnd,-8,IntPtr.Zero);
  TaskbarStyle(hwnd);
  SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x0040|0x0020|0x0001|0x0002|0x0010);
  Owners.Remove(hwnd);EnsureTaskbar(hwnd);
 }
 [ComImport,Guid("56FDF342-FD6D-11D0-958A-006097C9A090"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ITaskbarList {void HrInit();void AddTab(IntPtr hwnd);void DeleteTab(IntPtr hwnd);void ActivateTab(IntPtr hwnd);void SetActiveAlt(IntPtr hwnd);}
 static void EnsureTaskbar(IntPtr hwnd){
  object? shell=null;
  try{shell=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11D0-958A-006097C9A090"))!);var list=(ITaskbarList)shell!;list.HrInit();list.AddTab(hwnd);}
  catch(Exception ex){App.Instance.Log(ex);}
  finally{if(shell!=null)Marshal.FinalReleaseComObject(shell);}
 }
 static void TaskbarStyle(IntPtr hwnd) {
  const long appWindow=0x40000,toolWindow=0x80;
  long current=GetWindowLongPtr(hwnd,-20).ToInt64();
  SetWindowLongPtr(hwnd,-20,new IntPtr((current|appWindow)&~toolWindow));
 }
 public static bool HasTaskbarStyle(Window window)=>(GetWindowLongPtr(new WindowInteropHelper(window).Handle,-20).ToInt64()&0x40000)!=0;
 public static void Move(Window window,double dx,double dy) {
  var hwnd=new WindowInteropHelper(window).Handle;GetWindowRect(hwnd,out var r);
  SetWindowPos(hwnd,IntPtr.Zero,r.Left+(int)dx,r.Top+(int)dy,0,0,0x0001|0x0004|0x0010);
 }
 public static (int X,int Y,int Width,int Height) Bounds(Window window) {
  GetWindowRect(new WindowInteropHelper(window).Handle,out var r);
  return (r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
 }
 public static string Diagnostic(Window window) {
  var h=new WindowInteropHelper(window).Handle;var r=Bounds(window);
  return $"visible={IsWindowVisible(h)} minimized={IsIconic(h)} owner={GetWindowLongPtr(h,-8)} style={GetWindowLongPtr(h,-16):X} bounds={r}";
 }
 public static void Activate(Window window) {
  if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Show();var h=new WindowInteropHelper(window).Handle;ShowWindow(h,5);SetForegroundWindow(h);window.Activate();
 }
 public static void PlaceOnScreen(Window window) {
  var x=SystemParameters.VirtualScreenLeft;var y=SystemParameters.VirtualScreenTop;
  window.Width=Math.Min(window.Width,SystemParameters.VirtualScreenWidth-32);
  window.Height=Math.Min(window.Height,SystemParameters.VirtualScreenHeight-48);
  window.Left=Math.Clamp(window.Left,x,x+Math.Max(0,SystemParameters.VirtualScreenWidth-window.Width));
  window.Top=Math.Clamp(window.Top,y,y+Math.Max(0,SystemParameters.VirtualScreenHeight-window.Height));
 }
}




