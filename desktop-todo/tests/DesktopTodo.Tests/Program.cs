using DesktopTodo.Core;
var rootIndex=Array.IndexOf(args,"--test-root");
if(rootIndex<0)throw new Exception("An explicit E-drive test root is required.");
var root=Path.GetFullPath(args[rootIndex+1]);
Directory.CreateDirectory(root);
int passed=0,failed=0;
void Test(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+" :: "+ex.Message);}}
void Assert(bool condition,string message="assertion failed"){if(!condition)throw new Exception(message);}
void Reject(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Assert(rejected,"invalid data was accepted");}
DateTime D(int year,int month,int day,int hour=0,int minute=0)=>new(year,month,day,hour,minute,0);
CalendarEvent Weekly()=>new(){Title="Weekly",Start=D(2026,10,5,9),End=D(2026,10,5,10),Weekly=true};
AppState State(CalendarEvent e)=>new(){Events=[e]};
Test("Monday is week start",()=>Assert(CalendarLogic.WeekStart(D(2026,10,12))==D(2026,10,12)));
Test("Sunday belongs to prior Monday",()=>Assert(CalendarLogic.WeekStart(D(2026,10,18))==D(2026,10,12)));
Test("Week crosses year",()=>Assert(CalendarLogic.WeekStart(D(2027,1,1))==D(2026,12,28)));
Test("Leap day occurrence",()=>{var e=new CalendarEvent{Title="Leap",Start=D(2028,2,29,9),End=D(2028,2,29,10)};Assert(CalendarLogic.Expand(e,D(2028,2,1),D(2028,3,1)).Count()==1);});
Test("Weekly expansion",()=>Assert(CalendarLogic.Expand(Weekly(),D(2026,10,1),D(2026,11,1)).Count()==4));
Test("Weekly end inclusive",()=>{var e=Weekly();e.RepeatUntil=D(2026,10,12);Assert(CalendarLogic.Expand(e,D(2026,10,1),D(2026,11,1)).Count()==2);});
Test("Weekly exception",()=>{var e=Weekly();e.Exceptions.Add(D(2026,10,12));Assert(CalendarLogic.Expand(e,D(2026,10,1),D(2026,11,1)).Count()==3);});
Test("No occurrences before series start",()=>Assert(!CalendarLogic.Expand(Weekly(),D(2020,1,1),D(2020,2,1)).Any()));
Test("Far future expansion bounded to range",()=>Assert(CalendarLogic.Expand(Weekly(),D(2040,1,1),D(2040,2,1)).Count() is >=4 and <=5));
Test("Cross midnight event in both days",()=>{var e=new CalendarEvent{Title="Night",Start=D(2026,10,12,23),End=D(2026,10,13,1)};Assert(CalendarLogic.Expand(e,D(2026,10,13),D(2026,10,14)).Count()==1);});
Test("End boundary is exclusive",()=>{var e=Weekly();Assert(!CalendarLogic.Expand(e,D(2026,10,5,10),D(2026,10,5,11)).Any());});
Test("Edit one occurrence preserves history",()=>{
 var e=Weekly();var s=State(e);var o=CalendarLogic.Occurrences(s,D(2026,10,12),D(2026,10,13)).Single();var edit=Copies.Clone(e);edit.Title="changed";edit.Start=D(2026,10,13,14);edit.End=D(2026,10,13,15);
 CalendarLogic.EditOccurrence(s,o,edit,false);Validation.State(s);
 Assert(CalendarLogic.Occurrences(s,D(2026,10,5),D(2026,10,6)).Single().Source.Title=="Weekly");
 Assert(!CalendarLogic.Occurrences(s,D(2026,10,12),D(2026,10,13)).Any());
 Assert(CalendarLogic.Occurrences(s,D(2026,10,13),D(2026,10,14)).Single().Source.Title=="changed");
 Assert(CalendarLogic.Occurrences(s,D(2026,10,19),D(2026,10,20)).Count==1);
});
Test("Edit future splits series",()=>{
 var e=Weekly();var s=State(e);var o=CalendarLogic.Occurrences(s,D(2026,10,12),D(2026,10,13)).Single();var edit=Copies.Clone(e);edit.Title="future";edit.Start=o.Start.AddHours(1);edit.End=o.End.AddHours(1);
 CalendarLogic.EditOccurrence(s,o,edit,true);Validation.State(s);
 Assert(CalendarLogic.Occurrences(s,D(2026,10,5),D(2026,10,6)).Single().Source.Title=="Weekly");
 Assert(CalendarLogic.Occurrences(s,D(2026,10,19),D(2026,10,20)).Single().Source.Title=="future");
 Assert(CalendarLogic.Occurrences(s,D(2026,10,1),D(2026,11,1)).Count==4);
});
Test("Delete single occurrence",()=>{var e=Weekly();var s=State(e);CalendarLogic.DeleteOccurrence(s,CalendarLogic.Occurrences(s,D(2026,10,12),D(2026,10,13)).Single(),false);Assert(CalendarLogic.Occurrences(s,D(2026,10,1),D(2026,11,1)).Count==3);});
Test("Delete future preserves past",()=>{var e=Weekly();var s=State(e);CalendarLogic.DeleteOccurrence(s,CalendarLogic.Occurrences(s,D(2026,10,12),D(2026,10,13)).Single(),true);Assert(CalendarLogic.Occurrences(s,D(2026,10,1),D(2026,11,1)).Count==1);});
Test("Task shift moves all nodes",()=>{var t=new TodoTask{Title="Task",Start=D(2026,10,1),End=D(2026,10,10),Milestones=[new(){Title="Node",Date=D(2026,10,5)}]};CalendarLogic.ShiftTask(t,3);Assert(t.Start==D(2026,10,4)&&t.End==D(2026,10,13)&&t.Milestones[0].Date==D(2026,10,8));});
Test("Node outside task rejected",()=>Reject(()=>Validation.State(new(){Tasks=[new(){Title="Task",Start=D(2026,10,1),End=D(2026,10,2),Milestones=[new(){Title="Node",Date=D(2026,10,3)}]}]})));
Test("Reverse event range rejected",()=>Reject(()=>Validation.State(State(new(){Title="bad",Start=D(2026,10,1,10),End=D(2026,10,1,9)}))));
Test("Same day task allowed",()=>Validation.State(new(){Tasks=[new(){Title="Day",Start=D(2026,10,1),End=D(2026,10,1)}]}));
Test("Empty title rejected",()=>Reject(()=>Validation.State(State(new(){Start=D(2026,10,1),End=D(2026,10,2)}))));
Test("Bad colors rejected",()=>Reject(()=>Validation.Color("red")));
Test("Custom hex allowed",()=>Validation.Color("#aBcDef"));
Test("Corrupt opacity rejected",()=>Reject(()=>Validation.State(new(){Settings=new(){Opacity=0}})));
Test("Duplicate IDs rejected",()=>{var e=Weekly();Reject(()=>Validation.State(new(){Events=[e,Copies.Clone(e)]}));});
Test("Future reminders not emitted early",()=>{var s=State(Weekly());Assert(ReminderLogic.Due(s,D(2026,10,5,8),D(2026,10,5,8,49)).Count==0);});
Test("Event reminder due exactly",()=>{var s=State(Weekly());Assert(ReminderLogic.Due(s,D(2026,10,5,8),D(2026,10,5,8,50)).Count==1);});
Test("Reminders deduplicated",()=>{var s=State(Weekly());var due=ReminderLogic.Due(s,D(2026,10,5,8),D(2026,10,5,9));s.DeliveredReminders.Add(due.Single().Key);Assert(ReminderLogic.Due(s,D(2026,10,5,8),D(2026,10,5,9)).Count==0);});
Test("Completed task and nodes silent",()=>{var s=new AppState{Tasks=[new(){Title="Done",Start=D(2026,10,1),End=D(2026,10,5),Completed=true,Milestones=[new(){Title="Node",Date=D(2026,10,5)}]}]};Assert(ReminderLogic.Due(s,D(2026,10,5),D(2026,10,6)).Count==0);});
Test("Task and node deadlines",()=>{var s=new AppState{Tasks=[new(){Title="Task",Start=D(2026,10,1),End=D(2026,10,5),Milestones=[new(){Title="Node",Date=D(2026,10,5)}]}]};Assert(ReminderLogic.Due(s,D(2026,10,5),D(2026,10,5,9)).Count==2);});
Test("Clock moved backwards is safe",()=>Assert(ReminderLogic.Due(State(Weekly()),D(2026,10,6),D(2026,10,5)).Count==0));
Test("Overlap columns reusable",()=>{var e=Weekly();var a=new Occurrence(e,D(2026,10,5,9),D(2026,10,5,10));var b=new Occurrence(e,D(2026,10,5,9,30),D(2026,10,5,11));var c=new Occurrence(e,D(2026,10,5,10),D(2026,10,5,12));Assert(CalendarLogic.LayoutColumns([a,b,c]).SequenceEqual(new[]{0,1,0}));});
Test("SQLite save and reopen preserves future and past",()=>{
 var dir=Path.Combine(root,Guid.NewGuid().ToString("N"));
 using(var store=new Store(dir))store.Update(s=>{s.Events.Add(new(){Title="Past",Start=D(2020,1,1),End=D(2020,1,2)});s.Events.Add(new(){Title="Future",Start=D(2040,1,1),End=D(2040,1,2)});});
 using(var reopened=new Store(dir))Assert(reopened.Current.Events.Count==2);
});
Test("Invalid update rolls back memory and database",()=>{
 var dir=Path.Combine(root,Guid.NewGuid().ToString("N"));
 using(var store=new Store(dir)){store.Save(Demo.Create(D(2026,10,12)));int count=store.Current.Events.Count;Reject(()=>store.Update(s=>s.Events.Add(new(){Title=""})));Assert(store.Current.Events.Count==count);}
 using(var reopened=new Store(dir))Assert(reopened.Current.Events.Count==6);
});
Test("Backup restore recovers all tasks and settings",()=>{
 using var store=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));store.Save(Demo.Create(D(2026,10,12)));
 var backup=Path.Combine(store.DirectoryPath,"test.backup.json");store.Backup(backup);
 store.Update(s=>{s.Tasks.Clear();s.Settings.Theme="海岸蓝";});store.Restore(backup);
 Assert(store.Current.Tasks.Count==3 && store.Current.Settings.Theme=="雪景紫");
 Assert(Directory.GetFiles(Path.Combine(store.DirectoryPath,"backups"),"before-restore-*").Length==1);
});
Test("Unsupported backup leaves current data intact",()=>{
 using var store=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));store.Save(Demo.Create(D(2026,10,12)));
 var bad=Copies.Clone(store.Current);bad.Version=999;var path=Path.Combine(store.DirectoryPath,"bad.json");File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(bad));
 Reject(()=>store.Restore(path));Assert(store.Current.Tasks.Count==3);
});
Test("All palettes and demo validate",()=>{foreach(var p in Palette.All){var s=Demo.Create(D(2026,10,12));s.Settings.Theme=p.Name;Validation.State(s);Validation.Color(Palette.For(s.Settings).Background);}});

