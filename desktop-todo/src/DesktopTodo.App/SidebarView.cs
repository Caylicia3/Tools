using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;

namespace DesktopTodo.App;

internal sealed class SidebarView : Grid
{
    readonly Store store;
    readonly List<Panel> chrome = [];
    readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    readonly Dictionary<string, RichTextBox> editors = [];
    readonly Dictionary<string, Border> rows = [];
    readonly HashSet<string> dirty = [];
    readonly HashSet<RichTextBox> compositions = [];
    readonly StackPanel list = new();
    readonly Grid regions = new();
    readonly ScrollViewer listScroll = Scroller(), noteScroll = Scroller();
    readonly Border listPaper = new(), notePaper = new();
    readonly TextBlock status = new() { FontSize = 11, Margin = new Thickness(6, 2, 6, 2), TextWrapping = TextWrapping.Wrap };
    readonly CheckBox completed = new() { Content = "已完成", VerticalAlignment = VerticalAlignment.Center };
    readonly Button listPercent, notePercent, docking, undo, redo;
    internal RichTextBox NoteEditor
    {
        get;
    }
    internal RichTextBox? ActiveEditor
    {
        get; private set;
    }
    RichTextBox? listEditor;
    bool synchronizing, committing, previewing;
    string group = Guid.NewGuid().ToString("N");
    string? listPreview, notePreview;
    internal bool HasPending => dirty.Count > 0;
    internal bool IsComposing => compositions.Count > 0;
    internal IReadOnlyDictionary<string, RichTextBox> Editors => editors;

