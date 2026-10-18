using System.Text.Json;
using System.Windows.Interop;

namespace DesktopTodo.App;

internal static class SidebarMotionChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
    internal static async System.Threading.Tasks.Task Run(App app, List<string> results)
    {
        var sidebar = app.Sidebar!;
        var host = app.Planner;
        app.Store.Update(s => { s.Settings.Topmost = true; s.Settings.DesktopMode = false; });
        app.ApplyWindows();
        app.SetDetached(false);
        host.Width = 740;
        host.Height = 700;
        host.Left = 24;
        host.Top = 24;
        Native.Activate(host);
        await System.Threading.Tasks.Task.Delay(250);
        Check(sidebar.Docked.Owner == host && !sidebar.Docked.ShowInTaskbar, "Attached pane ownership/taskbar incorrect");
        Check(new WindowInteropHelper(sidebar.Docked).Handle != new WindowInteropHelper(host).Handle, "Pane must have its own native surface");
        var physical = Native.Bounds(host);
        var frameWidth = host.MainFrame.ActualWidth;
        var frameChild = ((Grid)host.MainFrame.Child).Children[0];
        var start = host.SidebarHandle.PointToScreen(new Point(15, 52));
        sidebar.HandleStart(start);
        var samples = new List<object>();
        foreach (int width in new[] { 40, 100, 200, 320, 240, 120, 40, 0, 60, 320 })
        {
            sidebar.HandleMove(new Point(start.X + width * VisualTreeHelper.GetDpi(host).DpiScaleX, start.Y));
            await System.Threading.Tasks.Task.Delay(100);
            Check(Native.Bounds(host) == physical, "Sidebar drag moved or resized host HWND");
            Check(Math.Abs(host.MainFrame.ActualWidth - frameWidth) < .1, "Sidebar changed chart width");
            Check(ReferenceEquals(frameChild, ((Grid)host.MainFrame.Child).Children[0]), "Sidebar drag rebuilt main chart");
            samples.Add(new
            {
                requested = width,
                host.Left,
                host.Top,
                host.Width,
                sidebar.OpenWidth,
                frame = host.MainFrame.ActualWidth
            });
        }
        for (int i = 0; i < 12; i++)
        {
            sidebar.HandleMove(new Point(start.X + 320 * VisualTreeHelper.GetDpi(host).DpiScaleX, start.Y));
            await System.Threading.Tasks.Task.Delay(25);
            Check(Native.Bounds(host) == physical, "Stationary pointer changed host bounds");
        }
        File.WriteAllText(Path.Combine(app.DataDir, "motion-samples.json"), JsonSerializer.Serialize(new
        {
            fixedHost = true,
            samples
        }));
        sidebar.HandleEnd(true);
        Check(!sidebar.Expanded && Native.Bounds(host) == physical, "Cancel changed host");
        results.Add("fixed-host-native-bounds-and-chart-identical-through-drag-and-cancel");

        var work = SidebarController.WorkArea(host);
        host.Left = work.Right - host.Width;
        await System.Threading.Tasks.Task.Delay(180);
        physical = Native.Bounds(host);
        Check(sidebar.SetWidth(320), "No left-side placement available");
        Check(!sidebar.OnRight, "Right screen edge should use outside left space");
        Check(Native.Bounds(host) == physical, "Left placement moved host");
        var paneBounds = Native.Bounds(sidebar.Docked);
        Check(paneBounds.X + paneBounds.Width <= physical.X + 10, "Left pane covers chart");
        sidebar.SetWidth(0);
        results.Add("outside-left-fallback-keeps-host-fixed");

        host.Left = 24;
        sidebar.Show();
        await System.Threading.Tasks.Task.Delay(200);
        physical = Native.Bounds(host);
        Native.Activate(sidebar.Docked);
        start = sidebar.Docked.Handle.PointToScreen(new Point(15, 52));
        double direction = sidebar.OnRight ? -1 : 1;
        double distance = (sidebar.OpenWidth - 20) * VisualTreeHelper.GetDpi(host).DpiScaleX;
        await NativeTestInput.Drag(start, new Point(start.X + direction * 12, start.Y), new Point(start.X + direction * distance, start.Y));
        await System.Threading.Tasks.Task.Delay(150);
        Check(!sidebar.Expanded && Native.Bounds(host) == physical, "Native handle moved host or failed to collapse");
        results.Add("native-attached-handle-collapses-without-host-resize");

        host.WindowState = WindowState.Maximized;
        await System.Threading.Tasks.Task.Delay(250);
        physical = Native.Bounds(host);
        Check(!sidebar.SetWidth(320), "Maximized host must require external space");
        Check(Native.Bounds(host) == physical && !sidebar.Expanded, "No-room attempt changed maximized host");
        var dismissPrompt = System.Threading.Tasks.Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                await System.Threading.Tasks.Task.Delay(50);
                if (NativeTestInput.DismissSpacePrompt())
                    return true;
            }
            return false;
        });
        sidebar.Show();
        Check(await dismissPrompt, "No-room instruction prompt was not shown");
        host.WindowState = WindowState.Normal;
        await System.Threading.Tasks.Task.Delay(200);
        sidebar.Show();
        host.WindowState = WindowState.Minimized;
        await System.Threading.Tasks.Task.Delay(120);
        Check(!sidebar.Docked.IsVisible, "Attached pane remained visible after host minimized");
        host.WindowState = WindowState.Normal;
        await System.Threading.Tasks.Task.Delay(150);
        Check(sidebar.Docked.IsVisible, "Attached pane did not restore with host");
        results.Add("no-room-does-not-overlap-and-minimize-restore-follows-host");

        app.Store.Update(s => { s.Settings.Topmost = false; s.Settings.DesktopMode = true; });
        app.ApplyWindows();
        await System.Threading.Tasks.Task.Delay(200);
        physical = Native.Bounds(host);
        Check(Native.IsAttached(host), "Host lost desktop attachment");
        sidebar.SetWidth(280);
        Check(Native.Bounds(host) == physical && sidebar.Docked.Owner == host, "Desktop mode pane changed host");
        NativeTestInput.ShellClose(new WindowInteropHelper(sidebar.Docked).Handle);
        await System.Threading.Tasks.Task.Delay(150);
        Check(!sidebar.Expanded && host.IsVisible && !app.Exiting, "Closing attached pane exited host");
        results.Add("desktop-residency-kept-and-attached-close-only-collapses");

        app.Store.Update(s => { s.Settings.Topmost = true; s.Settings.DesktopMode = false; });
        app.ApplyWindows();
        app.SetDetached(true);
        var timeline = app.TaskWindow!;
        var timelineBounds = Native.Bounds(timeline);
        sidebar.Dock(timeline);
        Check(sidebar.Docked.Owner == timeline && Native.Bounds(timeline) == timelineBounds, "Timeline docking changed host bounds");
        sidebar.SetWidth(260);
        Check(Native.Bounds(timeline) == timelineBounds, "Timeline width changed with pane");
        app.SetDetached(false);
        Check(sidebar.Host == host && sidebar.Docked.Owner == host, "Merge did not transfer attached owner");
        results.Add("timeline-owner-and-merge-transfer-preserve-host-bounds");
    }
}
