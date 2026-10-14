using System.Text.Json;
namespace DesktopTodo.Core;

public sealed class CalendarEvent {
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Title { get; set; } = "";
 public DateTime Start { get; set; }
 public DateTime End { get; set; }
 public string Location { get; set; } = "";
 public string Notes { get; set; } = "";
 public string Color { get; set; } = "";
 public bool Weekly { get; set; }
 public DateTime? RepeatUntil { get; set; }
 public int ReminderMinutes { get; set; } = 10;
 public List<DateTime> Exceptions { get; set; } = [];
}
public sealed class Milestone {
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Title { get; set; } = "";
 public DateTime Date { get; set; }
 public string Notes { get; set; } = "";
 public bool Completed { get; set; }
 public bool Reminder { get; set; } = true;
 public int ReminderHour { get; set; } = 9;
 public int ReminderMinute { get; set; }
 public int ReminderDaysBefore { get; set; }
}
public sealed class TodoTask {
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Title { get; set; } = "";
 public DateTime Start { get; set; } = DateTime.Today;
 public DateTime End { get; set; } = DateTime.Today.AddDays(7);
 public string Notes { get; set; } = "";
 public string Color { get; set; } = "";
 public bool Completed { get; set; }
 public bool Reminder { get; set; } = true;
 public int ReminderHour { get; set; } = 9;
 public int ReminderMinute { get; set; }
 public int ReminderDaysBefore { get; set; }
 public List<Milestone> Milestones { get; set; } = [];
}
public sealed class Preferences {
 public string Theme { get; set; } = "雪景紫";
 public string Background { get; set; } = "";
 public string Foreground { get; set; } = "";
 public string Accent { get; set; } = "";
 public double Opacity { get; set; } = .95;
 public bool Topmost { get; set; }
 public bool Detached { get; set; }
 public bool DesktopMode { get; set; } = true;

 public bool Notifications { get; set; } = true;
 public bool UseSystemNotifications { get; set; } // Opt in: registration may prompt security software.
 public int TimelineDays { get; set; } = 30;
 public double TimelineBarHeight { get; set; } = 11;
 public double TimelineRowHeight { get; set; } = 36;
 public double WeekZoom { get; set; } = 1;
 public double MonthZoom { get; set; } = 1;
 public double Left { get; set; } = 80;
 public double Top { get; set; } = 60;
 public double Width { get; set; } = 1050;
 public double Height { get; set; } = 820;
 public double TaskLeft { get; set; } = 180;
 public double TaskTop { get; set; } = 140;
 public double TaskWidth { get; set; } = 1000;
 public double TaskHeight { get; set; } = 430;
}
public sealed class AppState {
 public int Version { get; set; } = 1;
 public List<CalendarEvent> Events { get; set; } = [];
 public List<TodoTask> Tasks { get; set; } = [];
 public Preferences Settings { get; set; } = new();
 public DateTime LastReminderCheck { get; set; } = DateTime.Now;
 public List<string> DeliveredReminders { get; set; } = [];
}
public record Occurrence(CalendarEvent Source, DateTime Start, DateTime End) {
 public string Key => Source.Id + ":" + Start.Ticks;
}
public record Reminder(string Key, string Title, string Detail, DateTime At);
public static class Copies {
 public static readonly JsonSerializerOptions Options = new() { WriteIndented=true };
 public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
}
public record Palette(string Name, string Background, string Ink, string Accent, string[] Colors) {
 public static readonly Palette[] All = [
  new("雪景紫","#DDD9E3","#4C435A","#7F6890",["#B7ABD4","#ACBCE9","#9688B3","#F2EDD4"]),
  new("海岸蓝","#E9E7E0","#31446C","#2F509B",["#9DBFEB","#CDC8E6","#70A0DF","#4574C2"]),
  new("秋日橙","#FFF2E3","#672F16","#A14713",["#F9C79A","#FACE7A","#FC8C3A","#FDA937"]),
  new("林间绿","#F9F7EE","#314C35","#356B43",["#A6DC93","#74B16C","#F6F1AB","#BDDD43"])
 ];
 public static Palette For(Preferences p) {
  var a = All.FirstOrDefault(x=>x.Name==p.Theme) ?? All[0];
  return a with { Background=string.IsNullOrWhiteSpace(p.Background)?a.Background:p.Background, Ink=string.IsNullOrWhiteSpace(p.Foreground)?a.Ink:p.Foreground, Accent=string.IsNullOrWhiteSpace(p.Accent)?a.Accent:p.Accent };
 }
}
