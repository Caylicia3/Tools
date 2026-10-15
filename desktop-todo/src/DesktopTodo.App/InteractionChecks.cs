using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Windows.Shapes;

namespace DesktopTodo.App;

internal static class InteractionChecks
{
    internal static IEnumerable<T> Find<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T item)
            yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Find<T>(VisualTreeHelper.GetChild(root, i)))
                yield return child;
    }

    static T Named<T>(DependencyObject root, string name)
        where T : DependencyObject => Find<T>(root).First(x => AutomationProperties.GetName(x) == name);
    internal static async System.Threading.Tasks.Task Run(App app, Action<Window, string> capture, List<string> results)
    {
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        void Settings(Action<FormWindow> edit, bool save)
        {
            Exception? failure = null;
            bool done = false;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var w = app.Windows.OfType<FormWindow>().LastOrDefault();
                try
                {
                    if (w == null)
                        throw new Exception("Settings did not open");
                    edit(w);
                    if (save)
                        w.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else
                        w.Close();
                    done = true;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    w?.Close();
                }
            }), DispatcherPriority.ApplicationIdle);
            Dialogs.Settings(app.Planner);
            if (failure != null)
                throw failure;
            Check(done, "Settings action missing");
        }

        app.Store.Update(s =>
        {
            s.Settings.Topmost = true;
            s.Settings.DesktopMode = false;
        });
        app.ApplyWindows();
        Native.Activate(app.Planner);
        var originalSettings = System.Text.Json.JsonSerializer.Serialize(app.Store.Current.Settings);
        app.SetDetached(true);
        app.Store.Update(s => s.Settings.Detached = false, false); // Exercise both visible panels while keeping the combined preview visible.
        Settings(form =>
        {
            form.Fields.Children.OfType<ComboBox>().First().SelectedItem = "林间绿";
            var sliders = form.Fields.Children.OfType<Slider>().ToArray();
            sliders[0].Value = 42;
            sliders[1].Value = 22;
            sliders[2].Value = 36;
            Check(app.Appearance.Theme == "林间绿" && app.Appearance.Opacity == .42 && app.Appearance.TimelineBarHeight == 22 && app.Appearance.TimelineRowHeight == 36, "Live appearance did not update");
            Check(app.Store.Current.Settings.Opacity != .42, "Preview incorrectly persisted");
            Check(((SolidColorBrush)((Border)app.Planner.Content).Background).Opacity == .42, "Main preview missing");
            Check(((SolidColorBrush)((Border)app.TaskWindow!.Content).Background).Opacity == .42, "Detached preview missing");
            Check(((SolidColorBrush)form.Background).Color == (Color)ColorConverter.ConvertFromString("#F9F7EE"), "Settings did not follow theme");
            capture(app.Planner, "12-live-preview-thick-compact");
            capture(form, "13-green-settings");
            var colors = Find<TextBox>(form).Where(x => x.ToolTip is string tip && tip.Contains("#B7ABD4")).ToArray();
            Check(colors.Length == 3, "Color pickers missing");
            colors[0].Text = "#12";
            sliders[0].Value = 43;
            Check(app.Appearance.Opacity == .43, "Invalid color blocked independent slider preview");
        }, false);
        Check(app.Appearance.Theme == "雪景紫" && app.Appearance.Opacity == .95, "Cancel did not restore appearance");
        app.SetDetached(false);
        Check(System.Text.Json.JsonSerializer.Serialize(app.Store.Current.Settings) == originalSettings, "Canceled settings modified stored preferences");
        Settings(form =>
        {
            form.Fields.Children.OfType<ComboBox>().First().SelectedItem = "海岸蓝";
            var sliders = form.Fields.Children.OfType<Slider>().ToArray();
            sliders[0].Value = 88;
            sliders[1].Value = 6;
            sliders[2].Value = 144;
            capture(app.Planner, "14-live-preview-thin-tall");
        }, true);
        Check(app.Store.Current.Settings.Theme == "海岸蓝" && app.Store.Current.Settings.Opacity == .88 && app.Store.Current.Settings.TimelineRowHeight == 144, "Saved appearance missing");
        app.Store.Update(s =>
        {
            s.Settings.Theme = "雪景紫";
            s.Settings.Background = "";
            s.Settings.Foreground = "";
            s.Settings.Accent = "";
            s.Settings.Opacity = .95;
            s.Settings.TimelineBarHeight = 11;
            s.Settings.TimelineRowHeight = 36;
        });
        ThemeResources.Apply(app.Appearance);
        results.Add("appearance-preview-save-cancel-invalid-color-independent-sliders");
        foreach (var theme in Palette.All)
        {
            Settings(form =>
            {
                form.Fields.Children.OfType<ComboBox>().First().SelectedItem = theme.Name;
                var fields = Find<TextBox>(form).Where(x => x.ToolTip is string tip && tip.Contains("#B7ABD4")).ToArray();
                Check(fields[0].Text == theme.Background && fields[1].Text == theme.Ink && fields[2].Text == theme.Accent, "Preset color values did not synchronize");
                var editor = new FormWindow(form, "主题一致性检查");
                editor.Input("输入框", "检查文字");
                editor.Show();
                editor.UpdateLayout();
                try
                {
                    Check(((SolidColorBrush)editor.Background).Color == (Color)ColorConverter.ConvertFromString(theme.Background), "Editor background theme mismatch");
                    Check(((SolidColorBrush)Find<TextBox>(editor).First().Foreground).Color == (Color)ColorConverter.ConvertFromString(theme.Ink), "Editor input theme mismatch");
                }
                finally
                {
                    editor.Close();
                }
            }, false);
        }

        results.Add("all-four-themes-settings-fields-editors-synchronized");
        app.Planner.MonthView = false;
        app.Planner.Render();
        await System.Threading.Tasks.Task.Delay(180);
        var week = Named<ScrollViewer>(app.Planner, "周日程内容");
        NativeTestInput.PointAt(week, 120, 120);
        double oldZoom = app.Appearance.WeekZoom;
        await NativeTestInput.Wheel(120, true);
        Check(app.Appearance.WeekZoom == Math.Round(oldZoom + .1, 2), "Ctrl wheel did not zoom week");
        week = Named<ScrollViewer>(app.Planner, "周日程内容");
        double beforeScroll = week.VerticalOffset;
        NativeTestInput.PointAt(week, 120, 120);
        await NativeTestInput.Wheel(-120, false);
        Check(app.Appearance.WeekZoom == 1.1 && week.VerticalOffset != beforeScroll, "Ordinary wheel should scroll without zoom");
        app.ChangeView(p => p.WeekZoom = 2);
        await System.Threading.Tasks.Task.Delay(180);
        capture(app.Planner, "15-week-200-percent");
        app.ChangeView(p => p.WeekZoom = .5);
        await System.Threading.Tasks.Task.Delay(180);
        capture(app.Planner, "16-week-50-percent");
        app.Planner.MonthView = true;
        app.ChangeView(p => p.MonthZoom = 1.5);
        await System.Threading.Tasks.Task.Delay(180);
        capture(app.Planner, "17-month-150-percent");
        Check(app.Appearance.WeekZoom == .5 && app.Appearance.MonthZoom == 1.5, "Month/week zoom should be independent");
        app.Planner.MonthView = false;
        app.ChangeView(p => p.WeekZoom = 1.5);
        await System.Threading.Tasks.Task.Delay(180);
        week = Named<ScrollViewer>(app.Planner, "周日程内容");
        var weekCanvas = (Canvas)week.Content;
        week.ScrollToHorizontalOffset(0);
        week.ScrollToVerticalOffset(17 * 52 * 1.5);
        await System.Threading.Tasks.Task.Delay(160);
        double dayWidth = (weekCanvas.Width - 56) / 7;
        NativeTestInput.PointAt(weekCanvas, 56 + dayWidth * 2.5, 18 * 52 + 16);
        NativeTestInput.Down();
        await System.Threading.Tasks.Task.Delay(50);
        NativeTestInput.Up();
        await System.Threading.Tasks.Task.Delay(120);
        var editor = app.Windows.OfType<FormWindow>().LastOrDefault();
        Check(editor != null, "Scaled calendar click did not open editor");
        var date = editor!.Fields.Children.OfType<DatePicker>().First().SelectedDate;
        var inputs = editor.Fields.Children.OfType<TextBox>().ToArray();
        Check(date == CalendarLogic.WeekStart(app.Planner.SelectedDate).AddDays(2) && inputs[1].Text == "18:15", "Scaled calendar mapped click to wrong date/time");
        inputs[0].Text = "缩放定位检查";
        editor.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await System.Threading.Tasks.Task.Delay(100);
        Check(app.Store.Current.Events.Any(e => e.Title == "缩放定位检查" && e.Start.Hour == 18 && e.Start.Minute == 15), "Scaled edit not saved");
        app.Store.Undo();
        Check(!app.Store.Current.Events.Any(e => e.Title == "缩放定位检查"), "Undo failed after scaled edit");
        app.Store.Redo();
        Check(app.Store.Current.Events.Any(e => e.Title == "缩放定位检查"), "Redo failed after scaled edit");
        await System.Threading.Tasks.Task.Delay(150);
        week = Named<ScrollViewer>(app.Planner, "周日程内容");
        weekCanvas = (Canvas)week.Content;
        var card = Named<Border>(app.Planner, "日程：缩放定位检查");
        var dragStart = card.PointToScreen(new Point(16, 16));
        var dragThreshold = card.PointToScreen(new Point(26, 16));
        var destination = weekCanvas.PointToScreen(new Point(56 + dayWidth * 3.5, 18 * 52 + 16));
        await NativeTestInput.Drag(dragStart, dragThreshold, destination);
        await System.Threading.Tasks.Task.Delay(160);
        Check(app.Store.Current.Events.Single(e => e.Title == "缩放定位检查").Start == CalendarLogic.WeekStart(app.Planner.SelectedDate).AddDays(3).AddHours(18.25), "Scaled event drag mapped incorrectly: " + app.Store.Current.Events.Single(e => e.Title == "缩放定位检查").Start.ToString("s"));
        card = Named<Border>(app.Planner, "日程：缩放定位检查");
        var thumb = Find<System.Windows.Controls.Primitives.Thumb>(card).Single();
        var resizeStart = thumb.PointToScreen(new Point(Math.Max(1, thumb.ActualWidth / 2), 3));
        var lower = thumb.PointToScreen(new Point(Math.Max(1, thumb.ActualWidth / 2), 3 + 26));
        await NativeTestInput.Drag(resizeStart, resizeStart, lower);
        await System.Threading.Tasks.Task.Delay(160);
        Check(app.Store.Current.Events.Single(e => e.Title == "缩放定位检查").End.Hour == 19 && app.Store.Current.Events.Single(e => e.Title == "缩放定位检查").End.Minute == 45, "Scaled resize mapped incorrectly");
        var denseStart = CalendarLogic.WeekStart(app.Planner.SelectedDate).AddDays(3).AddHours(18);
        app.Store.Edit("密集重叠验收", s =>
        {
            for (int n = 0; n < 80; n++)
                s.Events.Add(new()
                {
                    Title = "密集日程 " + n,
                    Start = denseStart,
                    End = denseStart.AddHours(1)
                });
        });
        await System.Threading.Tasks.Task.Delay(160);
        Check(Find<Border>(app.Planner).Where(x => AutomationProperties.GetName(x).StartsWith("日程：密集日程")).All(x => double.IsFinite(x.ActualWidth) && x.ActualWidth >= 1), "Dense overlap width invalid");
        capture(app.Planner, "20-dense-overlap");
        app.Store.Undo();
        await System.Threading.Tasks.Task.Delay(100);
        results.Add("physical-calendar-wheel-zoom-scroll-scaled-click-move-resize-undo-redo-dense-overlap");
        app.ChangeView(p =>
        {
            p.WeekZoom = 1;
            p.MonthZoom = 1;
            p.TimelineDays = 30;
        });
        app.Planner.TimelineStart = DateTime.Today.AddDays(-4);
        app.Planner.Render();
        await System.Threading.Tasks.Task.Delay(150);
        var timeline = Named<ScrollViewer>(app.Planner, "任务时间轴内容");
        NativeTestInput.PointAt(timeline, 340, 60);
        await NativeTestInput.Wheel(120, true);
        Check(app.Appearance.TimelineDays == 25, "Timeline wheel did not zoom days");
        app.ChangeView(p => p.TimelineDays = 30);
        app.Planner.TimelineStart = DateTime.Today.AddDays(-4);
        var testTask = new TodoTask
        {
            Title = "交互检查任务",
            Start = app.Planner.TimelineStart,
            End = app.Planner.TimelineStart.AddDays(20),
            Milestones = [new()
            {
                Title = "拖动检查节点",
                Date = app.Planner.TimelineStart.AddDays(4)
            }

            ]
        };
        app.Store.Edit("添加交互检查任务", s => s.Tasks.Add(testTask));
        await System.Threading.Tasks.Task.Delay(150);
        var marker = Named<Polygon>(app.Planner, "节点：拖动检查节点");
        var canvas = (Canvas)VisualTreeHelper.GetParent(marker);
        double perDay = (canvas.Width - 165 - 12) / 30;
        var original = testTask.Milestones[0].Date;
        var target = original.AddDays(3);
        int history = app.Store.UndoCount;
        async System.Threading.Tasks.Task Drag()
        {
            marker = Named<Polygon>(app.Planner, "节点：拖动检查节点");
            NativeTestInput.PointAt(marker, 7, 7);
            NativeTestInput.Down();
            await System.Threading.Tasks.Task.Delay(70);
            var screen = marker.PointToScreen(new Point(7 + perDay * 3, 7));
            NativeTestInput.Move((int)screen.X, (int)screen.Y);
            await System.Threading.Tasks.Task.Delay(100);
            Check(app.Planner.NodeDragging, "Node did not enter preview drag");
            Check(app.Store.Current.Tasks.Single(t => t.Id == testTask.Id).Milestones[0].Date == original, "Node persisted before mouse release");
            Check(Find<TextBlock>(app.Planner).Any(x => x.Text == target.ToString("yyyy/MM/dd")), "Node preview date missing");
            capture(app.Planner, "18-node-date-preview");
            NativeTestInput.Up();
            await System.Threading.Tasks.Task.Delay(100);
            Check(!NativeTestInput.AnswerNodeMove(false), "Unexpected node confirmation");
            await System.Threading.Tasks.Task.Delay(120);
        }

        await Drag();
        Check(app.Store.UndoCount == history + 1 && app.Store.Current.Tasks.Single(t => t.Id == testTask.Id).Milestones[0].Date == target, "Released node move not saved");
        app.Store.Undo();
        Check(app.Store.Current.Tasks.Single(t => t.Id == testTask.Id).Milestones[0].Date == original, "Node undo failed");
        await System.Threading.Tasks.Task.Delay(140);
        app.Planner.UpdateLayout();
        var bar = Named<Border>(app.Planner, "任务条：交互检查任务");
        NativeTestInput.PointAt(bar, 20, 11);
        NativeTestInput.Down();
        await System.Threading.Tasks.Task.Delay(50);
        var barPoint = bar.PointToScreen(new Point(70, 11));
        NativeTestInput.Move((int)barPoint.X, (int)barPoint.Y);
        await System.Threading.Tasks.Task.Delay(70);
        NativeTestInput.Up();
        await System.Threading.Tasks.Task.Delay(120);
        Check(!app.Windows.OfType<FormWindow>().Any() && app.Store.Current.Tasks.Single(t => t.Id == testTask.Id).Start == testTask.Start, "Task bar drag must do nothing");
        history = app.Store.UndoCount;
        foreach (string mode in new[]
        {
            "escape",
            "outside",
            "range",
            "same"
        }

        )
        {
            marker = Named<Polygon>(app.Planner, "节点：拖动检查节点");
            var home = marker.PointToScreen(new Point(7, 7));
            NativeTestInput.Move((int)home.X, (int)home.Y);
            NativeTestInput.Down();
            await System.Threading.Tasks.Task.Delay(50);
            var dest = marker.PointToScreen(new Point(mode == "same" ? 13 : 7 + perDay * (mode == "range" ? 18 : 3), mode == "outside" ? 70 : 7));
            NativeTestInput.Move((int)dest.X, (int)dest.Y);
            await System.Threading.Tasks.Task.Delay(80);
            if (mode == "escape")
            {
                await NativeTestInput.Wheel(120, true);
                Check(app.Appearance.TimelineDays == 30, "Wheel zoom was not paused during node drag");
                NativeTestInput.Key(0x1B);
                NativeTestInput.Key(0x1B, true);
                await System.Threading.Tasks.Task.Delay(60);
                NativeTestInput.Move((int)home.X, (int)home.Y);
            }

            NativeTestInput.Up();
            await System.Threading.Tasks.Task.Delay(120);
            Check(!app.Planner.NodeDragging && app.Store.UndoCount == history && app.Store.Current.Tasks.Single(t => t.Id == testTask.Id).Milestones[0].Date == original, "Canceled/invalid node drag committed: " + mode);
        }

        results.Add("physical-node-preview-direct-save-undo-escape-outside-range-same-date-zoom-lock-task-bar-locked");
        foreach (int count in new[]
        {
            1,
            14,
            3650
        }

        )
        {
            app.ChangeView(p => p.TimelineDays = count);
            app.Planner.UpdateLayout();
            capture(app.Planner, "days-" + count);
        }

        app.ChangeView(p => p.TimelineDays = 30);
        app.FlushViews();
        app.SetDetached(true);
        app.Planner.WindowState = WindowState.Minimized;
        await System.Threading.Tasks.Task.Delay(140);
        Check(app.Planner.WindowState == WindowState.Minimized, "Minimize failed");
        Native.Activate(app.Planner);
        await System.Threading.Tasks.Task.Delay(140);
        Check(app.Planner.WindowState == WindowState.Normal, "Taskbar restore failed");
        Check(app.Planner.ShowInTaskbar && app.TaskWindow!.ShowInTaskbar, "Taskbar flags missing");
        app.SetDetached(false);
        app.Store.Update(s =>
        {
            s.Settings.Topmost = false;
            s.Settings.DesktopMode = false;
        });
        app.ApplyWindows();
        results.Add("custom-range-bounds-detached-taskbar-minimize-restore");
    }

    internal static bool ClickTaskbar()
    {
        var bar = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        if (bar == null)
            return false;
        foreach (AutomationElement element in bar.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
        {
            var name = element.Current.Name;
            if (!name.Contains("日程管理") && !name.Contains("DesktopTodo"))
                continue;
            var r = element.Current.BoundingRectangle;
            if (r.Width < 1 || r.Height < 1)
                continue;
            NativeTestInput.Move((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2));
            NativeTestInput.Down();
            NativeTestInput.Up();
            return true;
        }

        return false;
    }

    internal static string[] TaskbarEntries()
    {
        var bar = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        if (bar == null)
            return [];
        var buttons = bar.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
        var names = buttons.Cast<AutomationElement>().Select(x => x.Current.Name).ToArray();
        File.WriteAllLines(System.IO.Path.Combine(App.Instance.DataDir, "taskbar-diagnostic.txt"), names);
        return names.Where(x => x.Contains("日程管理") || x.Contains("DesktopTodo")).ToArray();
    }
}

