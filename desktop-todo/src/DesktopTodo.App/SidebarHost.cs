using System.Windows.Automation;
using System.Windows.Media.Effects;

namespace DesktopTodo.App;

internal partial class PlannerWindow
{
    internal Border MainFrame => frame;
    internal Border SidebarHandle => App.Instance.Sidebar!.Docked.Handle;
    readonly Grid shell = new();
    readonly Border sidebarPreview = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right, Width = 220 };
    void InitializeSidebarHost()
    {
        shell.Children.Add(frame);
        sidebarPreview.Child = new TextBlock { Text = "松开以合并清单", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        shell.Children.Add(sidebarPreview);
        Panel.SetZIndex(sidebarPreview, 4);
        Content = shell;
    }
    internal void HighlightSidebar(bool active) => sidebarPreview.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    void RefreshSidebarChrome() => sidebarPreview.Background = Brush(Palette.Accent, .35);
}

internal sealed class AttachedSidebarWindow : Window
{
    readonly SidebarController controller;
    readonly Grid root = new();
    readonly Border slot = new() { ClipToBounds = true, Padding = new Thickness(6, 0, 0, 0), BorderThickness = new Thickness(0, 1, 0, 1), CornerRadius = new CornerRadius(10) };
    readonly Border rail = new() { Width = 14, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(0, 12, 12, 0), IsHitTestVisible = false };
    internal Border Handle { get; } = new() { Width = 30, Height = 104, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeWE, Background = Brushes.Transparent, Focusable = true };
    readonly Border face = new() { Width = 30, Height = 88, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
    bool right = true;
    internal AttachedSidebarWindow(SidebarController controller)
    {
        this.controller = controller;
        Title = "日程管理 · 附属清单";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        FontSize = 13;
        root.ColumnDefinitions.Add(new());
        root.ColumnDefinitions.Add(new());
        root.Children.Add(slot);
        root.Children.Add(rail);
        Handle.Child = face;
        root.Children.Add(Handle);
        Panel.SetZIndex(Handle, 3);
        Content = root;
        AutomationProperties.SetName(Handle, "展开或收起待办与笔记");
        Handle.ToolTip = "点击展开／收起，横向拖动调整清单宽度";
        Handle.MouseLeftButtonDown += (_, e) => SidebarView.Run(() =>
        {
            controller.HandleStart(Native.CursorPosition());
            if (controller.IsHandleDragging)
            {
                Handle.Focus();
                Handle.CaptureMouse();
            }
            RefreshAppearance();
            e.Handled = true;
        });
        Handle.MouseMove += (_, e) => { if (Handle.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) controller.HandleMove(Native.CursorPosition()); };
        Handle.MouseLeftButtonUp += (_, e) => SidebarView.Run(() => { controller.HandleEnd(); Handle.ReleaseMouseCapture(); RefreshAppearance(); e.Handled = true; });
        Handle.LostMouseCapture += (_, _) => SidebarView.Run(() => { controller.HandleEnd(true); RefreshAppearance(); });
        Handle.MouseEnter += (_, _) => RefreshAppearance();
        Handle.MouseLeave += (_, _) => RefreshAppearance();
        Handle.GotKeyboardFocus += (_, _) => RefreshAppearance();
        Handle.LostKeyboardFocus += (_, _) => RefreshAppearance();
        Handle.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space && !Handle.IsMouseCaptured)
            {
                SidebarView.Run(controller.Toggle);
                e.Handled = true;
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Handle.IsMouseCaptured)
            {
                controller.HandleEnd(true);
                Handle.ReleaseMouseCapture();
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
        Closing += (_, e) =>
        {
            if (!App.Instance.Exiting)
            {
                e.Cancel = true;
                SidebarView.Run(() => { controller.View.Flush(true); controller.SetWidth(0); });
            }
        };
        RefreshAppearance();
    }
    internal void SetView(SidebarView? view) => slot.Child = view;
    internal void Place(double width, bool onRight)
    {
        right = onRight;
        var host = controller.Host;
        var work = SidebarController.WorkArea(host);
        double total = width + 30;
        Width = total;
        Height = Math.Max(150, host.ActualHeight - 32);
        Top = host.Top + 16;
        double left = right ? host.Left + host.ActualWidth - 6 : host.Left - width - 24;
        Left = width > 0 ? left : Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - total));
        root.ColumnDefinitions[right ? 0 : 1].Width = new GridLength(width);
        root.ColumnDefinitions[right ? 1 : 0].Width = new GridLength(30);
        Grid.SetColumn(slot, right ? 0 : 1);
        Grid.SetColumn(rail, right ? 1 : 0);
        Grid.SetColumn(Handle, right ? 1 : 0);
        slot.Visibility = width > 0 ? Visibility.Visible : Visibility.Collapsed;
        rail.CornerRadius = right ? new CornerRadius(0, 12, 12, 0) : new CornerRadius(12, 0, 0, 12);
        rail.HorizontalAlignment = right ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        if (!IsVisible)
            Show();
        RefreshAppearance();
    }
    static Color Mix(Color a, Color b, double n) => Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * n), (byte)Math.Round(a.G + (b.G - a.G) * n), (byte)Math.Round(a.B + (b.B - a.B) * n));
    internal void RefreshAppearance()
    {
        var preferences = App.Instance.Appearance;
        var palette = Palette.For(preferences);
        var accent = (Color)ColorConverter.ConvertFromString(palette.Accent);
        var paper = (Color)ColorConverter.ConvertFromString(palette.Background);
        var color = Mix(accent, paper, .6);
        if (Handle.IsMouseOver)
            color = Mix(color, Colors.White, .08);
        rail.Background = PlannerWindow.Brush(palette.Accent, preferences.Opacity);
        slot.BorderBrush = PlannerWindow.Brush(palette.Accent, .35 * preferences.Opacity);
        face.Background = new LinearGradientBrush(Mix(color, Colors.White, .12), Mix(color, Colors.Black, .04), 90);
        face.BorderBrush = PlannerWindow.Brush(Handle.IsKeyboardFocused ? palette.Ink : palette.Accent, Handle.IsKeyboardFocused ? .8 : .3);
        face.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = Handle.IsMouseCaptured ? 2 : 5, ShadowDepth = Handle.IsMouseCaptured ? 0 : 1.5, Direction = 270, Opacity = .16 * preferences.Opacity };
    }
}
