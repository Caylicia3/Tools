using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopTodo.App;

internal static class UpdateChecks
{
    static T Named<T>(DependencyObject root, string name)
        where T : DependencyObject => InteractionChecks.Find<T>(root).First(x => AutomationProperties.GetName(x) == name);
    static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    internal static async System.Threading.Tasks.Task Run(App app, Action<Window, string> capture, List<string> results)
    {
        void Edit(Action open, Action<FormWindow> change)
        {
            Exception? failure = null;
            bool done = false;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var form = app.Windows.OfType<FormWindow>().LastOrDefault();
                try
                {
                    Check(form != null, "Editor missing");
                    change(form!);
                    form!.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(!form.IsVisible, "Editor did not save");
                    done = true;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    form?.Close();
                }
            }), DispatcherPriority.ApplicationIdle);
            open();
            if (failure != null)
                throw failure;
            Check(done, "Editor check not completed");
        }

        app.Store.Update(s =>
        {
            s.Settings.Topmost = true;
            s.Settings.DesktopMode = false;
        });
        app.ApplyWindows();
        Native.Activate(app.Planner);
        var task = new TodoTask
        {
            Title = "不提醒独立设置验收",
            Start = DateTime.Today,
            End = DateTime.Today.AddDays(10),
            ReminderDaysBefore = 2,
            ReminderHour = 10,
            ReminderMinute = 25,
            Milestones = [new()
            {
                Title = "独立节点提醒",
                Date = DateTime.Today.AddDays(5)
            }

            ]
        };
        app.Store.Edit("添加提醒验收任务", s => s.Tasks.Add(task));
        Edit(() => Dialogs.Task(app.Planner, app.Store.Current.Tasks.Single(t => t.Id == task.Id)), form =>
        {
            var days = Named<TextBox>(form, "提前提醒天数");
            var time = Named<TextBox>(form, "提醒时刻");
            days.Text = "未填完";
            time.Text = "09:";
            Named<ComboBox>(form, "提醒方式").SelectedIndex = 1;
            Check(!days.IsEnabled && !time.IsEnabled, "Disabled reminder inputs should be inactive");
            capture(form, "21-task-no-reminder");
        });
        var saved = app.Store.Current.Tasks.Single(t => t.Id == task.Id);
        Check(!saved.Reminder && saved.Milestones[0].Reminder && saved.ReminderDaysBefore == 2 && saved.ReminderHour == 10 && saved.ReminderMinute == 25, "Task off lost node switch or previous schedule");
        Edit(() => Dialogs.EditSavedMilestone(app.Planner, saved, saved.Milestones[0]), form =>
        {
            Named<ComboBox>(form, "提醒方式").SelectedIndex = 1;
            capture(form, "22-node-no-reminder");
        });
        Check(!app.Store.Current.Tasks.Single(t => t.Id == task.Id).Milestones[0].Reminder, "Node switch not persisted");
        app.Store.Undo();
        Check(!app.Store.Current.Tasks.Single(t => t.Id == task.Id).Reminder && app.Store.Current.Tasks.Single(t => t.Id == task.Id).Milestones[0].Reminder, "Node undo altered parent reminder");
        results.Add("task-node-no-reminder-independent-disabled-inputs-save-undo");
        app.SetDetached(true);
        foreach (bool desktop in new[]
        {
            false,
            true
        }

        )
        {
            app.Store.Update(s =>
            {
                s.Settings.DesktopMode = desktop;
                s.Settings.Topmost = !desktop;
            });
            app.ApplyWindows();
            foreach (var panel in new[]
            {
                app.Planner,
                app.TaskWindow!
            }

            )
            {
                Native.Activate(panel);
                await System.Threading.Tasks.Task.Delay(200);
                var before = Native.Bounds(panel);
                double width = panel.TasksOnly ? app.Store.Current.Settings.TaskWidth : app.Store.Current.Settings.Width;
                Named<Button>(panel, "最大化或还原").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await System.Threading.Tasks.Task.Delay(350);
                var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(panel).Handle).WorkingArea;
                var b = Native.Bounds(panel);
                Check(panel.WindowState == WindowState.Maximized, "Maximize button did not maximize");
                Check(Math.Abs(b.X - area.X) <= 3 && Math.Abs(b.Y - area.Y) <= 3 && Math.Abs(b.Width - area.Width) <= 3 && Math.Abs(b.Height - area.Height) <= 3, "Maximized bounds cover taskbar or wrong monitor: " + b + " vs " + area);
                Check((panel.TasksOnly ? app.Store.Current.Settings.TaskWidth : app.Store.Current.Settings.Width) == width, "Maximize overwrote normal bounds");
                capture(panel, "23-max-" + desktop + "-" + panel.TasksOnly);
                panel.WindowState = WindowState.Minimized;
                await System.Threading.Tasks.Task.Delay(100);
                Native.Activate(panel);
                await System.Threading.Tasks.Task.Delay(200);
                Check(panel.WindowState == WindowState.Maximized, "Restore from minimize lost maximized state");
                Named<Button>(panel, "最大化或还原").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await System.Threading.Tasks.Task.Delay(350);
                b = Native.Bounds(panel);
                Check(panel.WindowState == WindowState.Normal && Math.Abs(b.X - before.X) <= 3 && Math.Abs(b.Y - before.Y) <= 3 && Math.Abs(b.Width - before.Width) <= 3 && Math.Abs(b.Height - before.Height) <= 3, "Restore did not recover previous size and position");
            }
        }

        results.Add("maximize-workarea-restore-minimize-both-panels-desktop-floating");
        app.SetDetached(false);
        app.Store.Update(s =>
        {
            s.Settings.DesktopMode = false;
            s.Settings.Topmost = true;
        });
        app.ApplyWindows();
        Native.Activate(app.Planner);
        app.ChangeView(p => p.TimelineDays = 30);
        app.Planner.TimelineStart = DateTime.Today.AddDays(-4);
        app.Planner.Render();
        await System.Threading.Tasks.Task.Delay(200);
        string contents = JsonSerializer.Serialize(new
        {
            app.Store.Current.Tasks,
            app.Store.Current.Events
        });
        int history = app.Store.UndoCount;
        bool redo = app.Store.CanRedo;
        async System.Threading.Tasks.Task Pan(int direction, bool cancel, bool lose = false)
        {
            var start = app.Planner.TimelineStart;
            var scroll = Named<ScrollViewer>(app.Planner, "任务时间轴内容");
            scroll.ScrollToVerticalOffset(0);
            await System.Threading.Tasks.Task.Delay(100);
            var canvas = (Canvas)scroll.Content;
            double plot = canvas.Width - 165 - 12, perDay = plot / 30;
            var home = canvas.PointToScreen(new Point(165 + plot * .45, 16));
            var dest = canvas.PointToScreen(new Point(165 + plot * .45 + direction * 3 * perDay, 16));
            NativeTestInput.Move((int)home.X, (int)home.Y);
            NativeTestInput.Down();
            await System.Threading.Tasks.Task.Delay(70);
            NativeTestInput.Move((int)dest.X, (int)dest.Y);
            await System.Threading.Tasks.Task.Delay(200);
            Check(app.Planner.TimelinePanning, "Timeline did not capture pan");
            Check(app.Planner.TimelineStart == start.AddDays(-direction * 3), "Timeline live pan date mismatch");
            Check(JsonSerializer.Serialize(new
            {
                app.Store.Current.Tasks,
                app.Store.Current.Events
            }) == contents, "Panning modified task/event dates");
            capture(app.Planner, "24-pan-" + direction + "-" + cancel + "-" + lose);
            if (cancel)
            {
                await NativeTestInput.Wheel(120, true);
                Check(app.Appearance.TimelineDays == 30, "Pan did not lock zoom");
                NativeTestInput.Key(0x1B);
                NativeTestInput.Key(0x1B, true);
            }

            if (lose)
                Mouse.Capture(null);
            NativeTestInput.Up();
            await System.Threading.Tasks.Task.Delay(160);
            Check(!app.Planner.TimelinePanning && app.Planner.TimelineStart == ((cancel || lose) ? start : start.AddDays(-direction * 3)), "Pan finish/cancel mismatch");
            Check(app.Store.UndoCount == history && app.Store.CanRedo == redo, "View pan changed content history");
        }

        await Pan(1, false);
        await Pan(-1, false);
        await Pan(1, true);
        await Pan(-1, false, true);
        app.Planner.TimelineStart = DateTime.Today.AddYears(8);
        app.Planner.Render();
        await System.Threading.Tasks.Task.Delay(100);
        await Pan(1, false);
        results.Add("physical-timeline-pan-live-both-directions-empty-range-escape-capture-loss-zoom-lock-no-content-history");
        var beforeBar = app.Planner.TimelineStart;
        var browse = Named<System.Windows.Controls.Primitives.ScrollBar>(app.Planner, "时间轴横向滚动条");
        browse.Value += 7;
        await System.Threading.Tasks.Task.Delay(150);
        Check(app.Planner.TimelineStart == beforeBar.AddDays(7), "Scrollbar did not shift date range");
        var track = InteractionChecks.Find<System.Windows.Controls.Primitives.Track>(browse).Single();
        var thumb = track.Thumb;
        NativeTestInput.PointAt(thumb, thumb.ActualWidth / 2, thumb.ActualHeight / 2);
        NativeTestInput.Down();
        await System.Threading.Tasks.Task.Delay(50);
        var dest = thumb.PointToScreen(new Point(thumb.ActualWidth / 2 - 35, thumb.ActualHeight / 2));
        NativeTestInput.Move((int)dest.X, (int)dest.Y);
        await System.Threading.Tasks.Task.Delay(150);
        Check(thumb.IsDragging && app.Planner.TimelineStart < beforeBar.AddDays(7), "Horizontal thumb did not preview while dragging");
        capture(app.Planner, "25-horizontal-scrollbar-live");
        NativeTestInput.Up();
        await System.Threading.Tasks.Task.Delay(120);
        Check(JsonSerializer.Serialize(new
        {
            app.Store.Current.Tasks,
            app.Store.Current.Events
        }) == contents && app.Store.UndoCount == history, "Scrollbar changed content or history");
        Check(Math.Round(browse.Value) == app.Planner.TimelineStart.Ticks / (double)TimeSpan.TicksPerDay, "Scrollbar value and date not synchronized");
        results.Add("physical-horizontal-scrollbar-thumb-live-range-synchronization-no-content-change");
        app.Planner.TimelineStart = DateTime.Today.AddDays(-4);
        app.Planner.Render();
        app.Store.Update(s =>
        {
            s.Settings.Topmost = false;
            s.Settings.DesktopMode = false;
        });
        app.ApplyWindows();
    }
}
