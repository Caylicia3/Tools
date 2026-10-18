using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Automation;

namespace DesktopTodo.App;

internal static class SidebarChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
    internal static async System.Threading.Tasks.Task Run(App app, Action<Window, string> capture, List<string> results)
    {
        var controller = app.Sidebar!;
        var view = controller.View;
        Check(!controller.Expanded && !controller.IsFloating && controller.Host == app.Planner, "Sidebar must start collapsed on calendar");
        app.Store.Update(s => { s.Settings.Topmost = true; s.Settings.DesktopMode = false; });
        app.ApplyWindows();
        app.Planner.Width = 900;
        app.Planner.Height = 760;
        app.Planner.Left = 24;
        app.Planner.Top = 24;
        Native.Activate(app.Planner);
        await System.Threading.Tasks.Task.Delay(250);
        var calendarScroll = InteractionChecks.Find<ScrollViewer>(app.Planner).First(x => AutomationProperties.GetName(x) == "周日程内容");
        calendarScroll.ScrollToVerticalOffset(8 * 52);
        await System.Threading.Tasks.Task.Delay(150);
        double initialWidth = app.Planner.Width;
        capture(app.Planner, "sidebar-00-collapsed-handle");
        Check(!InteractionChecks.Find<TextBlock>(app.Planner.SidebarHandle).Any(), "Handle must not contain visible text");
        controller.Show();
        await System.Threading.Tasks.Task.Delay(300);
        Check(controller.Expanded && view.IsVisible && view.ActualWidth >= 230, "Sidebar did not open");
        calendarScroll = InteractionChecks.Find<ScrollViewer>(app.Planner).First(x => AutomationProperties.GetName(x) == "周日程内容");
        Check(calendarScroll.VerticalOffset > 400, "Sidebar opening reset calendar scroll");
        var frame = app.Planner.MainFrame.ActualWidth;
        Check(frame > 350 && Math.Abs(frame - app.Planner.ActualWidth) < 1, "Sidebar changed planner content width");
        view.AddItem();
        await System.Threading.Tasks.Task.Delay(150);
        var first = app.Store.Current.Sidebar.Content.Items.Last();
        var title = view.Editors[first.Id + ":title"];
        title.SelectAll();
        title.Selection.Text = "学习 C 语言 · 每周阅读";
        title.SelectAll();
        title.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, PlannerWindow.Brush("#7F6890"));
        title.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 18 * 4d / 3);
        await System.Threading.Tasks.Task.Delay(850);
        Check(app.Store.Current.Sidebar.Content.Items.Last().Title.PlainText.Contains("学习 C"), "Inline title autosave failed");
        Check(app.Store.Current.Sidebar.Content.Items.Last().Title.Runs.Any(r => r.Size == 18 && r.Color == "#7F6890"), "Title formatting lost");
        var note = view.NoteEditor;
        note.Focus();
        note.SelectAll();
        note.Selection.Text = "我的长期目标\n每天读一点，持续记录想法。\n可以没有截止日期。🙂";
        note.SelectAll();
        note.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, PlannerWindow.Brush("#31446C"));
        view.Flush(true);
        string noteBefore = app.Store.Current.Sidebar.Content.Note.PlainText;
        var nativeDocument = note.Document;
        note.Selection.Select(note.Document.ContentStart, note.Document.ContentEnd);
        app.Planner.Render();
        app.Planner.RefreshClock();
        var appearance = Copies.Clone(app.Store.Current.Settings);
        appearance.Theme = "林间绿";
        appearance.Opacity = .6;
        app.PreviewAppearance(appearance);
        Check(ReferenceEquals(nativeDocument, note.Document) && note.Selection.Text.Contains("长期目标"), "Refresh replaced editor or selection");
        app.PreviewAppearance(null);
        await System.Threading.Tasks.Task.Delay(180);
        capture(app.Planner, "sidebar-01-fixed-host");
        capture(controller.Docked, "sidebar-01-content");
        results.Add("sidebar-inline-rich-text-autosave-stable-selection");

        void Dialog(Action open, Action<FormWindow> edit, bool accept)
        {
            Exception? error = null;
            bool visited = false;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var form = app.Windows.OfType<FormWindow>().LastOrDefault();
                try
                {
                    Check(form != null, "Sidebar format dialog missing");
                    edit(form!);
                    visited = true;
                    if (accept)
                        form!.SaveButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    else
                        form!.Close();
                }
                catch (Exception ex) { error = ex; form?.Close(); }
            }), DispatcherPriority.ApplicationIdle);
            open();
            if (error != null)
                throw error;
            Check(visited, "Sidebar format dialog unvisited");
        }
        Button Button(string name, bool last = true) => last ? InteractionChecks.Find<Button>(view).Last(b => AutomationProperties.GetName(b) == name) : InteractionChecks.Find<Button>(view).First(b => AutomationProperties.GetName(b) == name);
        string originalFormat = RichEditing.Fingerprint(RichEditing.Read(note));
        Dialog(() => Button("字色").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)), form =>
        {
            InteractionChecks.Find<TextBox>(form).First().Text = "#FC8C3A";
            Check(RichEditing.Read(note).Runs.Any(r => r.Color == "#FC8C3A"), "Color live preview missing");
        }, false);
        Check(RichEditing.Fingerprint(RichEditing.Read(note)) == originalFormat, "Color cancel failed");
        note.SelectAll();
        Dialog(() => Button("字号").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)), form => InteractionChecks.Find<TextBox>(form).First().Text = "24", true);
        Check(app.Store.Current.Sidebar.Content.Note.Runs.Any(r => r.Size == 24), "Size dialog commit failed");
        app.Store.Undo();
        Check(app.Store.Current.Sidebar.Content.Note.Runs.All(r => r.Size != 24), "Format undo failed");
        app.Store.Redo();
        Dialog(() => Button("背景").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)), form => InteractionChecks.Find<TextBox>(form).First().Text = "#F2EDD4", true);
        Check(app.Store.Current.Sidebar.Settings.NoteBackground == "#F2EDD4" && app.Store.Current.Sidebar.Settings.ListBackground == "#FFFFFF", "Paper colors not independent");
        view.Zoom(true, 2);
        view.Zoom(false, .5);
        Check(app.Store.Current.Sidebar.Settings.NoteZoom == 2 && app.Store.Current.Sidebar.Settings.ListZoom == .5 && app.Store.Current.Sidebar.Content.Note.Runs.Any(r => r.Size == 24), "Zoom changed text or other region");
        capture(controller.Docked, "sidebar-02-format-zoom");
        view.Zoom(true, 1);
        view.Zoom(false, 1);
        results.Add("sidebar-color-size-preview-cancel-undo-independent-background-zoom");

        view.AddItem();
        await System.Threading.Tasks.Task.Delay(120);
        var second = app.Store.Current.Sidebar.Content.Items.Last();
        InteractionChecks.Find<ScrollViewer>(view).First(s => s.Content is StackPanel).ScrollToTop();
        await System.Threading.Tasks.Task.Delay(150);
        capture(app.Planner, "sidebar-before-sort");
        var grips = InteractionChecks.Find<TextBlock>(view).Where(t => t.Text == "↕").ToArray();
        var sourceGrip = grips.Last();
        Native.Activate(app.Planner);
        // Foreground activation can be denied while another application owns the input focus.
        // Click the unobscured header as a user would before driving an overlapping split window.
        NativeTestInput.PointAt(app.Planner, 80, 30);
        NativeTestInput.Down();
        await System.Threading.Tasks.Task.Delay(40);
        NativeTestInput.Up();
        await System.Threading.Tasks.Task.Delay(180);
        var sortingStart = sourceGrip.PointToScreen(new Point(sourceGrip.ActualWidth / 2, sourceGrip.ActualHeight / 2));
        var sortingEnd = title.PointToScreen(new Point(10, 2));
        var diagnostic = new
        {
            main = new System.Windows.Interop.WindowInteropHelper(app.Planner).Handle.ToInt64(),
            other = app.TaskWindow == null ? 0 : new System.Windows.Interop.WindowInteropHelper(app.TaskWindow).Handle.ToInt64(),
            active = app.Planner.IsActive,
            foreground = NativeTestInput.GetForegroundWindow().ToInt64(),
            sourceWindow = NativeTestInput.WindowFromPoint(new System.Drawing.Point((int)sortingStart.X, (int)sortingStart.Y)).ToInt64(),
            targetWindow = NativeTestInput.WindowFromPoint(new System.Drawing.Point((int)sortingEnd.X, (int)sortingEnd.Y)).ToInt64(),
            sortingStart,
            sortingEnd
        };
        File.WriteAllText(Path.Combine(app.DataDir, "sort-diagnostic.json"), System.Text.Json.JsonSerializer.Serialize(diagnostic));
        long paneHandle = new System.Windows.Interop.WindowInteropHelper(controller.Docked).Handle.ToInt64();
        Check(diagnostic.sourceWindow == paneHandle && diagnostic.targetWindow == paneHandle, "Test gesture is occluded by another window");
        await NativeTestInput.Drag(sortingStart, new Point(sortingStart.X, sortingStart.Y - 12), sortingEnd);
        await System.Threading.Tasks.Task.Delay(150);
        Check(app.Store.Current.Sidebar.Content.Items[0].Id == second.Id, "List reorder failed");
        app.Store.Undo();
        Check(app.Store.Current.Sidebar.Content.Items.Last().Id == second.Id, "List reorder undo failed");
        app.Store.Edit("完成", s => s.Sidebar.Content.Items.Single(x => x.Id == first.Id).Completed = true);
        app.Store.Update(s => s.Sidebar.Settings.ShowCompleted = false);
        Check(!title.IsVisible, "Completed filter failed");
        app.Store.Update(s => s.Sidebar.Settings.ShowCompleted = true);
        Check(title.IsVisible, "Completed item did not return");
        controller.Toggle();
        await System.Threading.Tasks.Task.Delay(180);
        Check(!controller.Expanded && Math.Abs(app.Planner.Width - initialWidth) < 2, "Collapse did not restore original width");
        controller.Show();
        controller.Detach();
        await System.Threading.Tasks.Task.Delay(200);
        Check(controller.IsFloating && controller.Floating!.IsVisible && note.IsVisible, "Detach failed");
        var floating = controller.Floating!;
        floating.Close();
        Check(!floating.IsVisible && app.Planner.IsVisible && !app.Exiting, "Floating close did not hide only sidebar");
        controller.Show();
        Check(floating.IsVisible, "Hidden sidebar not restored");
        capture(floating, "sidebar-03-floating");
        NativeTestInput.ShellClose(new System.Windows.Interop.WindowInteropHelper(floating).Handle);
        await System.Threading.Tasks.Task.Delay(180);
        Check(!floating.IsVisible && !app.Exiting, "Shell close should hide floating sidebar");
        controller.Show();
        await System.Threading.Tasks.Task.Delay(150);
        NativeTestInput.Key(0x12);
        NativeTestInput.Key(0x73);
        await System.Threading.Tasks.Task.Delay(60);
        NativeTestInput.Key(0x73, true);
        NativeTestInput.Key(0x12, true);
        await System.Threading.Tasks.Task.Delay(180);
        Check(!floating.IsVisible && !app.Exiting, "Alt F4 should hide floating sidebar");
        controller.Show();
        await System.Threading.Tasks.Task.Delay(150);
        results.Add("sidebar-floating-close-shell-alt-f4-hide-only");
        var floatingGrip = InteractionChecks.Find<TextBlock>(floating).First(t => t.Text == "待办与笔记");
        var dragStart = floatingGrip.PointToScreen(new Point(20, 10));
        var hostBounds = Native.Bounds(app.Planner);
        var dragEnd = new Point(hostBounds.X + hostBounds.Width, hostBounds.Y + 100);
        await NativeTestInput.Drag(dragStart, new Point(dragStart.X + 10, dragStart.Y), dragEnd);
        await System.Threading.Tasks.Task.Delay(200);
        Check(!controller.IsFloating && controller.Host == app.Planner, "Native drag to dock failed");
        controller.Detach();
        results.Add("sidebar-native-window-docking");
        app.SetDetached(true);
        controller.Dock(app.TaskWindow!);
        await System.Threading.Tasks.Task.Delay(200);
        Check(!controller.IsFloating && controller.Host == app.TaskWindow && note.IsVisible, "Dock to timeline failed");
        app.SetDetached(false);
        await System.Threading.Tasks.Task.Delay(200);
        Check(controller.Host == app.Planner && controller.Expanded && note.IsVisible, "Merge did not transfer sidebar");
        app.SetDetached(true);
        Check(controller.Host == app.Planner, "Split moved sidebar away from calendar");
        app.SetDetached(false);
        results.Add("sidebar-reorder-complete-collapse-detach-hide-dock-merge");

        controller.SetWidth(640);
        app.Planner.WindowState = WindowState.Maximized;
        await System.Threading.Tasks.Task.Delay(250);
        app.Planner.UpdateLayout();
        Check(view.ActualWidth <= app.Planner.ActualWidth && app.Planner.MainFrame.ActualWidth > 0, "Maximized layout invalid");
        capture(app.Planner, "sidebar-04-maximized-wide");
        app.Planner.WindowState = WindowState.Normal;
        controller.SetWidth(320);
        await System.Threading.Tasks.Task.Delay(250);
        var grip = app.Planner.SidebarHandle;
        var point = grip.PointToScreen(new Point(grip.ActualWidth / 2, grip.ActualHeight / 2));
        await NativeTestInput.Drag(point, new Point(point.X - 12, point.Y), new Point(point.X + (controller.OnRight ? -1 : 1) * (controller.OpenWidth - 20) * VisualTreeHelper.GetDpi(app.Planner).DpiScaleX, point.Y));
        await System.Threading.Tasks.Task.Delay(200);
        Check(!controller.Expanded, "Native handle drag did not collapse");
        controller.Show();
        await System.Threading.Tasks.Task.Delay(200);
        NativeTestInput.PointAt(note, 15, 15);
        await NativeTestInput.Wheel(120, true);
        Check(app.Store.Current.Sidebar.Settings.NoteZoom == 1.1, "Native Ctrl wheel did not zoom note");
        view.Zoom(true, 1);
        results.Add("sidebar-maximized-native-handle-drag-wheel");

        foreach (var palette in Palette.All)
        {
            app.Store.Update(s => s.Settings.Theme = palette.Name);
            Check(app.Store.Current.Sidebar.Settings.NoteBackground == "#F2EDD4", "Theme overwrote custom paper");
            capture(app.Planner, "sidebar-theme-" + palette.Name);
        }
        app.Store.Update(s => s.Settings.Theme = "雪景紫");
        foreach (double opacity in new[] { .01, .35, .95, 1d })
        {
            app.Store.Update(s => s.Settings.Opacity = opacity);
            Check(Math.Abs(app.Planner.MainFrame.Background.Opacity - opacity) < .001, "Drawer changed main background opacity");
            capture(app.Planner, "sidebar-opacity-" + (int)(opacity * 100));
        }
        app.Store.Update(s => s.Settings.Opacity = .95);
        results.Add("sidebar-handle-no-text-theme-opacity-layering");
        var backup = Path.Combine(app.DataDir, "sidebar.backup.json");
        app.Store.Backup(backup);
        app.Store.Edit("clear", s => s.Sidebar.Content = new());
        app.Store.Restore(backup);
        Check(app.Store.Current.Sidebar.Content.Note.PlainText == noteBefore, "Sidebar restore failed");
        capture(controller.Docked, "sidebar-05-final");
        results.Add("sidebar-themes-backup-restore");
        // Keep a dirty edit for the normal shutdown path; the outer harness verifies it on disk.
        note.CaretPosition = note.Document.ContentEnd;
        note.AppendText("\n退出前保存检查");
    }
}
