namespace DesktopTodo.Core;

public static class CalendarLogic
{
    public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static IEnumerable<Occurrence> Expand(CalendarEvent e, DateTime from, DateTime to)
    {
        if (to <= from || e.End <= e.Start)
            yield break;
        var duration = e.End - e.Start;
        if (!e.Weekly)
        {
            if (e.Start < to && e.End > from)
                yield return new(e, e.Start, e.End);
            yield break;
        }

        var skip = Math.Max(0, (int)Math.Floor((from - e.Start - duration).TotalDays / 7));
        var start = e.Start.AddDays(skip * 7);
        var limit = (e.RepeatUntil?.Date ?? DateTime.MaxValue.Date);
        for (; start < to && start.Date <= limit; start = start.AddDays(7))
        {
            if (start + duration > from && !e.Exceptions.Any(d => d.Date == start.Date))
                yield return new(e, start, start + duration);
            if (start > DateTime.MaxValue.AddDays(-7))
                break;
        }
    }

    public static List<Occurrence> Occurrences(AppState state, DateTime from, DateTime to) => state.Events.SelectMany(e => Expand(e, from, to)).OrderBy(x => x.Start).ThenBy(x => x.End).ToList();
    public static void EditOccurrence(AppState state, Occurrence occurrence, CalendarEvent edited, bool future)
    {
        var original = state.Events.Single(e => e.Id == occurrence.Source.Id);
        if (!original.Weekly)
        {
            edited.Id = original.Id;
            state.Events[state.Events.IndexOf(original)] = edited;
            return;
        }

        if (future)
        {
            var futureExceptions = original.Exceptions.Where(x => x.Date >= occurrence.Start.Date).ToList();
            if (occurrence.Start == original.Start)
                state.Events.Remove(original);
            else
            {
                original.RepeatUntil = occurrence.Start.Date.AddDays(-1);
                original.Exceptions.RemoveAll(x => x.Date >= occurrence.Start.Date);
            }

            edited.Id = Guid.NewGuid().ToString("N");
            edited.Exceptions = [];
            // Preserve exclusions when the future series keeps the same weekday.
            if (edited.Weekly && edited.Start.DayOfWeek == occurrence.Start.DayOfWeek)
                edited.Exceptions = futureExceptions;
            state.Events.Add(edited);
        }
        else
        {
            if (!original.Exceptions.Contains(occurrence.Start.Date))
                original.Exceptions.Add(occurrence.Start.Date);
            edited.Id = Guid.NewGuid().ToString("N");
            edited.Weekly = false;
            edited.RepeatUntil = null;
            edited.Exceptions = [];
            state.Events.Add(edited);
        }
    }

    public static void DeleteOccurrence(AppState state, Occurrence occurrence, bool future)
    {
        var original = state.Events.Single(x => x.Id == occurrence.Source.Id);
        if (!original.Weekly || (future && occurrence.Start == original.Start))
            state.Events.Remove(original);
        else if (future)
            original.RepeatUntil = occurrence.Start.Date.AddDays(-1);
        else if (!original.Exceptions.Contains(occurrence.Start.Date))
            original.Exceptions.Add(occurrence.Start.Date);
    }

    public static void ShiftTask(TodoTask task, int days)
    {
        task.Start = task.Start.AddDays(days);
        task.End = task.End.AddDays(days);
        foreach (var m in task.Milestones)
            m.Date = m.Date.AddDays(days);
    }

    public static int[] LayoutColumns(IReadOnlyList<Occurrence> items)
    {
        var ends = new List<DateTime>();
        var result = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            int col = ends.FindIndex(x => x <= items[i].Start);
            if (col < 0)
            {
                col = ends.Count;
                ends.Add(items[i].End);
            }
            else
                ends[col] = items[i].End;
            result[i] = col;
        }

        return result;
    }
}

