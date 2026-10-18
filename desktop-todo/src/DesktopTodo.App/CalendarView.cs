using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace DesktopTodo.App;

internal record CalendarDrag(Occurrence Occurrence);
internal partial class PlannerWindow
{
    const double HourHeight = 52;
    double CalendarWidth => Math.Max(480, frame.ActualWidth - 58);

    FrameworkElement BuildWeek()
    {
        double width = CalendarWidth, timeWidth = 56, dayWidth = (width - timeWidth) / 7;
        var week = CalendarLogic.WeekStart(SelectedDate);
        var root = new Grid();
        root.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        root.RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        var heading = new Canvas
        {
            Height = 48,
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        At(heading, Text(week.ToString("M月"), 12, true), 0, 15);
        for (int d = 0; d < 7; d++)
        {
            var date = week.AddDays(d);
            var label = new StackPanel
            {
                Width = dayWidth - 4,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var name = Text(new[] { "一", "二", "三", "四", "五", "六", "日" }[d], 11);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            label.Children.Add(name);
            var number = Text(date.ToString("dd"), 18, true, date == DateTime.Today ? Palette.Accent : null);
            number.HorizontalAlignment = HorizontalAlignment.Center;
            label.Children.Add(number);
            var border = new Border
            {
                Width = dayWidth - 4,
                Height = 43,
                Background = date == DateTime.Today ? Brush(Palette.Accent, .12) : Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Child = label
            };
            At(heading, border, timeWidth + d * dayWidth, 0);
        }

        var headingHost = new Canvas
        {
            Height = 48 * CalendarZoom,
            ClipToBounds = true
        };
        heading.LayoutTransform = new ScaleTransform(CalendarZoom, CalendarZoom);
        At(headingHost, heading, -weekScrollX, 0);
        root.Children.Add(headingHost);
        var canvas = new Canvas
        {
            Width = width,
            Height = 24 * HourHeight,
            Background = Brush("#FFFFFF", .32),
            AllowDrop = true
        };
        for (int h = 0; h <= 24; h++)
        {
            double y = h * HourHeight;
            var line = new Line
            {
                X1 = timeWidth,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = Brush(Palette.Ink, .10),
                IsHitTestVisible = false
            };
            canvas.Children.Add(line);
            if (h < 24)
            {
                var t = Text(h.ToString("00") + ":00", 10);
                t.IsHitTestVisible = false;
                At(canvas, t, 0, y + 3);
            }
        }

        for (int d = 0; d < 8; d++)
            canvas.Children.Add(new Line { X1 = timeWidth + d * dayWidth, Y1 = 0, X2 = timeWidth + d * dayWidth, Y2 = canvas.Height, Stroke = Brush(Palette.Ink, .08), IsHitTestVisible = false });
        var events = CalendarLogic.Occurrences(Store.Current, week, week.AddDays(7));
        for (int d = 0; d < 7; d++)
        {
            var date = week.AddDays(d);
            var items = events.Where(o => o.Start < date.AddDays(1) && o.End > date).ToList();
            // Connected overlap groups share columns; unrelated events retain full width.
            int groupStart = 0;
            while (groupStart < items.Count)
            {
                int groupEnd = groupStart + 1;
                DateTime latest = items[groupStart].End;
                while (groupEnd < items.Count && items[groupEnd].Start < latest)
                {
                    if (items[groupEnd].End > latest)
                        latest = items[groupEnd].End;
                    groupEnd++;
                }

                var group = items.GetRange(groupStart, groupEnd - groupStart);
                var columns = CalendarLogic.LayoutColumns(group);
                int count = columns.Length == 0 ? 1 : columns.Max() + 1;
                for (int k = 0; k < group.Count; k++)
                {
                    var o = group[k];
                    var from = o.Start < date ? date : o.Start;
                    var to = o.End > date.AddDays(1) ? date.AddDays(1) : o.End;
                    var y = (from - date).TotalHours * HourHeight;
                    var height = Math.Max(20, (to - from).TotalHours * HourHeight - 3);
                    var color = string.IsNullOrEmpty(o.Source.Color) ? Palette.Colors[Math.Abs((int)(uint)o.Source.Id.GetHashCode() % Palette.Colors.Length)] : o.Source.Color;
                    double cardWidth = Math.Max(1, (dayWidth - 6) / count - 2);
                    var stack = new StackPanel
                    {
                        Margin = new Thickness(7, 5, 5, 3)
                    };
                    var title = Text(o.Source.Title, cardWidth < 90 ? 11 : 13, true, InkFor(color));
                    title.TextWrapping = TextWrapping.Wrap;
                    title.MaxHeight = Math.Max(18, height - 25);
                    stack.Children.Add(title);
                    if (height > 46)
                        stack.Children.Add(Text(o.Start.ToString("HH:mm") + "–" + o.End.ToString("HH:mm"), 10, false, InkFor(color)));
                    if (height > 75 && o.Source.Location != "")
                        stack.Children.Add(Text(o.Source.Location, 10, false, InkFor(color)));
                    var eventGrid = new Grid();
                    eventGrid.Children.Add(stack);
                    var card = new Border
                    {
                        Width = cardWidth,
                        Height = height,
                        CornerRadius = new CornerRadius(7),
                        Background = Brush(color),
                        Child = eventGrid,
                        Cursor = Cursors.Hand,
                        ClipToBounds = true,
                        ToolTip = o.Source.Title + "\n" + o.Start.ToString("MM/dd HH:mm") + " — " + o.End.ToString("MM/dd HH:mm") + "\n" + o.Source.Location + "\n" + o.Source.Notes + "\n点击编辑 · 拖动移动 · 底边调整时长"
                    };
                    System.Windows.Automation.AutomationProperties.SetName(card, "日程：" + o.Source.Title);
                    Point? down = null;
                    bool dragged = false;
                    card.MouseLeftButtonDown += (_, e) =>
                    {
                        down = e.GetPosition(card);
                        dragged = false;
                        e.Handled = true;
                    };
                    card.MouseMove += (_, e) =>
                    {
                        if (down is Point p && e.LeftButton == MouseButtonState.Pressed && (e.GetPosition(card) - p).Length > 5)
                        {
                            dragged = true;
                            down = null;
                            DragDrop.DoDragDrop(card, new CalendarDrag(o), DragDropEffects.Move);
                        }
                    };
                    card.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        if (!dragged && down != null)
                        {
                            down = null;
                            EditEvent(o);
                        }
                    };
                    if (o.End.Date == date || o.End == date.AddDays(1))
                    {
                        var thumb = new Thumb
                        {
                            Height = 7,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Cursor = Cursors.SizeNS,
                            Opacity = .25,
                            Background = Brush(InkFor(color))
                        };
                        double delta = 0, dragY = 0;
                        thumb.DragStarted += (_, _) =>
                        {
                            delta = 0;
                            dragY = Mouse.GetPosition(canvas).Y;
                        };
                        thumb.DragDelta += (_, e) => delta = Mouse.GetPosition(canvas).Y - dragY;
                        thumb.DragCompleted += (_, _) => Run(() =>
                        {
                            var minutes = Math.Round(delta / HourHeight * 60 / 15) * 15;
                            if (minutes != 0)
                                MoveOccurrence(o, o.Start, o.End.AddMinutes(minutes));
                        });
                        eventGrid.Children.Add(thumb);
                    }

                    At(canvas, card, timeWidth + d * dayWidth + 3 + columns[k] * (dayWidth - 6) / count, y + 2);
                }

                groupStart = groupEnd;
            }
        }

        var today = DateTime.Today;
        if (today >= week && today < week.AddDays(7))
        {
            double x = timeWidth + (today - week).Days * dayWidth, y = DateTime.Now.TimeOfDay.TotalHours * HourHeight;
            canvas.Children.Add(new Line { X1 = x, Y1 = y, X2 = x + dayWidth, Y2 = y, Stroke = Brush(Palette.Accent), StrokeThickness = 2, IsHitTestVisible = false });
            At(canvas, new Ellipse { Width = 6, Height = 6, Fill = Brush(Palette.Accent), IsHitTestVisible = false }, x - 3, y - 3);
        }

        DateTime DateAt(Point p)
        {
            int d = Math.Clamp((int)((p.X - timeWidth) / dayWidth), 0, 6);
            double minutes = Math.Clamp(Math.Floor(p.Y / HourHeight * 4) * 15, 0, 1425);
            return week.AddDays(d).AddMinutes(minutes);
        }

        Point? selectStart = null;
        canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (e.GetPosition(canvas).X < timeWidth)
                return;
            selectStart = e.GetPosition(canvas);
            canvas.CaptureMouse();
            e.Handled = true;
        };
        canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (selectStart is not Point p)
                return;
            canvas.ReleaseMouseCapture();
            selectStart = null;
            var first = DateAt(p);
            var last = DateAt(e.GetPosition(canvas));
            if (last < first)
                (first, last) = (last, first);
            EditEvent(null, first, last == first ? first.AddHours(1) : last.AddMinutes(15));
            e.Handled = true;
        };
        canvas.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(typeof(CalendarDrag)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        canvas.Drop += (_, e) => Run(() =>
        {
            if (e.Data.GetData(typeof(CalendarDrag)) is CalendarDrag drag)
                MoveOccurrence(drag.Occurrence, DateAt(e.GetPosition(canvas)));
            e.Handled = true;
        });
        var scroll = CalendarScroll(canvas, false, x => Canvas.SetLeft(heading, -x));
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    FrameworkElement BuildMonth()
    {
        var first = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        var start = CalendarLogic.WeekStart(first);
        int rows = (int)Math.Ceiling(((first - start).Days + DateTime.DaysInMonth(first.Year, first.Month)) / 7.0);
        var grid = new Grid
        {
            Width = CalendarWidth,
            Height = Math.Max(rows * 72 + 28, ActualHeight - (Store.Current.Settings.Detached ? 155 : 420))
        };
        for (int d = 0; d < 7; d++)
            grid.ColumnDefinitions.Add(new());
        grid.RowDefinitions.Add(new()
        {
            Height = new GridLength(28)
        });
        for (int r = 0; r < rows; r++)
            grid.RowDefinitions.Add(new()
            {
                Height = new GridLength(1, GridUnitType.Star),
                MinHeight = 65
            });
        for (int d = 0; d < 7; d++)
        {
            var text = Text(new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" }[d], 11, true);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(text, d);
            grid.Children.Add(text);
        }

        var all = CalendarLogic.Occurrences(Store.Current, start, start.AddDays(rows * 7));
        for (int i = 0; i < rows * 7; i++)
        {
            var date = start.AddDays(i);
            var cell = new StackPanel
            {
                Margin = new Thickness(6)
            };
            var num = Text(date.Day.ToString(), 13, date == DateTime.Today);
            cell.Children.Add(num);
            var list = all.Where(o => o.Start < date.AddDays(1) && o.End > date).ToList();
            int visible = Math.Max(1, (int)((ActualHeight * (Store.Current.Settings.Detached ? .70 : .42) / rows - 30) / 22));
            visible = Math.Min(visible, 3);
            foreach (var o in list.Take(visible))
            {
                var color = o.Source.Color == "" ? Palette.Colors[0] : o.Source.Color;
                var text = Text(o.Start.ToString("HH:mm") + " " + o.Source.Title, 10, false, InkFor(color));
                cell.Children.Add(new Border { Background = Brush(color), CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 3, 0, 0), Child = text });
            }

            if (list.Count > visible)
                cell.Children.Add(Text("另有 " + (list.Count - visible) + " 项", 10));
            var border = new Border
            {
                Background = Brush(date == DateTime.Today ? Palette.Accent : "#FFFFFF", date == DateTime.Today ? .13 : date.Month == first.Month ? .35 : .12),
                BorderBrush = Brush(Palette.Ink, .10),
                BorderThickness = new Thickness(.5),
                Child = cell,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };
            border.MouseLeftButtonUp += (_, _) =>
            {
                SelectedDate = date;
                followToday = false;
                MonthView = false;
                Render();
            };
            border.ToolTip = date.ToString("yyyy年M月d日") + " · " + list.Count + " 项日程\n点击查看这一周";
            Grid.SetColumn(border, i % 7);
            Grid.SetRow(border, i / 7 + 1);
            grid.Children.Add(border);
        }

        return CalendarScroll(grid, true);
    }

    internal void ResetCalendarZoom()
    {
        bool month = MonthView;
        double old = CalendarZoom;
        if (month)
        {
            monthScrollX /= old;
            monthScrollY /= old;
        }
        else
        {
            weekScrollX /= old;
            weekScrollY /= old;
        }

        App.Instance.ChangeView(p =>
        {
            if (month)
                p.MonthZoom = 1;
            else
                p.WeekZoom = 1;
        });
    }

    internal void ZoomCalendarAt(bool month, int direction, Point pointer, ScrollViewer scroll)
    {
        if (NodeDragging || Mouse.LeftButton == MouseButtonState.Pressed)
            return;
        double old = month ? App.Instance.Appearance.MonthZoom : App.Instance.Appearance.WeekZoom;
        double next = Math.Clamp(Math.Round(old + direction * .1, 2), .5, 2);
        if (next == old)
            return;
        double x = ViewMath.ZoomOffset(scroll.HorizontalOffset, pointer.X, old, next), y = ViewMath.ZoomOffset(scroll.VerticalOffset, pointer.Y, old, next);
        if (month)
        {
            monthScrollX = x;
            monthScrollY = y;
        }
        else
        {
            weekScrollX = x;
            weekScrollY = y;
        }

        App.Instance.ChangeView(p =>
        {
            if (month)
                p.MonthZoom = next;
            else
                p.WeekZoom = next;
        });
    }

    ScrollViewer CalendarScroll(FrameworkElement surface, bool month, Action<double>? horizontal = null)
    {
        double zoom = month ? App.Instance.Appearance.MonthZoom : App.Instance.Appearance.WeekZoom;
        surface.LayoutTransform = new ScaleTransform(zoom, zoom);
        surface.HorizontalAlignment = HorizontalAlignment.Left;
        surface.VerticalAlignment = VerticalAlignment.Top;
        var scroll = new ScrollViewer
        {
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = surface,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false,
            ToolTip = "Ctrl＋滚轮缩放 · 普通滚轮上下浏览"
        };
        System.Windows.Automation.AutomationProperties.SetName(scroll, month ? "月日程内容" : "周日程内容");
        bool ready = false;
        double restoreX = month ? monthScrollX : weekScrollX, restoreY = month ? monthScrollY : weekScrollY;
        scroll.Loaded += (_, _) =>
        {
            scroll.ScrollToHorizontalOffset(restoreX);
            scroll.ScrollToVerticalOffset(restoreY);
            Dispatcher.BeginInvoke(() =>
            {
                if (!scroll.IsLoaded || !IsAncestorOf(scroll))
                    return;
                scroll.UpdateLayout();
                scroll.ScrollToHorizontalOffset(restoreX);
                scroll.ScrollToVerticalOffset(restoreY);
                scroll.UpdateLayout();
                ready = true;
                horizontal?.Invoke(scroll.HorizontalOffset);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        scroll.ScrollChanged += (_, _) =>
        {
            if (!ready || !scroll.IsLoaded || !IsAncestorOf(scroll))
                return;
            if (month)
            {
                monthScrollX = scroll.HorizontalOffset;
                monthScrollY = scroll.VerticalOffset;
            }
            else
            {
                weekScrollX = scroll.HorizontalOffset;
                weekScrollY = scroll.VerticalOffset;
            }

            horizontal?.Invoke(scroll.HorizontalOffset);
        };
        scroll.PreviewMouseWheel += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;
            e.Handled = true;
            ZoomCalendarAt(month, Math.Sign(e.Delta), e.GetPosition(scroll), scroll);
        };
        return scroll;
    }
}