Test("Old data gets compatible view and reminder defaults",()=>{
 var json="""{"Version":1,"Events":[],"Tasks":[{"Title":"Old","Start":"2026-10-01","End":"2026-10-05","ReminderHour":17}],"Settings":{}}""";
 var s=System.Text.Json.JsonSerializer.Deserialize<AppState>(json,Copies.Options)!;Validation.State(s);
 Assert(s.Settings.TimelineDays==30&&s.Settings.TimelineBarHeight==11&&s.Settings.TimelineRowHeight==36&&s.Settings.WeekZoom==1);
 Assert(s.Tasks[0].ReminderDaysBefore==0&&s.Tasks[0].ReminderMinute==0&&ReminderLogic.TaskAt(s.Tasks[0])==D(2026,10,5,17));
});
Test("Independent row height and thickness bounds",()=>{
 foreach(int height in new[]{36,72,144})foreach(int bar in new[]{6,11,22})Validation.State(new(){Settings=new(){TimelineRowHeight=height,TimelineBarHeight=bar}});
 Reject(()=>Validation.State(new(){Settings=new(){TimelineRowHeight=35}}));Reject(()=>Validation.State(new(){Settings=new(){TimelineBarHeight=23}}));
});
Test("Custom days and calendar zoom validation",()=>{
 foreach(int days in new[]{1,14,30,3650})Validation.State(new(){Settings=new(){TimelineDays=days,WeekZoom=.5,MonthZoom=2}});
 Reject(()=>Validation.State(new(){Settings=new(){TimelineDays=0}}));Reject(()=>Validation.State(new(){Settings=new(){TimelineDays=3651}}));
 Reject(()=>Validation.State(new(){Settings=new(){WeekZoom=double.NaN}}));Reject(()=>Validation.State(new(){Settings=new(){MonthZoom=2.1}}));
});
Test("Timeline zoom changes at least one day within bounds",()=>{
 Assert(ViewMath.ZoomDays(1,-1)==2);Assert(ViewMath.ZoomDays(1,1)==1);Assert(ViewMath.ZoomDays(3650,-1)==3650);Assert(ViewMath.ZoomDays(30,1)==25);Assert(ViewMath.ZoomDays(30,-1)==36);
});
Test("Timeline zoom keeps pointer date across year",()=>{
 var start=D(2026,12,20);double fraction=.62;var after=ViewMath.AnchoredStart(start,30,25,fraction);
 Assert(start.AddDays(Math.Floor(30*fraction))==after.AddDays(Math.Floor(25*fraction)));
});
Test("Calendar zoom offset preserves pointer content position",()=>{
 double offset=400,pointer=80;double updated=ViewMath.ZoomOffset(offset,pointer,1,1.5);Assert(Math.Abs((updated+pointer)/1.5-(offset+pointer))<.001);
});
Test("Task reminder days and minute across year",()=>{
 var t=new TodoTask{Title="New year",Start=D(2026,12,20),End=D(2027,1,2),ReminderDaysBefore=3,ReminderHour=9,ReminderMinute=25};var s=new AppState{Tasks=[t]};Validation.State(s);
 Assert(ReminderLogic.TaskAt(t)==D(2026,12,30,9,25));Assert(ReminderLogic.Due(s,D(2026,12,30,9,24),D(2026,12,30,9,25)).Count==1);
 Assert(!ReminderLogic.Due(s,D(2027,1,2),D(2027,1,3)).Any());
});
Test("Node reminder independently crosses leap day",()=>{
 var t=new TodoTask{Title="Task",Start=D(2028,2,20),End=D(2028,3,10),Reminder=false,Milestones=[new(){Title="Node",Date=D(2028,3,1),ReminderDaysBefore=1,ReminderHour=16,ReminderMinute=45}]};var s=new AppState{Tasks=[t]};Validation.State(s);
 Assert(ReminderLogic.NodeAt(t.Milestones[0])==D(2028,2,29,16,45));Assert(ReminderLogic.Due(s,D(2028,2,29,16,44),D(2028,2,29,16,45)).Single().Title=="Node");
});
Test("Invalid advance reminder rejected",()=>{
 Reject(()=>Validation.State(new(){Tasks=[new(){Title="Bad",ReminderDaysBefore=-1}]}));
 Reject(()=>Validation.State(new(){Tasks=[new(){Title="Bad",ReminderDaysBefore=3651}]}));
 Reject(()=>Validation.State(new(){Tasks=[new(){Title="Bad",ReminderMinute=60}]}));
});
Test("Advanced reminders disabled completed and deduplicated",()=>{
 var t=new TodoTask{Title="Task",Start=D(2026,10,1),End=D(2026,10,10),ReminderDaysBefore=2,ReminderHour=9,ReminderMinute=30};var s=new AppState{Tasks=[t]};
 var due=ReminderLogic.Due(s,D(2026,10,8,9),D(2026,10,8,10));s.DeliveredReminders=due.Select(x=>x.Key).ToList();Assert(due.Count==1&&!ReminderLogic.Due(s,D(2026,10,8),D(2026,10,9)).Any());
 s.DeliveredReminders.Clear();t.Reminder=false;Assert(!ReminderLogic.Due(s,D(2026,10,8),D(2026,10,9)).Any());t.Reminder=true;t.Completed=true;Assert(!ReminderLogic.Due(s,D(2026,10,8),D(2026,10,9)).Any());
});
Test("Content history undo redo persists changes",()=>{
 var dir=Path.Combine(root,Guid.NewGuid().ToString("N"));
 using(var st=new Store(dir)){st.Edit("Add",s=>s.Events.Add(Weekly()));Assert(st.CanUndo&&!st.CanRedo);st.Undo();Assert(st.Current.Events.Count==0&&st.CanRedo);st.Redo();Assert(st.Current.Events.Count==1);}
 using(var st=new Store(dir)){Assert(st.Current.Events.Count==1&&!st.CanUndo&&!st.CanRedo);}
});
Test("History preserves settings clock and delivered reminders",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));st.Edit("Add",s=>s.Events.Add(Weekly()));
 var clock=D(2026,10,5,12);st.Update(s=>{s.LastReminderCheck=clock;s.DeliveredReminders.Add("already-sent");s.Settings.Opacity=.4;},false);
 st.Undo();Assert(st.Current.Events.Count==0&&st.Current.LastReminderCheck==clock&&st.Current.Settings.Opacity==.4&&st.Current.DeliveredReminders.Contains("already-sent"));
 st.Redo();Assert(st.Current.Events.Count==1&&st.Current.LastReminderCheck==clock&&st.Current.DeliveredReminders.Contains("already-sent"));
});
Test("New edit clears redo but view changes do not",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));st.Edit("Add",s=>s.Events.Add(Weekly()));st.Undo();
 st.Update(s=>s.Settings.TimelineDays=14);Assert(st.CanRedo);st.Edit("Other",s=>s.Tasks.Add(new(){Title="Task"}));Assert(!st.CanRedo&&st.UndoCount==1);
});
Test("Noop and invalid edits do not enter history",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));st.Edit("Noop",s=>{});Assert(!st.CanUndo);
 Reject(()=>st.Edit("Invalid",s=>s.Tasks.Add(new(){Title=""})));Assert(!st.CanUndo&&st.Current.Tasks.Count==0);
 st.Edit("Add",s=>s.Events.Add(Weekly()));st.Edit("Same",s=>s.Events[0].Title="Weekly");Assert(st.UndoCount==1);
});
Test("History is bounded to latest 100 operations",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));st.Save(new(){Tasks=[new(){Title="Initial"}]});
 for(int i=0;i<110;i++){string title=i.ToString();st.Edit("Rename",s=>s.Tasks[0].Title=title);}Assert(st.UndoCount==100);
 for(int i=0;i<100;i++)st.Undo();Assert(!st.CanUndo&&st.Current.Tasks[0].Title=="9");
 for(int i=0;i<100;i++)st.Redo();Assert(st.Current.Tasks[0].Title=="109"&&!st.CanRedo);
});
Test("Task deletion restores all nodes and completion in one step",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));st.Save(Demo.Create(D(2026,10,12)));var task=Copies.Clone(st.Current.Tasks[0]);
 st.Edit("Delete task",s=>s.Tasks.RemoveAt(0));st.Undo();var recovered=st.Current.Tasks.Single(t=>t.Id==task.Id);
 Assert(recovered.Milestones.Count==task.Milestones.Count&&recovered.Milestones[0].Completed);st.Redo();Assert(st.Current.Tasks.All(t=>t.Id!=task.Id));
});
Test("Recurring future edit undo restores original series and exceptions",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));var e=Weekly();e.Exceptions.Add(D(2026,10,26));st.Save(State(e));var original=System.Text.Json.JsonSerializer.Serialize(st.Current.Events);
 var occurrence=CalendarLogic.Occurrences(st.Current,D(2026,10,12),D(2026,10,13)).Single();var edited=Copies.Clone(e);edited.Title="Changed";edited.Start=occurrence.Start.AddHours(2);edited.End=occurrence.End.AddHours(2);
 st.Edit("Future",s=>CalendarLogic.EditOccurrence(s,occurrence,edited,true));Assert(st.Current.Events.Count==2);st.Undo();Assert(System.Text.Json.JsonSerializer.Serialize(st.Current.Events)==original);st.Redo();Assert(st.Current.Events.Count==2);
});
Test("Failed restore preserves history successful restore clears it",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));var path=Path.Combine(st.DirectoryPath,"before.json");st.Backup(path);st.Edit("Add",s=>s.Events.Add(Weekly()));
 var bad=Path.Combine(st.DirectoryPath,"bad.json");File.WriteAllText(bad,"{\"Version\":999}");Reject(()=>st.Restore(bad));Assert(st.UndoCount==1);
 st.Restore(path);Assert(!st.CanUndo&&!st.CanRedo&&st.Current.Events.Count==0);
});
Test("Database failure does not move undo history or in-memory state",()=>{
 using var st=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));st.Edit("Add",s=>s.Events.Add(Weekly()));
 using var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(st.DirectoryPath,"desktop-todo.db"));db.Open();using var cmd=db.CreateCommand();
 cmd.CommandText="CREATE TRIGGER test_reject BEFORE UPDATE ON app_state BEGIN SELECT RAISE(FAIL,'test failure'); END;";cmd.ExecuteNonQuery();
 bool rejected=false;try{st.Undo();}catch(Microsoft.Data.Sqlite.SqliteException){rejected=true;}
 Assert(rejected&&st.Current.Events.Count==1&&st.UndoCount==1&&!st.CanRedo);
 cmd.CommandText="DROP TRIGGER test_reject";cmd.ExecuteNonQuery();st.Undo();Assert(st.Current.Events.Count==0&&st.CanRedo);
});
Test("Large content has no artificial item limit",()=>{
 var s=new AppState();
 for(int i=0;i<1000;i++){s.Tasks.Add(new(){Title="Task "+i,Start=D(2026,10,1),End=D(2026,10,30)});s.Events.Add(new(){Title="Event "+i,Start=D(2026,10,12,9),End=D(2026,10,12,10)});}
 Validation.State(s);Assert(CalendarLogic.Occurrences(s,D(2026,10,12),D(2026,10,13)).Count==1000);
});

Console.WriteLine($"RESULT {passed} passed; {failed} failed");
File.WriteAllText(Path.Combine(root,"results.txt"),$"{passed} passed; {failed} failed");
Environment.ExitCode=failed==0?0:1;