public static class Validation
{
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    public static void Color(string value) => Check(value == "" || System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$"), "颜色请填写 # 后接 6 位数字或 A～F 字母，例如 #B7ABD4，也可点击选色。");
    public static void State(AppState s)
    {
        Check(s.Version == 1, "备份版本不受支持，请使用对应版本的软件。");
        Check(s.Events != null && s.Tasks != null && s.Settings != null && s.DeliveredReminders != null, "数据文件缺少必要内容。");
        var ids = new HashSet<string>();
        foreach (var e in s.Events!)
        {
            Check(e != null && !string.IsNullOrWhiteSpace(e.Id) && ids.Add(e.Id), "日程标识重复或缺失。");
            Check(!string.IsNullOrWhiteSpace(e!.Title), "请填写日程标题。");
            Check(e.End > e.Start, "日程结束时间必须晚于开始时间。");
            Check(e.Start.Year >= 1900 && e.End.Year <= 9990, "日期超出支持范围。");
            Check(!e.Weekly || e.RepeatUntil == null || e.RepeatUntil.Value.Date >= e.Start.Date, "重复结束日期不能早于开始日期。");
            Check(e.ReminderMinutes >= -1 && e.ReminderMinutes <= 525600, "提醒时间超出范围。");
            Check(e.Exceptions != null, "重复日程例外数据缺失。");
            Color(e.Color);
        }

        foreach (var t in s.Tasks!)
        {
            Check(t != null && !string.IsNullOrWhiteSpace(t.Id) && ids.Add(t.Id), "任务标识重复或缺失。");
            Check(!string.IsNullOrWhiteSpace(t!.Title), "请填写任务名称。");
            Check(t.Start.Date <= t.End.Date, "任务截止日期不能早于开始日期。");
            Check(t.Start.Year >= 1900 && t.End.Year <= 9990, "日期超出支持范围。");
            Color(t.Color);
            Check(t.ReminderHour >= 0 && t.ReminderHour <= 23 && t.ReminderMinute >= 0 && t.ReminderMinute <= 59, "提醒时间必须在 00:00 到 23:59 之间。");
            Check(t.ReminderDaysBefore >= 0 && t.ReminderDaysBefore <= 3650, "提前提醒天数必须在 0 到 3650 之间。");
            Check(t.Milestones != null, "任务节点数据缺失。");
            foreach (var m in t.Milestones!)
            {
                Check(m != null && !string.IsNullOrWhiteSpace(m.Id) && ids.Add(m.Id), "节点标识重复或缺失。");
                Check(!string.IsNullOrWhiteSpace(m!.Title), "请填写节点名称。");
                Check(m.Date.Date >= t.Start.Date && m.Date.Date <= t.End.Date, "节点日期必须处于任务起止日期之间。");
                Check(m.ReminderHour >= 0 && m.ReminderHour <= 23 && m.ReminderMinute >= 0 && m.ReminderMinute <= 59, "节点提醒时间必须在 00:00 到 23:59 之间。");
                Check(m.ReminderDaysBefore >= 0 && m.ReminderDaysBefore <= 3650, "节点提前提醒天数必须在 0 到 3650 之间。");
            }
        }

        var p = s.Settings!;
        Check(p.TimelineDays >= 1 && p.TimelineDays <= 3650, "时间轴天数必须在 1 到 3650 之间。");
        Check(double.IsFinite(p.TimelineBarHeight) && p.TimelineBarHeight >= 6 && p.TimelineBarHeight <= 22, "任务条粗细必须在 6 到 22 之间。");
        Check(double.IsFinite(p.TimelineRowHeight) && p.TimelineRowHeight >= 36 && p.TimelineRowHeight <= 144, "任务行高必须在 36 到 144 之间。");
        Check(new[] { p.WeekZoom, p.MonthZoom }.All(z => double.IsFinite(z) && z >= .5 && z <= 2), "日程缩放必须在 50% 到 200% 之间。");
        Check(double.IsFinite(p.Opacity) && p.Opacity >= .01 && p.Opacity <= 1, "背景不透明度必须在 1% 到 100% 之间。");
        Check(new[] { p.Left, p.Top, p.Width, p.Height, p.TaskLeft, p.TaskTop, p.TaskWidth, p.TaskHeight }.All(double.IsFinite), "窗口位置数据无效。");
        Check(p.Width >= 400 && p.Height >= 300 && p.TaskWidth >= 400 && p.TaskHeight >= 250, "窗口尺寸过小。");
        Color(p.Background);
        Color(p.Foreground);
        Color(p.Accent);
    }
}

public static class ReminderLogic
{
    public static DateTime TaskAt(TodoTask t) => t.End.Date.AddDays(-t.ReminderDaysBefore).AddHours(t.ReminderHour).AddMinutes(t.ReminderMinute);
    public static DateTime NodeAt(Milestone m) => m.Date.Date.AddDays(-m.ReminderDaysBefore).AddHours(m.ReminderHour).AddMinutes(m.ReminderMinute);
    public static List<Reminder> Due(AppState state, DateTime from, DateTime now)
    {
        if (now < from)
            return [];
        var result = new List<Reminder>();
        var delivered = state.DeliveredReminders.ToHashSet();
        foreach (var e in state.Events.Where(e => e.ReminderMinutes >= 0))
        {
            foreach (var o in CalendarLogic.Expand(e, from, now.AddMinutes(e.ReminderMinutes).AddSeconds(1)))
            {
                var at = o.Start.AddMinutes(-e.ReminderMinutes);
                var key = "event:" + o.Key + ":" + e.ReminderMinutes;
                if (at > from && at <= now && !delivered.Contains(key))
                    result.Add(new(key, e.Title, o.Start.ToString("MM/dd HH:mm") + (e.Location == "" ? "" : " · " + e.Location), at));
            }
        }

        foreach (var t in state.Tasks.Where(t => !t.Completed))
        {
            var at = TaskAt(t);
            var key = "task:" + t.Id + ":" + at.Ticks;
            if (t.Reminder && at > from && at <= now && !delivered.Contains(key))
                result.Add(new(key, t.Title, "任务截止：" + t.End.ToString("yyyy/MM/dd"), at));
            foreach (var m in t.Milestones.Where(m => !m.Completed && m.Reminder))
            {
                at = NodeAt(m);
                key = "node:" + m.Id + ":" + at.Ticks;
                if (at > from && at <= now && !delivered.Contains(key))
                    result.Add(new(key, m.Title, t.Title + " · 节点日期：" + m.Date.ToString("yyyy/MM/dd"), at));
            }
        }

        return result.OrderBy(x => x.At).ToList();
    }
}

public static class ViewMath
{
    public static DateTime ShiftTimeline(DateTime start, double offsetDays, int days)
    {
        double min = new DateTime(1900, 1, 1).Ticks / (double)TimeSpan.TicksPerDay;
        double max = new DateTime(9990, 1, 1).Ticks / (double)TimeSpan.TicksPerDay - days;
        double serial = start.Date.Ticks / (double)TimeSpan.TicksPerDay;
        return new DateTime((long)(Math.Clamp(serial + offsetDays, min, max) * TimeSpan.TicksPerDay)).Date;
    }

