using System.Windows.Interop;

namespace DesktopTodo.App;

internal sealed class SidebarController
{
    readonly App app;
    internal SidebarView View
    {
        get;
    }
    internal PlannerWindow Host
    {
        get; private set;
    }
    internal SidebarWindow? Floating
    {
        get; private set;
    }
    internal AttachedSidebarWindow Docked
    {
        get;
    }
    internal bool IsFloating => Floating != null;
    internal bool Expanded
    {
        get; private set;
    }
    internal double OpenWidth
    {
        get; private set;
    }
    internal bool IsHandleDragging => handleStart != null;
    internal bool OnRight { get; private set; } = true;
    PlannerWindow? dropTarget;
    Point? handleStart;
    double handleWidth;
    bool handleMoved, syncing;

    internal SidebarController(App app)
    {
        this.app = app;
        Host = app.Planner;
        View = new SidebarView(app.Store);
        Docked = new AttachedSidebarWindow(this);
        WatchHost(true);
        Docked.Owner = Host;
        Docked.SetView(View);
        SyncHost();
        app.Store.Changed += RefreshAppearance;
    }
    void WatchHost(bool add)
    {
        if (add)
        {
            Host.LocationChanged += HostChanged;
            Host.SizeChanged += HostChanged;
            Host.StateChanged += HostChanged;
            Host.IsVisibleChanged += HostVisibilityChanged;
        }
        else
        {
            Host.LocationChanged -= HostChanged;
            Host.SizeChanged -= HostChanged;
            Host.StateChanged -= HostChanged;
            Host.IsVisibleChanged -= HostVisibilityChanged;
        }
    }
    void HostChanged(object? sender, EventArgs e) => SyncHost();
    void HostVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => SyncHost();
    internal static Rect WorkArea(Window window)
    {
        var bounds = Native.Bounds(window);
        var work = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Rect(window.Left + (work.Left - bounds.X) / dpi.DpiScaleX, window.Top + (work.Top - bounds.Y) / dpi.DpiScaleY, work.Width / dpi.DpiScaleX, work.Height / dpi.DpiScaleY);
    }
    double Available(bool chooseSide)
    {
        var work = WorkArea(Host);
        double right = Math.Max(0, work.Right - Host.Left - Host.ActualWidth - 24);
        double left = Math.Max(0, Host.Left - work.Left - 24);
        if (chooseSide)
            OnRight = right >= 240 || left < 240;
        return Math.Min(640, OnRight ? right : left);
    }
    internal void SyncHost()
    {
        if (syncing || Floating != null)
            return;
        syncing = true;
        try
        {
            if (!Host.IsVisible || Host.WindowState == WindowState.Minimized || app.Exiting)
            {
                Docked.Hide();
                return;
            }
            double available = Available(!IsHandleDragging);
            if (Expanded && available < 240)
            {
                Expanded = false;
                OpenWidth = 0;
            }
            else if (Expanded)
                OpenWidth = Math.Min(OpenWidth, available);
            Docked.Topmost = Host.Topmost;
            Docked.Place(OpenWidth, OnRight);
        }
        finally { syncing = false; }
    }
    internal void RefreshAppearance()
    {
        Docked.RefreshAppearance();
        Floating?.RefreshAppearance();
    }
    void ExplainNoRoom() => MessageBox.Show(Host, "主窗口两侧没有足够空间显示清单。请先还原或移动主窗口，为侧边留出至少 264 个逻辑像素，再展开清单。", "请为清单留出空间", MessageBoxButton.OK, MessageBoxImage.Information);
    internal void Show()
    {
        if (Floating != null)
        {
            Native.Activate(Floating);
            return;
        }
        Native.Activate(Host);
        if (!Expanded && !SetWidth(app.Store.Current.Sidebar.Settings.Width))
            ExplainNoRoom();
    }
    internal void Toggle()
    {
        View.Flush(true);
        if (!SetWidth(Expanded ? 0 : app.Store.Current.Sidebar.Settings.Width))
            ExplainNoRoom();
    }
    internal Rect SavedBounds(PlannerWindow window) => new(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
    internal bool SetWidth(double width, bool persist = true)
    {
        if (Floating != null)
            return false;
        width = Math.Clamp(width, 0, 640);
        if (width > 0)
        {
            double available = Available(!IsHandleDragging);
            if (available < 240)
                return false;
            width = Math.Min(width, available);
        }
        Expanded = width > 0;
        OpenWidth = width;
        Docked.Place(width, OnRight);
        if (persist && width >= 240)
            app.Store.Update(s => s.Sidebar.Settings.Width = width, false);
        return true;
    }
    internal void HandleStart(Point screen)
    {
        View.Flush(true);
        if (!Expanded && Available(true) < 240)
        {
            ExplainNoRoom();
            return;
        }
        handleStart = screen;
        handleWidth = OpenWidth;
        handleMoved = false;
    }
    internal void HandleMove(Point screen)
    {
        if (handleStart is not Point start)
            return;
        double delta = (screen.X - start.X) / VisualTreeHelper.GetDpi(Host).DpiScaleX * (OnRight ? 1 : -1);
        if (Math.Abs(delta) < 4 && !handleMoved)
            return;
        handleMoved = true;
        SetWidth(Math.Clamp(handleWidth + delta, 0, 640), false);
    }
    internal void HandleEnd(bool cancel = false)
    {
        if (handleStart == null)
            return;
        if (cancel)
            SetWidth(handleWidth, false);
        else if (!handleMoved)
            Toggle();
        else
            SetWidth(OpenWidth < 120 ? 0 : Math.Clamp(OpenWidth, 240, 640));
        handleStart = null;
    }
    internal void ChooseDocking()
    {
        if (Floating == null)
        {
            Detach();
            return;
        }
        var menu = new ContextMenu();
        var calendar = new MenuItem { Header = "合并到日程" };
        calendar.Click += (_, _) => SidebarView.Run(() => Dock(app.Planner));
        menu.Items.Add(calendar);
        var timeline = new MenuItem { Header = "合并到时间轴", IsEnabled = app.TaskWindow != null };
        timeline.Click += (_, _) => SidebarView.Run(() => Dock(app.TaskWindow!));
        menu.Items.Add(timeline);
        menu.PlacementTarget = View;
        menu.IsOpen = true;
    }
    internal void Detach()
    {
        View.Flush(true);
        if (Floating != null)
        {
            Native.Activate(Floating);
            return;
        }
        SetWidth(0);
        Docked.Hide();
        Docked.SetView(null);
        Floating = new SidebarWindow(this);
        Floating.Show();
        View.RefreshAppearance();
    }
    internal void Dock(PlannerWindow target)
    {
        View.Flush(true);
        ClearDrop();
        if (Floating != null)
        {
            Floating.SaveBounds();
            Floating.ReleaseView();
            var old = Floating;
            Floating = null;
            old.AllowClose = true;
            old.Close();
        }
        Docked.Hide();
        WatchHost(false);
        Host = target;
        WatchHost(true);
        Docked.Owner = Host;
        Docked.SetView(View);
        Expanded = false;
        OpenWidth = 0;
        SyncHost();
        if (!SetWidth(app.Store.Current.Sidebar.Settings.Width))
            ExplainNoRoom();
        Native.Activate(Host);
        View.RefreshAppearance();
    }
    internal void BeforeMerge(PlannerWindow timeline)
    {
        if (Floating == null && Host == timeline)
        {
            bool open = Expanded;
            Dock(app.Planner);
            if (!open)
                SetWidth(0);
        }
    }
    internal void HideFloating()
    {
        View.Flush(true);
        Floating?.SaveBounds();
        Floating?.Hide();
        ClearDrop();
    }
    internal void PreviewDrop(Point screen)
    {
        ClearDrop();
        foreach (var target in new[] { app.Planner, app.TaskWindow }.Where(w => w != null).Cast<PlannerWindow>())
        {
            if (!target.IsVisible || target.WindowState == WindowState.Minimized)
                continue;
            var r = Native.Bounds(target);
            double band = 28 * VisualTreeHelper.GetDpi(target).DpiScaleX;
            if (Math.Abs(screen.X - (r.X + r.Width)) <= band && screen.Y >= r.Y && screen.Y <= r.Y + r.Height)
            {
                dropTarget = target;
                target.HighlightSidebar(true);
                break;
            }
        }
    }
    internal void FinishDrop(bool cancel)
    {
        var target = dropTarget;
        ClearDrop();
        if (!cancel && target != null)
            Dock(target);
    }
    void ClearDrop()
    {
        app.Planner.HighlightSidebar(false);
        app.TaskWindow?.HighlightSidebar(false);
        dropTarget = null;
    }
    internal void ApplyWindowMode()
    {
        Floating?.ApplyWindowMode();
        SyncHost();
    }
    internal void Dispose()
    {
        WatchHost(false);
        app.Store.Changed -= RefreshAppearance;
        View.Dispose();
    }
}

internal sealed class SidebarWindow : Window
{
    readonly SidebarController controller;
    readonly Grid slot = new();
    readonly Border frame = new();
    Point? drag, original;
    internal bool AllowClose;
    internal SidebarWindow(SidebarController controller)
    {
        this.controller = controller;
        Title = "日程管理 · 待办与笔记";
        Icon = App.Instance.Planner.Icon;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        FontSize = 13;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = true;
        MinWidth = 240;
        MinHeight = 290;
        var p = App.Instance.Store.Current.Sidebar.Settings;
        Width = p.FloatingWidth;
        Height = p.FloatingHeight;
        Left = p.Left;
        Top = p.Top;
        Native.PlaceOnScreen(this);
        var root = new DockPanel();
        frame.Child = root;
        frame.CornerRadius = new CornerRadius(12);
        frame.BorderThickness = new Thickness(1);
        Content = frame;
        var header = new DockPanel { Margin = new Thickness(5), Background = Brushes.Transparent };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(SidebarView.Small("—", () => WindowState = WindowState.Minimized));
        actions.Children.Add(SidebarView.Small("□", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
        actions.Children.Add(SidebarView.Small("×", controller.HideFloating, "隐藏清单，日程仍继续运行"));
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var grip = new TextBlock { Text = "待办与笔记", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(5), Cursor = Cursors.SizeAll, Background = Brushes.Transparent };
        header.Children.Add(grip);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        slot.Children.Add(controller.View);
        root.Children.Add(slot);
        grip.MouseLeftButtonDown += (_, e) =>
        {
            if (WindowState != WindowState.Normal)
                return;
            SidebarView.Run(() => controller.View.Flush(true));
            var b = Native.Bounds(this);
            original = new Point(b.X, b.Y);
            drag = PointToScreen(e.GetPosition(this));
            grip.CaptureMouse();
            e.Handled = true;
        };
        grip.MouseMove += (_, e) =>
        {
            if (drag is not Point last || e.LeftButton != MouseButtonState.Pressed)
                return;
            var next = PointToScreen(e.GetPosition(this));
            Native.Move(this, next.X - last.X, next.Y - last.Y);
            drag = next;
            controller.PreviewDrop(next);
        };
        grip.MouseLeftButtonUp += (_, _) => { drag = null; grip.ReleaseMouseCapture(); SidebarView.Run(() => controller.FinishDrop(false)); SaveBounds(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && drag != null)
            {
                if (original is Point o)
                {
                    var b = Native.Bounds(this);
                    Native.Move(this, o.X - b.X, o.Y - b.Y);
                }
                drag = null;
                grip.ReleaseMouseCapture();
                controller.FinishDrop(true);
                e.Handled = true;
            }
        };
        KeyDown += (_, e) =>
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;
            if (e.Key == Key.Z)
            {
                SidebarView.Run((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? App.Instance.Store.Redo : App.Instance.Store.Undo);
                e.Handled = true;
            }
            if (e.Key == Key.Y)
            {
                SidebarView.Run(App.Instance.Store.Redo);
                e.Handled = true;
            }
        };
        SourceInitialized += (_, _) => Native.ConstrainMaximize(this);
        Loaded += (_, _) => { ApplyWindowMode(); RefreshAppearance(); };
        Closing += (_, e) =>
        {
            if (!AllowClose && !App.Instance.Exiting)
            {
                e.Cancel = true;
                SidebarView.Run(controller.HideFloating);
            }
        };
        SizeChanged += (_, _) => { if (IsLoaded) SaveBounds(); };
        App.Instance.Store.Changed += RefreshAppearance;
        Closed += (_, _) => App.Instance.Store.Changed -= RefreshAppearance;
    }
    internal void ReleaseView() => slot.Children.Clear();
    internal void RefreshAppearance()
    {
        var p = Palette.For(App.Instance.Appearance);
        frame.BorderBrush = PlannerWindow.Brush(p.Ink, .3);
        Foreground = PlannerWindow.Brush(p.Ink);
        ((DockPanel)frame.Child).Children.OfType<DockPanel>().First().Background = PlannerWindow.Brush(p.Background, App.Instance.Appearance.Opacity);
    }
    internal void ApplyWindowMode()
    {
        var p = App.Instance.Store.Current.Settings;
        if (p.Topmost || !p.DesktopMode)
        {
            Native.Detach(this);
            Topmost = p.Topmost;
        }
        else
        {
            Topmost = false;
            Native.Attach(this);
        }
    }
    internal void SaveBounds()
    {
        if (!IsLoaded || WindowState != WindowState.Normal || AllowClose)
            return;
        App.Instance.Store.Update(s => { var p = s.Sidebar.Settings; p.Left = Left; p.Top = Top; p.FloatingWidth = Math.Max(240, ActualWidth); p.FloatingHeight = Math.Max(290, ActualHeight); }, false);
    }
}
