using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace DesktopTodo.Core;
public sealed class Store : IDisposable {
 readonly SqliteConnection db;
 sealed record Content(List<CalendarEvent> Events,List<TodoTask> Tasks);
 sealed record Entry(string Label,string Before,string After);
 readonly List<Entry> undo=[],redo=[];
 public string DirectoryPath { get; }
 public AppState Current { get; private set; }
 public event Action? Changed;
 public bool CanUndo=>undo.Count>0;
 public bool CanRedo=>redo.Count>0;
 public int UndoCount=>undo.Count;
 public int RedoCount=>redo.Count;
 public string UndoLabel=>CanUndo?undo[^1].Label:"";
 public string RedoLabel=>CanRedo?redo[^1].Label:"";
 static string Capture(AppState state)=>JsonSerializer.Serialize(new Content(state.Events,state.Tasks),Copies.Options);
 public Store(string directory) {
  DirectoryPath=Path.GetFullPath(directory);Directory.CreateDirectory(DirectoryPath);
  db=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.Combine(DirectoryPath,"desktop-todo.db"),Pooling=false}.ToString());
  db.Open();
  using(var cmd=db.CreateCommand()){cmd.CommandText="PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS app_state (id INTEGER PRIMARY KEY CHECK(id=1), version INTEGER NOT NULL, json TEXT NOT NULL);";cmd.ExecuteNonQuery();}
  using(var cmd=db.CreateCommand()){
   cmd.CommandText="SELECT json FROM app_state WHERE id=1";var json=cmd.ExecuteScalar() as string;
   Current=json==null?new AppState():JsonSerializer.Deserialize<AppState>(json,Copies.Options)??throw new InvalidDataException("数据库内容为空。");
   Validation.State(Current);
  }
  DailyBackup();
 }
 void Commit(AppState state) {
  Validation.State(state);
  using var tx=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=tx;
  cmd.CommandText="INSERT INTO app_state(id,version,json) VALUES(1,$version,$json) ON CONFLICT(id) DO UPDATE SET version=excluded.version,json=excluded.json";
  cmd.Parameters.AddWithValue("$version",state.Version);cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(state,Copies.Options));
  cmd.ExecuteNonQuery();tx.Commit();Current=state;
 }
 public void Update(Action<AppState> edit,bool notify=true){var copy=Copies.Clone(Current);edit(copy);Save(copy,notify);}
 public void Save(AppState state,bool notify=true){
  bool changed=Capture(Current)!=Capture(state);Commit(state);
  if(changed){undo.Clear();redo.Clear();}if(notify)Changed?.Invoke();
 }
 public void Edit(string label,Action<AppState> edit) {
  string before=Capture(Current);var copy=Copies.Clone(Current);edit(copy);Validation.State(copy);string after=Capture(copy);
  if(before==after)return;
  Commit(copy);undo.Add(new(label,before,after));if(undo.Count>100)undo.RemoveAt(0);redo.Clear();Changed?.Invoke();
 }
 public void Undo()=>Travel(undo,redo,false);
 public void Redo()=>Travel(redo,undo,true);
 void Travel(List<Entry> from,List<Entry> to,bool forward) {
  if(from.Count==0)return;
  var entry=from[^1];var content=JsonSerializer.Deserialize<Content>(forward?entry.After:entry.Before,Copies.Options)!;
  var copy=Copies.Clone(Current);copy.Events=content.Events;copy.Tasks=content.Tasks;
  Commit(copy);from.RemoveAt(from.Count-1);to.Add(entry);Changed?.Invoke();
 }
 public void Backup(string path) {
  var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);var temp=full+".writing";
  File.WriteAllText(temp,JsonSerializer.Serialize(Current,Copies.Options),new System.Text.UTF8Encoding(false));File.Move(temp,full,true);
 }
 public void Restore(string path) {
  var restored=JsonSerializer.Deserialize<AppState>(File.ReadAllText(path),Copies.Options)??throw new InvalidDataException("备份为空。");
  Validation.State(restored);Backup(Path.Combine(DirectoryPath,"backups","before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".backup.json"));
  restored.LastReminderCheck=DateTime.Now;restored.DeliveredReminders=[];Commit(restored);undo.Clear();redo.Clear();Changed?.Invoke();
 }
 public void DailyBackup(){var path=Path.Combine(DirectoryPath,"backups",DateTime.Today.ToString("yyyyMMdd")+".backup.json");if(!File.Exists(path))Backup(path);}
 public void Dispose()=>db.Dispose();
}
public static class Demo {
 public static AppState Create(DateTime now) {
  var week=CalendarLogic.WeekStart(now); var s=new AppState();
  s.Events=[
   new(){Title="阅读与笔记",Start=week.AddHours(9),End=week.AddHours(10.5),Color="#B7ABD4",Location="图书馆 · 安静区",Weekly=true,RepeatUntil=week.AddMonths(3)},
   new(){Title="项目讨论",Start=week.AddDays(1).AddHours(10),End=week.AddDays(1).AddHours(11.5),Color="#ACBCE9",Location="线上会议"},
   new(){Title="专注写作",Start=week.AddDays(2).AddHours(9),End=week.AddDays(2).AddHours(11),Color="#9688B3",Notes="完成报告的核心章节"},
   new(){Title="课程学习",Start=week.AddDays(3).AddHours(14),End=week.AddDays(3).AddHours(16),Color="#B7ABD4",Weekly=true},
   new(){Title="整理这一周",Start=week.AddDays(4).AddHours(15),End=week.AddDays(4).AddHours(16),Color="#F2EDD4",Weekly=true},
   new(){Title="散步，留一点空白",Start=week.AddDays(5).AddHours(10),End=week.AddDays(5).AddHours(11),Color="#ACBCE9"}
  ];
  s.Tasks=[
   new(){Title="完成研究报告",Start=now.Date.AddDays(-3),End=now.Date.AddDays(17),Color="#B7ABD4",Milestones=[
    new(){Title="整理资料",Date=now.Date,Completed=true},new(){Title="完成初稿",Date=now.Date.AddDays(8)},new(){Title="提交报告",Date=now.Date.AddDays(17)}]},
   new(){Title="准备作品集",Start=now.Date.AddDays(2),End=now.Date.AddDays(24),Color="#ACBCE9",Milestones=[
    new(){Title="确定目录",Date=now.Date.AddDays(5)},new(){Title="完成排版",Date=now.Date.AddDays(18)}]},
   new(){Title="本月阅读计划",Start=now.Date.AddDays(-7),End=now.Date.AddDays(10),Color="#F2EDD4",Milestones=[new(){Title="阅读记录",Date=now.Date.AddDays(6)}]}
  ];
  return s;
 }
}