    public static DateTime PanTimeline(DateTime start, int days, double delta, double plot)
    {
        if (!double.IsFinite(delta) || !double.IsFinite(plot) || plot <= 0)
            return start.Date;
        return ShiftTimeline(start, -Math.Round(delta / plot * days, MidpointRounding.AwayFromZero), days);
    }

    public static int ZoomDays(int days, int direction) => direction > 0 ? Math.Max(1, Math.Min(days - 1, (int)Math.Floor(days / 1.2))) : Math.Min(3650, Math.Max(days + 1, (int)Math.Ceiling(days * 1.2)));
    public static DateTime AnchoredStart(DateTime start, int oldDays, int newDays, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, .999999);
        int offset = (int)Math.Floor(fraction * oldDays) - (int)Math.Floor(fraction * newDays);
        var result = start.Date.AddDays(offset);
        return result < DateTime.MinValue.AddYears(1899) ? new DateTime(1900, 1, 1) : result > new DateTime(9990, 1, 1).AddDays(-newDays) ? new DateTime(9990, 1, 1).AddDays(-newDays) : result;
    }

    public static double ZoomOffset(double oldOffset, double pointer, double oldZoom, double newZoom) => Math.Max(0, (oldOffset + pointer) / oldZoom * newZoom - pointer);
}