internal static class NativeTestInput
{
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")]
    static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")]
    static extern IntPtr GetDlgItem(IntPtr window, int id);
    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    internal static void Release()
    {
        Up();
        foreach (byte key in new byte[]
        {
            0x11,
            0x12,
            0x10,
            0x5B,
            0x73
        }

        )
            Key(key, true);
    }

    internal static void Move(int x, int y) => SetCursorPos(x, y);
    internal static void PointAt(FrameworkElement element, double x, double y)
    {
        var point = element.PointToScreen(new Point(x, y));
        Move((int)point.X, (int)point.Y);
    }

    // WPF drag-and-drop runs a native message loop. Deliver the entire gesture on
    // a worker so releasing the mouse does not depend on a UI-thread continuation.
    // Capture screen coordinates before the gesture; a drop can rebuild the view.
    internal static System.Threading.Tasks.Task Drag(Point start, Point threshold, Point end) => System.Threading.Tasks.Task.Run(async () =>
    {
        Move((int)start.X, (int)start.Y);
        await System.Threading.Tasks.Task.Delay(80);
        Down();
        try
        {
            await System.Threading.Tasks.Task.Delay(100);
            Move((int)threshold.X, (int)threshold.Y);
            await System.Threading.Tasks.Task.Delay(150);
            Move((int)end.X, (int)end.Y);
            await System.Threading.Tasks.Task.Delay(200);
        }
        finally
        {
            Up();
        }
    });
    internal static void Down() => mouse_event(2, 0, 0, 0, UIntPtr.Zero);
    internal static void Up() => mouse_event(4, 0, 0, 0, UIntPtr.Zero);
    internal static void Key(byte key, bool up = false) => keybd_event(key, 0, up ? 2u : 0u, UIntPtr.Zero);
    internal static async System.Threading.Tasks.Task Wheel(int delta, bool control)
    {
        if (control)
        {
            Key(0x11);
            await System.Threading.Tasks.Task.Delay(40);
        }

        mouse_event(0x800, 0, 0, unchecked((uint)delta), UIntPtr.Zero);
        await System.Threading.Tasks.Task.Delay(120);
        if (control)
            Key(0x11, true);
        await System.Threading.Tasks.Task.Delay(100);
    }

    internal static void ShellClose(IntPtr window) => PostMessage(window, 0x112, new IntPtr(0xF060), IntPtr.Zero);
    internal static void WinD()
    {
        Key(0x5B);
        Key(0x44);
        Key(0x44, true);
        Key(0x5B, true);
    }

    internal static bool AnswerNodeMove(bool yes)
    {
        var dialog = FindWindow("#32770", "移动节点");
        if (dialog == IntPtr.Zero)
            return false;
        return PostMessage(GetDlgItem(dialog, yes ? 6 : 7), 0xF5, IntPtr.Zero, IntPtr.Zero);
    }
}
