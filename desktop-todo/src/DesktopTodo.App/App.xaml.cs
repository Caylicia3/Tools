using System.Threading;
using System.Security;
using System.Text;
using System.Security.Cryptography;
using System.Windows.Threading;
using Microsoft.Windows.AppNotifications;
using Microsoft.Win32;

namespace DesktopTodo.App;

public partial class App : Application
{
    internal static App Instance => (App)Current;
    internal Store Store { get; private set; } = null!;

    Preferences? preview, viewDraft;
    readonly DispatcherTimer viewSaveTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(450)
    };
    internal Preferences Appearance => preview ?? viewDraft ?? Store.Current.Settings;

    internal void PreviewAppearance(Preferences? value)
    {
        preview = value;
        ThemeResources.Apply(Appearance);
        RefreshWindows();
    }

    internal void ChangeView(Action<Preferences> change)
    {
        viewDraft ??= Copies.Clone(Store.Current.Settings);
        change(viewDraft);
        viewSaveTimer.Stop();
        viewSaveTimer.Start();
        RefreshWindows();
    }

    internal void FlushViews()
    {
        viewSaveTimer.Stop();
        if (viewDraft == null || Store == null)
            return;
        var p = viewDraft;
        Store.Update(s =>
        {
            s.Settings.TimelineDays = p.TimelineDays;
            s.Settings.WeekZoom = p.WeekZoom;
            s.Settings.MonthZoom = p.MonthZoom;
        }, false);
        viewDraft = null;
    }

    internal void RefreshWindows()
    {
        Planner?.Render();
        TaskWindow?.Render();
    }

    internal PlannerWindow Planner { get; private set; } = null!;
    internal PlannerWindow? TaskWindow
    {
        get; private set;
    }
    internal bool Exiting
    {
        get; private set;
    }
    internal bool Smoke
    {
        get; private set;
    }
    internal bool NotificationCheck
    {
        get; private set;
    }
    internal uint LastNotificationId
    {
        get; private set;
    }
    internal string DataDir { get; private set; } = "";
    internal string NotificationStatus { get; private set; } = "托盘提醒";

    System.Windows.Forms.NotifyIcon? tray;
    Mutex? singleton;
    DispatcherTimer? clock;
    bool sdkNotifications;
    DateTime minute = DateTime.MinValue;
    string day = "";
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, a) =>
        {
            Log(a.Exception);
            MessageBox.Show(a.Exception.Message + "\n操作未完成，请检查后重试。", "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            a.Handled = true;
        };
        try
        {
            Smoke = e.Args.Contains("--smoke");
            if (Smoke)
                NativeTestInput.Release();
            NotificationCheck = e.Args.Contains("--check-notifications");
            int i = Array.IndexOf(e.Args, "--data-dir");
            string? explicitDir = i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : null;
            if ((Smoke || e.Args.Contains("--demo")) && string.IsNullOrWhiteSpace(explicitDir))
                throw new InvalidDataException("测试和演示模式必须明确指定独立的数据目录。");
            var config = Path.Combine(AppContext.BaseDirectory, "data-location.txt");
            DataDir = explicitDir ?? (File.Exists(config) ? File.ReadAllText(config).Trim() : "");
            if (string.IsNullOrWhiteSpace(DataDir))
            {
                string suggested = Path.Combine(Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))?.FullName ?? AppContext.BaseDirectory, "DesktopTodoData");
                using var picker = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择日程数据文件夹（需在程序文件夹之外，建议选择空间充足的磁盘）",
                    UseDescriptionForTitle = true,
                    SelectedPath = suggested
                };
                if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    Shutdown();
                    return;
                }

                DataDir = picker.SelectedPath;
                try
                {
                    File.WriteAllText(config, DataDir);
                }
                catch
                {
                }
            }

            DataDir = Path.GetFullPath(DataDir);
            if ((Path.TrimEndingDirectorySeparator(DataDir) + Path.DirectorySeparatorChar).StartsWith(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("数据目录必须在程序安装目录之外。请修改 data-location.txt 中的数据位置。");
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDir.ToLowerInvariant())))[..20];
            singleton = new Mutex(true, "Local\\DesktopTodo-" + key, out bool created);
            if (!created)
            {
                MessageBox.Show("程序已经运行，请通过系统托盘图标打开。", "日程管理");
                Shutdown();
                return;
            }

            Store = new Store(DataDir);
            viewSaveTimer.Tick += (_, _) =>
            {
                try
                {
                    FlushViews();
                }
                catch (Exception ex)
                {
                    Log(ex);
                }
            };
            ThemeResources.Apply(Store.Current.Settings);
            Store.Changed += () => ThemeResources.Apply(Appearance);
            if (e.Args.Contains("--demo") && Store.Current.Events.Count == 0 && Store.Current.Tasks.Count == 0)
                Store.Save(Demo.Create(DateTime.Now), false);
            if (Smoke)
                Store.Update(s =>
                {
                    s.Settings.DesktopMode = false;
                    s.Settings.Notifications = NotificationCheck;
                    s.Settings.UseSystemNotifications = e.Args.Contains("--system-notifications");
                }, false);
            SetupTray();
            Planner = new PlannerWindow(false);
            MainWindow = Planner;
            Planner.Show();
            if (Store.Current.Settings.Detached)
                SetDetached(true, false);
            ConfigureNotifications();
            clock = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            clock.Tick += (_, _) => Tick();
            clock.Start();
            SystemEvents.PowerModeChanged += PowerChanged;
            Tick();
            int exitIndex = Array.IndexOf(e.Args, "--exit-check");
            if (Smoke && exitIndex >= 0 && exitIndex + 1 < e.Args.Length)
                Dispatcher.BeginInvoke(async () => await ExitChecks.Run(this, e.Args[exitIndex + 1]), DispatcherPriority.ApplicationIdle);
            else if (Smoke)
                Dispatcher.BeginInvoke(async () => await SmokeRunner.Run(this), DispatcherPriority.ApplicationIdle);
        }
        catch (Exception ex)
        {
            Log(ex);
            MessageBox.Show("启动失败：\n" + ex.Message + "\n\n数据目录：" + DataDir, "日程管理", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Dispatcher.BeginInvoke(Tick);
    }

    void SetupTray()
    {
        tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "日程管理 · 周计划与时间轴",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowPanels);
        tray.BalloonTipClicked += (_, _) => Dispatcher.Invoke(ShowPanels);
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示日程", null, (_, _) => Dispatcher.Invoke(() => Native.Activate(Planner)));
        menu.Items.Add("显示时间轴", null, (_, _) => Dispatcher.Invoke(() =>
        {
            if (TaskWindow != null)
                Native.Activate(TaskWindow);
            else
            {
                Planner.ShowTimeline();
                Native.Activate(Planner);
            }
        }));
        menu.Items.Add("置顶 / 回到桌面", null, (_, _) => Dispatcher.Invoke(ToggleTopmost));
        menu.Items.Add("设置与备份", null, (_, _) => Dispatcher.Invoke(() => Dialogs.Settings(Planner)));
        menu.Items.Add("退出程序", null, (_, _) => Dispatcher.Invoke(ExitApp));
        tray.ContextMenuStrip = menu;
    }

    internal void ShowPanels()
    {
        Native.Activate(Planner);
        if (TaskWindow != null)
            Native.Activate(TaskWindow);
    }

    internal void ToggleTopmost()
    {
        Store.Update(s => s.Settings.Topmost = !s.Settings.Topmost);
        ApplyWindows();
    }

    internal void ApplyWindows()
    {
        Planner?.ApplyWindowMode();
        TaskWindow?.ApplyWindowMode();
    }

    internal void SetDetached(bool detached, bool save = true)
    {
        FlushViews();
        if (save)
            Store.Update(s => s.Settings.Detached = detached, false);
        if (detached && TaskWindow == null)
        {
            TaskWindow = new PlannerWindow(true);
            TaskWindow.CopyTimelineFrom(Planner);
            TaskWindow.Show();
        }

        if (!detached && TaskWindow != null)
        {
            var old = TaskWindow;
            Planner.CopyTimelineFrom(old);
            TaskWindow = null;
            old.AllowClose = true;
            old.Close();
        }

        Planner.Render();
    }

    void NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) => Dispatcher.BeginInvoke(ShowPanels);
    internal void ConfigureNotifications()
    {
        bool want = Store.Current.Settings.Notifications && Store.Current.Settings.UseSystemNotifications && (!Smoke || NotificationCheck);
        if (!want)
        {
            if (sdkNotifications)
                StopSystemNotifications();
            NotificationStatus = Store.Current.Settings.Notifications ? "托盘提醒" : "提醒已关闭";
            return;
        }

        if (sdkNotifications)
            return;
        try
        {
            AppNotificationManager.Default.NotificationInvoked += NotificationInvoked;
            AppNotificationManager.Default.Register("日程管理", new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "app.png")));
            sdkNotifications = true;
            NotificationStatus = "系统通知";
        }
        catch (Exception ex)
        {
            AppNotificationManager.Default.NotificationInvoked -= NotificationInvoked;
            NotificationStatus = "托盘提醒（系统通知不可用）";
            Log(ex);
        }
    }

    void StopSystemNotifications()
    {
        try
        {
            AppNotificationManager.Default.RemoveAllAsync().AsTask().Wait(1000);
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception ex)
        {
            Log(ex);
        }
        finally
        {
            AppNotificationManager.Default.NotificationInvoked -= NotificationInvoked;
            sdkNotifications = false;
        }
    }

    internal void Notify(string title, string body)
    {
        if (Smoke && !NotificationCheck)
            return;
        if (sdkNotifications)
        {
            try
            {
                string Escape(string v) => SecurityElement.Escape(v) ?? "";
                var xml = "<toast><visual><binding template='ToastGeneric'><text>" + Escape(title) + "</text><text>" + Escape(body) + "</text></binding></visual></toast>";
                var notification = new AppNotification(xml);
                AppNotificationManager.Default.Show(notification);
                LastNotificationId = notification.Id;
                return;
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        tray?.ShowBalloonTip(6000, title, body, System.Windows.Forms.ToolTipIcon.Info);
    }

    void Tick()
    {
        if (Exiting || Store == null)
            return;
        try
        {
            var now = DateTime.Now;
            if ((now - minute).TotalSeconds < 55 && day == now.ToString("yyyyMMdd"))
                return;
            minute = now;
            var due = Store.Current.Settings.Notifications ? ReminderLogic.Due(Store.Current, Store.Current.LastReminderCheck, now) : [];
            if (due.Count > 0)
            {
                if (due.Count > 3 || due.Any(x => (now - x.At).TotalMinutes > 5))
                    Notify("有 " + due.Count + " 项日程或截止提醒", string.Join("\n", due.Take(4).Select(x => x.Title + " · " + x.Detail)) + (due.Count > 4 ? "\n请打开面板查看其余事项。" : ""));
                else
                    foreach (var r in due)
                        Notify(r.Title, r.Detail);
            }

            Store.Update(s =>
            {
                s.LastReminderCheck = now;
                s.DeliveredReminders.AddRange(due.Select(x => x.Key));
                if (s.DeliveredReminders.Count > 5000)
                    s.DeliveredReminders = s.DeliveredReminders.TakeLast(5000).ToList();
            }, false);
            if (day != now.ToString("yyyyMMdd"))
            {
                day = now.ToString("yyyyMMdd");
                Store.DailyBackup();
            }

            Planner.RefreshClock();
            TaskWindow?.RefreshClock();
        }
        catch (Exception ex)
        {
            Log(ex);
        }
    }

    internal void Log(Exception ex)
    {
        try
        {
            if (DataDir != "")
            {
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(Path.Combine(DataDir, "app.log"), DateTime.Now.ToString("s") + " " + ex + "\n");
            }
        }
        catch
        {
        }
    }

    internal void ExitApp()
    {
        if (Exiting)
            return;
        Exiting = true;
        clock?.Stop();
        try
        {
            FlushViews();
        }
        catch (Exception ex)
        {
            Log(ex);
        }

        Planner?.SaveBounds();
        TaskWindow?.SaveBounds();
        if (sdkNotifications)
            StopSystemNotifications();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Exiting = true;
        if (Smoke)
            NativeTestInput.Release();
        viewSaveTimer.Stop();
        SystemEvents.PowerModeChanged -= PowerChanged;
        clock?.Stop();
        if (tray != null)
        {
            tray.Visible = false;
            tray.Dispose();
        }

        Store?.Dispose();
        singleton?.Dispose();
        base.OnExit(e);
    }
}