    internal SidebarView(Store store)
    {
        this.store = store;
        ClipToBounds = true;
        RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var top = new WrapPanel { Margin = new Thickness(4) };
        undo = Small("↶", store.Undo, "撤销清单或其他内容");
        redo = Small("↷", store.Redo, "重做");
        docking = Small("独立", () => App.Instance.Sidebar!.ChooseDocking(), "独立显示或合并到日程、时间轴");
        top.Children.Add(undo);
        top.Children.Add(redo);
        top.Children.Add(docking);
        top.Children.Add(new TextBlock { Text = "待办与笔记", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5) });
        Children.Add(top);
        chrome.Add(top);
        regions.RowDefinitions.Add(new()
        {
            Height = new GridLength(.5, GridUnitType.Star),
            MinHeight = 92
        });
        regions.RowDefinitions.Add(new()
        {
            Height = new GridLength(6)
        });
        regions.RowDefinitions.Add(new()
        {
            Height = new GridLength(.5, GridUnitType.Star),
            MinHeight = 92
        });
        Grid.SetRow(regions, 1);
        Children.Add(regions);
        var listArea = Area();
        var listTools = new WrapPanel();
        listTools.Children.Add(Small("＋ 待办", AddItem));
        listTools.Children.Add(completed);
        listTools.Children.Add(Small("字号", () => Format(false, false)));
        listTools.Children.Add(Small("字色", () => Format(false, true)));
        listTools.Children.Add(Small("背景", () => BackgroundColor(false)));
        listPercent = Small("100%", () => Zoom(false, 1));
        listTools.Children.Add(listPercent);
        listArea.Children.Add(listTools);
        chrome.Add(listTools);
        completed.Checked += (_, _) => SetCompletedFilter(true);
        completed.Unchecked += (_, _) => SetCompletedFilter(false);
        listScroll.Content = list;
        listPaper.Child = listScroll;
        Grid.SetRow(listPaper, 1);
        listArea.Children.Add(listPaper);
        regions.Children.Add(listArea);
        var split = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext, ShowsPreview = false, Cursor = Cursors.SizeNS };
        System.Windows.Automation.AutomationProperties.SetName(split, "调整待办与笔记高度");
        Grid.SetRow(split, 1);
        regions.Children.Add(split);
        split.DragCompleted += (_, _) => Run(() =>
        {
            double total = regions.RowDefinitions[0].ActualHeight + regions.RowDefinitions[2].ActualHeight;
            if (total > 0)
                store.Update(s => s.Sidebar.Settings.Split = Math.Clamp(regions.RowDefinitions[0].ActualHeight / total, .15, .85), false);
            ApplySplit();
        });
        var noteArea = Area();
        Grid.SetRow(noteArea, 2);
        regions.Children.Add(noteArea);
        var noteTools = new WrapPanel();
        noteTools.Children.Add(new TextBlock { Text = "自由笔记", Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center });
        noteTools.Children.Add(Small("字号", () => Format(true, false)));
        noteTools.Children.Add(Small("字色", () => Format(true, true)));
        noteTools.Children.Add(Small("背景", () => BackgroundColor(true)));
        notePercent = Small("100%", () => Zoom(true, 1));
        noteTools.Children.Add(notePercent);
        noteArea.Children.Add(noteTools);
        chrome.Add(noteTools);
        NoteEditor = CreateEditor("note", "自由笔记正文");
        NoteEditor.MinHeight = 150;
        noteScroll.Content = NoteEditor;
        notePaper.Child = noteScroll;
        Grid.SetRow(notePaper, 1);
        noteArea.Children.Add(notePaper);
        Grid.SetRow(status, 2);
        Children.Add(status);
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            try
            {
                Flush();
            }
            catch (Exception ex) { status.Text = "尚未保存：" + ex.Message; App.Instance.Log(ex); }
        };
        listScroll.PreviewMouseWheel += (_, e) => Wheel(false, e);
        noteScroll.PreviewMouseWheel += (_, e) => Wheel(true, e);
        SizeChanged += (_, _) => ResizePaper();
        store.BeforeContentChange += BeforeChange;
        store.Changed += Refresh;
        ApplySplit();
        Refresh();
    }
    static Grid Area()
    {
        var grid = new Grid { Margin = new Thickness(4, 0, 4, 0), ClipToBounds = true };
        grid.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        grid.RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        return grid;
    }
    static ScrollViewer Scroller() => new() { CanContentScroll = false, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Visible, Padding = new Thickness(2), Focusable = false };
    internal static Button Small(string label, Action action, string? tip = null)
    {
        var b = new Button { Content = label, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(2), Focusable = false, ToolTip = tip ?? label };
        System.Windows.Automation.AutomationProperties.SetName(b, tip ?? label);
        b.Click += (_, _) => Run(action);
        return b;
    }
    internal static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) { App.Instance.Log(ex); MessageBox.Show(ex.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    void BeforeChange()
    {
        if (!committing)
            Flush(true);
    }
    void ApplySplit()
    {
        double ratio = store.Current.Sidebar.Settings.Split;
        regions.RowDefinitions[0].Height = new GridLength(ratio, GridUnitType.Star);
        regions.RowDefinitions[2].Height = new GridLength(1 - ratio, GridUnitType.Star);
    }
    internal void Flush(bool finish = false)
    {
        if (committing || previewing)
            return;
        saveTimer.Stop();
        if (IsComposing)
            throw new InvalidDataException("请先确认或取消输入法中正在输入的文字。");
        if (dirty.Count > 0)
        {
            var values = dirty.ToDictionary(key => key, key => RichEditing.Read(editors[key]));
            committing = true;
            try
            {
                store.Edit("编辑待办与笔记", s =>
                {
                    foreach (var pair in values)
                        if (pair.Key == "note")
                            s.Sidebar.Content.Note = pair.Value;
                        else
                        {
                            var parts = pair.Key.Split(':');
                            var item = s.Sidebar.Content.Items.Single(x => x.Id == parts[0]);
                            if (parts[1] == "title")
                                item.Title = pair.Value;
                            else
                                item.Notes = pair.Value;
                        }
                }, group);
                dirty.Clear();
                status.Text = "已保存到本机";
            }
            finally { committing = false; }
            UpdateHistoryButtons();
        }
        if (finish)
            group = Guid.NewGuid().ToString("N");
    }
    RichTextBox CreateEditor(string key, string name)
    {
        var box = RichEditing.Editor(name);
        editors.Add(key, box);
        box.TextChanged += (_, _) =>
        {
            if (synchronizing || previewing)
                return;
            synchronizing = true;
            try
            {
                foreach (var paragraph in box.Document.Blocks.OfType<Paragraph>())
                {
                    if (paragraph.Margin != new Thickness(0))
                        paragraph.Margin = new Thickness(0);
                }
            }
            finally { synchronizing = false; }
            dirty.Add(key);
            status.Text = "正在编辑…";
            saveTimer.Stop();
            if (!IsComposing)
                saveTimer.Start();
        };
        box.GotKeyboardFocus += (_, _) =>
        {
            if (ActiveEditor != box)
            {
                Run(() => Flush(true));
                ActiveEditor = box;
                if (key != "note")
                    listEditor = box;
            }
        };
        box.LostKeyboardFocus += (_, _) => { if (!previewing && !synchronizing) Run(() => Flush(true)); };
        TextCompositionManager.AddPreviewTextInputStartHandler(box, (_, _) => { compositions.Add(box); saveTimer.Stop(); });
        TextCompositionManager.AddPreviewTextInputUpdateHandler(box, (_, e) => { if (e.TextComposition.CompositionText.Length > 0) compositions.Add(box); });
        box.PreviewTextInput += (_, _) => { compositions.Remove(box); if (dirty.Count > 0) { saveTimer.Stop(); saveTimer.Start(); } };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !previewing && !IsComposing)
            {
                dirty.Remove(key);
                synchronizing = true;
                try
                {
                    RichEditing.Write(box, Value(key));
                }
                finally { synchronizing = false; }
                status.Text = "已恢复上次保存的文字";
                e.Handled = true;
            }
        };
        return box;
    }
    RichText Value(string key)
    {
        if (key == "note")
            return store.Current.Sidebar.Content.Note;
        var parts = key.Split(':');
        var item = store.Current.Sidebar.Content.Items.Single(x => x.Id == parts[0]);
        return parts[1] == "title" ? item.Title : item.Notes;
    }
    internal void Refresh()
    {
        RefreshAppearance();
        if (committing || previewing || HasPending || IsComposing)
            return;
        synchronizing = true;
        try
        {
            var data = store.Current.Sidebar.Content;
            foreach (string id in rows.Keys.Where(id => data.Items.All(x => x.Id != id)).ToList())
            {
                list.Children.Remove(rows[id]);
                rows.Remove(id);
                editors.Remove(id + ":title");
                editors.Remove(id + ":notes");
            }
            foreach (var item in data.Items)
            {
                if (!rows.ContainsKey(item.Id))
                    rows.Add(item.Id, BuildRow(item));
                var row = rows[item.Id];
                row.Visibility = item.Completed && !store.Current.Sidebar.Settings.ShowCompleted ? Visibility.Collapsed : Visibility.Visible;
                var check = (CheckBox)((DockPanel)row.Child).Children[0];
                check.IsChecked = item.Completed;
                check.ToolTip = item.Completed ? "已完成，点击恢复为未完成" : "标记完成";
            }
            for (int i = 0; i < data.Items.Count; i++)
            {
                var row = rows[data.Items[i].Id];
                if (list.Children.IndexOf(row) != i)
                {
                    list.Children.Remove(row);
                    list.Children.Insert(i, row);
                }
            }
            foreach (var pair in editors)
            {
                var value = Value(pair.Key);
                if (RichEditing.Fingerprint(RichEditing.Read(pair.Value)) != RichEditing.Fingerprint(value))
                    RichEditing.Write(pair.Value, value);
            }
            completed.IsChecked = store.Current.Sidebar.Settings.ShowCompleted;
            ApplySplit();
        }
        finally { synchronizing = false; }
    }
    Border BuildRow(ChecklistItem item)
    {
        var border = new Border { Background = Brushes.Transparent, Padding = new Thickness(2, 5, 2, 5), BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = PlannerWindow.Brush("#888888", .2), AllowDrop = true };
        var row = new DockPanel();
        border.Child = row;
        var check = new CheckBox { Margin = new Thickness(2, 8, 3, 0), VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(check, Dock.Left);
        row.Children.Add(check);
        check.Click += (_, _) => { if (!synchronizing) Run(() => store.Edit("切换待办完成状态", s => s.Sidebar.Content.Items.Single(x => x.Id == item.Id).Completed = check.IsChecked == true)); };
        var controls = new StackPanel { Orientation = Orientation.Vertical };
        DockPanel.SetDock(controls, Dock.Right);
        row.Children.Add(controls);
        controls.Children.Add(Small("×", () => store.Edit("删除待办", s => s.Sidebar.Content.Items.RemoveAll(x => x.Id == item.Id)), "删除待办，可撤销"));
        var grip = new TextBlock { Text = "↕", Background = Brushes.Transparent, Padding = new Thickness(5), Cursor = Cursors.SizeNS, ToolTip = "拖动调整待办顺序" };
        controls.Children.Add(grip);
        Point? origin = null;
        grip.MouseLeftButtonDown += (_, e) => { origin = e.GetPosition(grip); grip.CaptureMouse(); e.Handled = true; };
        grip.MouseLeftButtonUp += (_, _) => { origin = null; grip.ReleaseMouseCapture(); };
        grip.MouseMove += (_, e) =>
        {
            if (origin is not Point start || e.LeftButton != MouseButtonState.Pressed || (e.GetPosition(grip) - start).Length < 5)
                return;
            origin = null;
            grip.ReleaseMouseCapture();
            Run(() => { Flush(true); try { DragDrop.DoDragDrop(grip, new DataObject("DesktopTodo.Checklist", item.Id), DragDropEffects.Move); } finally { foreach (var b in rows.Values) ResetRowBorder(b); } });
        };
        border.PreviewDragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent("DesktopTodo.Checklist"))
                return;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            border.BorderBrush = PlannerWindow.Brush(Palette.For(App.Instance.Appearance).Accent);
            bool before = e.GetPosition(border).Y < border.ActualHeight / 2;
            border.BorderThickness = before ? new Thickness(0, 3, 0, 0) : new Thickness(0, 0, 0, 3);
            border.Padding = before ? new Thickness(2, 2, 2, 6) : new Thickness(2, 5, 2, 3);
        };
        border.DragLeave += (_, _) => ResetRowBorder(border);
        border.PreviewDrop += (_, e) =>
        {
            if (e.Data.GetData("DesktopTodo.Checklist") is not string source)
                return;
            bool after = e.GetPosition(border).Y >= border.ActualHeight / 2;
            e.Handled = true;
            ResetRowBorder(border);
            Run(() => MoveItem(source, item.Id, after));
        };
        var fields = new StackPanel();
        row.Children.Add(fields);
        fields.Children.Add(CreateEditor(item.Id + ":title", "待办标题：" + item.Id));
        var notes = new Expander { Header = "备注", FontSize = 11, Foreground = PlannerWindow.Brush("#555555"), IsExpanded = item.Notes.PlainText.Length > 0 };
        notes.Content = CreateEditor(item.Id + ":notes", "待办备注：" + item.Id);
        fields.Children.Add(notes);
        return border;
    }
    static void ResetRowBorder(Border border)
    {
        border.Padding = new Thickness(2, 5, 2, 5);
        border.BorderBrush = PlannerWindow.Brush("#888888", .2);
        border.BorderThickness = new Thickness(0, 0, 0, 1);
    }
    internal void MoveItem(string source, string target, bool after)
    {
        if (source == target)
            return;
        store.Edit("调整待办顺序", s =>
        {
            var items = s.Sidebar.Content.Items;
            var item = items.Single(x => x.Id == source);
            items.Remove(item);
            int index = items.FindIndex(x => x.Id == target);
            if (index < 0)
                throw new InvalidDataException("目标待办已不存在。");
            items.Insert(index + (after ? 1 : 0), item);
        });
    }
    internal void AddItem()
    {
        Flush(true);
        var item = new ChecklistItem { Title = RichText.From("新待办") };
        store.Edit("新增待办", s => s.Sidebar.Content.Items.Add(item));
        var box = editors[item.Id + ":title"];
        box.Focus();
        box.SelectAll();
        box.BringIntoView();
    }
    void SetCompletedFilter(bool value)
    {
        if (!synchronizing)
            Run(() => store.Update(s => s.Sidebar.Settings.ShowCompleted = value));
    }
    void UpdateHistoryButtons()
    {
        undo.IsEnabled = store.CanUndo || HasPending;
        redo.IsEnabled = store.CanRedo;
    }
    internal void RefreshAppearance()
    {
        var p = App.Instance.Appearance;
        var palette = Palette.For(p);
        Background = Brushes.Transparent;
        foreach (var panel in chrome)
            panel.Background = PlannerWindow.Brush(palette.Background, p.Opacity);
        status.Background = PlannerWindow.Brush(palette.Background, p.Opacity);
        TextElement.SetForeground(this, PlannerWindow.Brush(palette.Ink));
        listPaper.Background = PlannerWindow.Brush(listPreview ?? store.Current.Sidebar.Settings.ListBackground, p.Opacity);
        notePaper.Background = PlannerWindow.Brush(notePreview ?? store.Current.Sidebar.Settings.NoteBackground, p.Opacity);
        listPercent.Content = Math.Round(store.Current.Sidebar.Settings.ListZoom * 100) + "%";
        notePercent.Content = Math.Round(store.Current.Sidebar.Settings.NoteZoom * 100) + "%";
        docking.Content = App.Instance.Sidebar?.IsFloating == true ? "合并" : "独立";
        list.LayoutTransform = new ScaleTransform(store.Current.Sidebar.Settings.ListZoom, store.Current.Sidebar.Settings.ListZoom);
        NoteEditor.LayoutTransform = new ScaleTransform(store.Current.Sidebar.Settings.NoteZoom, store.Current.Sidebar.Settings.NoteZoom);
        foreach (var pair in editors)
            pair.Value.CaretBrush = PlannerWindow.Brush(PlannerWindow.InkFor(pair.Key == "note" ? notePreview ?? store.Current.Sidebar.Settings.NoteBackground : listPreview ?? store.Current.Sidebar.Settings.ListBackground));
        UpdateHistoryButtons();
        ResizePaper();
    }
    void ResizePaper()
    {
        double width = Math.Max(180, ActualWidth - 42);
        list.Width = width;
        NoteEditor.Width = width;
    }
    void Wheel(bool note, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;
        e.Handled = true;
        if (Mouse.LeftButton == MouseButtonState.Pressed || previewing)
            return;
        var scroller = note ? noteScroll : listScroll;
        double previous = note ? store.Current.Sidebar.Settings.NoteZoom : store.Current.Sidebar.Settings.ListZoom;
        Zoom(note, Math.Clamp(Math.Round(previous + Math.Sign(e.Delta) * .1, 1), .5, 2), e.GetPosition(scroller));
    }
    internal void Zoom(bool note, double value, Point? anchor = null)
    {
        var scroll = note ? noteScroll : listScroll;
        double previous = note ? store.Current.Sidebar.Settings.NoteZoom : store.Current.Sidebar.Settings.ListZoom;
        var point = anchor ?? new Point(0, 0);
        double x = (scroll.HorizontalOffset + point.X) * value / previous - point.X;
        double y = (scroll.VerticalOffset + point.Y) * value / previous - point.Y;
        store.Update(s => { if (note) s.Sidebar.Settings.NoteZoom = value; else s.Sidebar.Settings.ListZoom = value; }, false);
        RefreshAppearance();
        UpdateLayout();
        scroll.ScrollToHorizontalOffset(Math.Max(0, x));
        scroll.ScrollToVerticalOffset(Math.Max(0, y));
    }
    void BackgroundColor(bool note)
    {
        Flush(true);
        string initial = note ? store.Current.Sidebar.Settings.NoteBackground : store.Current.Sidebar.Settings.ListBackground;
        void Preview(string color)
        {
            if (note)
                notePreview = color;
            else
                listPreview = color;
            RefreshAppearance();
        }
        try
        {
            if (RichEditing.ChooseColor(Window.GetWindow(this), initial, Preview, out string color))
                store.Update(s => { if (note) s.Sidebar.Settings.NoteBackground = color; else s.Sidebar.Settings.ListBackground = color; }, false);
        }
        finally { listPreview = notePreview = null; RefreshAppearance(); }
    }
    void Format(bool note, bool color)
    {
        var box = note ? NoteEditor : listEditor;
        if (box == null || !editors.ContainsValue(box))
        {
            status.Text = "请先点击需要设置格式的待办文字。";
            return;
        }
        Flush(true);
        var before = RichEditing.Read(box);
        int start = box.Document.ContentStart.GetOffsetToPosition(box.Selection.Start);
        int end = box.Document.ContentStart.GetOffsetToPosition(box.Selection.End);
        var initialSize = box.Selection.GetPropertyValue(TextElement.FontSizeProperty);
        var initialColor = box.Selection.GetPropertyValue(TextElement.ForegroundProperty);
        bool empty = box.Selection.IsEmpty;
        previewing = true;
        bool accepted = false;
        try
        {
            if (color)
            {
                var brush = box.Selection.GetPropertyValue(TextElement.ForegroundProperty) as SolidColorBrush;
                string initial = brush == null ? "#333333" : $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                accepted = RichEditing.ChooseColor(Window.GetWindow(this), initial, c => box.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, PlannerWindow.Brush(c)), out _);
            }
            else
            {
                var size = box.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                accepted = RichEditing.ChooseSize(Window.GetWindow(this), size is double d ? d * .75 : 11, n => box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, n * 4d / 3));
            }
        }
        finally
        {
            if (!accepted)
            {
                if (RichEditing.Fingerprint(RichEditing.Read(box)) != RichEditing.Fingerprint(before))
                {
                    RichEditing.Write(box, before);
                    var a = box.Document.ContentStart.GetPositionAtOffset(start) ?? box.Document.ContentEnd;
                    var b = box.Document.ContentStart.GetPositionAtOffset(end) ?? box.Document.ContentEnd;
                    box.Selection.Select(a, b);
                }
                if (empty)
                {
                    if (initialSize is double)
                        box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, initialSize);
                    if (initialColor is Brush)
                        box.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, initialColor);
                }
            }
            previewing = false;
        }
        if (accepted)
        {
            dirty.Add(editors.Single(x => x.Value == box).Key);
            Flush(true);
        }
        box.Focus();
    }
    internal void Dispose()
    {
        saveTimer.Stop();
        store.BeforeContentChange -= BeforeChange;
        store.Changed -= Refresh;
    }
}
